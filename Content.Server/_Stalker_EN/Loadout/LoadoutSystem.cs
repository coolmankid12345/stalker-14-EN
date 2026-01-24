using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Content.Server._Stalker.StalkerDB;
using Content.Server._Stalker.StalkerRepository;
using Content.Server._Stalker.Storage;
using Content.Server.Database;
using Content.Server.Hands.Systems;
using Content.Server.Popups;
using Content.Shared._Stalker.StalkerRepository;
using Content.Shared._Stalker.Storage;
using Content.Shared._Stalker_EN.Loadout;
using Content.Shared.Hands.Components;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Storage;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Player;

namespace Content.Server._Stalker_EN.Loadout;

public sealed class LoadoutSystem : EntitySystem
{
    [Dependency] private readonly IServerDbManager _dbManager = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly HandsSystem _hands = default!;
    [Dependency] private readonly StalkerStorageSystem _stalkerStorage = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    private ISawmill _sawmill = default!;

    private const int MaxRecursionDepth = 5;
    private const int MaxLoadoutNameLength = 32;
    private static readonly string[] ContainerFallbacks = { "storagebase", "storage" };

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = Logger.GetSawmill("loadout");

        SubscribeLocalEvent<StalkerRepositoryComponent, LoadoutSaveMessage>(OnSaveMessage);
        SubscribeLocalEvent<StalkerRepositoryComponent, LoadoutLoadMessage>(OnLoadMessage);
        SubscribeLocalEvent<StalkerRepositoryComponent, LoadoutDeleteMessage>(OnDeleteMessage);
        SubscribeLocalEvent<StalkerRepositoryComponent, LoadoutRenameMessage>(OnRenameMessage);
        SubscribeLocalEvent<StalkerRepositoryComponent, LoadoutRequestMessage>(OnRequestMessage);
        SubscribeLocalEvent<StalkerRepositoryComponent, LoadoutQuickStoreMessage>(OnQuickStoreMessage);
    }

    #region Message Handlers

    /// <summary>
    /// Gets the owner for loadout operations. Falls back to actor's session name when StorageOwner is empty.
    /// This handles map stashes that don't go through the portal/teleport system.
    /// </summary>
    private string? GetOwner(StalkerRepositoryComponent component, EntityUid actor)
    {
        // Try component first (set by portal system)
        if (!string.IsNullOrEmpty(component.StorageOwner))
            return component.StorageOwner;

        // Fallback to actor's session name (for map stashes)
        if (TryComp<ActorComponent>(actor, out var actorComp))
            return actorComp.PlayerSession.Name;

        return null;
    }

    private async void OnSaveMessage(EntityUid uid, StalkerRepositoryComponent component, LoadoutSaveMessage msg)
    {
        if (msg.Actor == null)
            return;

        var owner = GetOwner(component, msg.Actor);
        if (string.IsNullOrEmpty(owner))
        {
            _sawmill.Warning("Cannot save loadout: could not determine owner");
            return;
        }

        var name = msg.IsQuickSave ? "Quick Save" : msg.Name.Trim();
        if (string.IsNullOrEmpty(name))
        {
            _popup.PopupEntity(Loc.GetString("loadout-name-required"), msg.Actor, msg.Actor, PopupType.SmallCaution);
            return;
        }

        if (name.Length > MaxLoadoutNameLength)
            name = name[..MaxLoadoutNameLength];

        // Capture loadout metadata (items stay equipped - use Quick Store to move items to stash)
        var loadout = CaptureCurrentLoadout(msg.Actor, name, msg.IsQuickSave ? 0 : -1);
        if (loadout == null || loadout.SlotItems.Count == 0 && loadout.HandItems.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("loadout-empty"), msg.Actor, msg.Actor, PopupType.SmallCaution);
            return;
        }

        // Save loadout to database (no stash changes, just metadata)
        try
        {
            await SaveLoadoutAsync(owner, loadout, msg.IsQuickSave);
            _popup.PopupEntity(Loc.GetString("loadout-saved"), msg.Actor, msg.Actor, PopupType.Small);
            await SendLoadoutStateUpdateAsync(uid, component, msg.Actor);
        }
        catch (Exception e)
        {
            _sawmill.Error($"Failed to save loadout: {e}");
        }
    }

    private async void OnLoadMessage(EntityUid uid, StalkerRepositoryComponent component, LoadoutLoadMessage msg)
    {
        if (msg.Actor == null)
            return;

        var owner = GetOwner(component, msg.Actor);
        if (string.IsNullOrEmpty(owner))
        {
            _sawmill.Warning("Cannot load loadout: could not determine owner");
            return;
        }

        try
        {
            var container = await GetLoadoutsAsync(owner);
            if (container == null)
            {
                _popup.PopupEntity(Loc.GetString("loadout-not-found"), msg.Actor, msg.Actor, PopupType.SmallCaution);
                return;
            }

            var loadout = container.Loadouts.FirstOrDefault(l => l.Id == msg.LoadoutId);
            if (loadout == null)
            {
                _popup.PopupEntity(Loc.GetString("loadout-not-found"), msg.Actor, msg.Actor, PopupType.SmallCaution);
                return;
            }

            // This now runs on main thread after await
            var result = ApplyLoadout(msg.Actor, (uid, component), loadout);
            if (result.Success)
            {
                if (result.MissingCount > 0)
                {
                    _popup.PopupEntity(
                        Loc.GetString("loadout-loaded-partial", ("missing", result.MissingCount)),
                        msg.Actor, msg.Actor, PopupType.Medium);
                }
                else
                {
                    _popup.PopupEntity(Loc.GetString("loadout-loaded"), msg.Actor, msg.Actor, PopupType.Small);
                }
            }
            else
            {
                _popup.PopupEntity(Loc.GetString("loadout-load-failed"), msg.Actor, msg.Actor, PopupType.SmallCaution);
            }

            _stalkerStorage.SaveStorage(component);

            // Raise event so StalkerRepositorySystem can refresh both UIs
            // Repository system will send loadout state AFTER repository state
            var ev = new LoadoutOperationCompletedEvent(msg.Actor, uid);
            RaiseLocalEvent(uid, ref ev);
        }
        catch (Exception e)
        {
            _sawmill.Error($"Failed to load loadout: {e}");
        }
    }

    private async void OnDeleteMessage(EntityUid uid, StalkerRepositoryComponent component, LoadoutDeleteMessage msg)
    {
        if (msg.Actor == null)
            return;

        var owner = GetOwner(component, msg.Actor);
        if (string.IsNullOrEmpty(owner))
            return;

        try
        {
            var container = await GetLoadoutsAsync(owner);
            if (container == null)
                return;

            var loadout = container.Loadouts.FirstOrDefault(l => l.Id == msg.LoadoutId);
            if (loadout == null)
                return;

            container.Loadouts.Remove(loadout);
            await SetLoadoutsAsync(owner, container);

            _popup.PopupEntity(Loc.GetString("loadout-deleted"), msg.Actor, msg.Actor, PopupType.Small);
            await SendLoadoutStateUpdateAsync(uid, component, msg.Actor);
        }
        catch (Exception e)
        {
            _sawmill.Error($"Failed to delete loadout: {e}");
        }
    }

    private async void OnRenameMessage(EntityUid uid, StalkerRepositoryComponent component, LoadoutRenameMessage msg)
    {
        if (msg.Actor == null)
            return;

        var owner = GetOwner(component, msg.Actor);
        if (string.IsNullOrEmpty(owner))
            return;

        var newName = msg.NewName.Trim();
        if (string.IsNullOrEmpty(newName))
            return;

        if (newName.Length > MaxLoadoutNameLength)
            newName = newName[..MaxLoadoutNameLength];

        try
        {
            var container = await GetLoadoutsAsync(owner);
            if (container == null)
                return;

            var loadout = container.Loadouts.FirstOrDefault(l => l.Id == msg.LoadoutId);
            if (loadout == null)
                return;

            loadout.Name = newName;
            await SetLoadoutsAsync(owner, container);

            await SendLoadoutStateUpdateAsync(uid, component, msg.Actor);
        }
        catch (Exception e)
        {
            _sawmill.Error($"Failed to rename loadout: {e}");
        }
    }

    private void OnRequestMessage(EntityUid uid, StalkerRepositoryComponent component, LoadoutRequestMessage msg)
    {
        if (msg.Actor == null)
            return;

        SendLoadoutStateUpdate(uid, component, msg.Actor);
    }

    private void OnQuickStoreMessage(EntityUid uid, StalkerRepositoryComponent component, LoadoutQuickStoreMessage msg)
    {
        if (msg.Actor == null)
            return;

        MoveEquipmentToStash(msg.Actor, (uid, component));
        _popup.PopupEntity(Loc.GetString("loadout-items-stored"), msg.Actor, msg.Actor, PopupType.Small);

        // Refresh stash UI
        var ev = new LoadoutOperationCompletedEvent(msg.Actor, uid);
        RaiseLocalEvent(uid, ref ev);
    }

    #endregion

    #region Save Logic

    /// <summary>
    /// Captures the current equipment of a player as a loadout.
    /// </summary>
    private PlayerLoadout? CaptureCurrentLoadout(EntityUid player, string name, int forcedId = -1)
    {
        var loadout = new PlayerLoadout
        {
            Name = name,
            CreatedAt = DateTime.UtcNow
        };

        // Capture inventory slots
        if (_inventory.TryGetContainerSlotEnumerator(player, out var enumerator))
        {
            while (enumerator.NextItem(out var item, out var slotDef))
            {
                var slotItem = CaptureSlotItem(item, slotDef.Name);
                if (slotItem != null)
                    loadout.SlotItems.Add(slotItem);
            }
        }

        // Capture hand items
        if (TryComp<HandsComponent>(player, out var hands))
        {
            foreach (var held in _hands.EnumerateHeld(player, hands))
            {
                var nestedItem = CaptureNestedItem(held, "hand", 0);
                if (nestedItem != null)
                    loadout.HandItems.Add(nestedItem);
            }
        }

        if (forcedId >= 0)
            loadout.Id = forcedId;

        return loadout;
    }

    private LoadoutSlotItem? CaptureSlotItem(EntityUid item, string slotName)
    {
        var meta = MetaData(item);
        if (meta.EntityPrototype == null)
            return null;

        var storageData = _stalkerStorage.ConvertToIItemStalkerStorage(item);
        var identifier = "";
        if (storageData.Count > 0 && storageData[0] is IItemStalkerStorage iss)
            identifier = iss.Identifier();

        var slotItem = new LoadoutSlotItem
        {
            SlotName = slotName,
            PrototypeId = meta.EntityPrototype.ID,
            StorageData = storageData.Count > 0 ? storageData[0] : null,
            Identifier = identifier
        };

        // Capture nested items in containers
        if (TryComp<ContainerManagerComponent>(item, out var containerMan))
        {
            foreach (var container in containerMan.Containers)
            {
                if (IsBlacklistedContainer(container.Key))
                    continue;

                foreach (var contained in container.Value.ContainedEntities)
                {
                    var nested = CaptureNestedItem(contained, container.Key, 1);
                    if (nested != null)
                        slotItem.NestedItems.Add(nested);
                }
            }
        }

        return slotItem;
    }

    private LoadoutNestedItem? CaptureNestedItem(EntityUid item, string containerName, int depth)
    {
        if (depth > MaxRecursionDepth)
            return null;

        if (IsBlacklistedEntity(item))
            return null;

        var meta = MetaData(item);
        if (meta.EntityPrototype == null)
            return null;

        var storageData = _stalkerStorage.ConvertToIItemStalkerStorage(item);
        var identifier = "";
        if (storageData.Count > 0 && storageData[0] is IItemStalkerStorage iss)
            identifier = iss.Identifier();

        var nestedItem = new LoadoutNestedItem
        {
            ContainerName = containerName,
            PrototypeId = meta.EntityPrototype.ID,
            StorageData = storageData.Count > 0 ? storageData[0] : null,
            Identifier = identifier
        };

        // Recursively capture nested items
        if (TryComp<ContainerManagerComponent>(item, out var containerMan))
        {
            foreach (var container in containerMan.Containers)
            {
                if (IsBlacklistedContainer(container.Key))
                    continue;

                foreach (var contained in container.Value.ContainedEntities)
                {
                    var nested = CaptureNestedItem(contained, container.Key, depth + 1);
                    if (nested != null)
                        nestedItem.NestedItems.Add(nested);
                }
            }
        }

        return nestedItem;
    }

    private async Task SaveLoadoutAsync(string owner, PlayerLoadout loadout, bool isQuickSave)
    {
        var container = await GetLoadoutsAsync(owner) ?? new AllLoadoutsContainer();

        if (isQuickSave)
        {
            // Replace existing quick save
            var existing = container.Loadouts.FirstOrDefault(l => l.Id == 0);
            if (existing != null)
                container.Loadouts.Remove(existing);
            loadout.Id = 0;
        }
        else
        {
            loadout.Id = container.GetNextId();
        }

        container.Loadouts.Add(loadout);
        await SetLoadoutsAsync(owner, container);
    }

    #endregion

    #region Load Logic

    private record struct LoadResult(bool Success, int MissingCount);

    /// <summary>
    /// Applies a loadout to a player, pulling items from their stash.
    /// First moves any currently equipped items to stash, then equips loadout items.
    /// </summary>
    private LoadResult ApplyLoadout(EntityUid player, Entity<StalkerRepositoryComponent> repository, PlayerLoadout loadout)
    {
        var missingCount = 0;

        // First, move any currently equipped items to stash
        MoveEquipmentToStash(player, repository);

        // Rebuild lookup after moving items (stash contents changed)
        var stashLookup = BuildStashLookup(repository.Comp.ContainedItems);

        // Process each slot in the loadout
        foreach (var slotItem in loadout.SlotItems)
        {
            if (!TryEquipSlotItem(player, repository, slotItem, stashLookup))
                missingCount++;
        }

        // Process hand items
        foreach (var handItem in loadout.HandItems)
        {
            if (!TryEquipHandItem(player, repository, handItem, stashLookup))
                missingCount++;
        }

        return new LoadResult(true, missingCount);
    }

    private bool TryEquipSlotItem(
        EntityUid player,
        Entity<StalkerRepositoryComponent> repository,
        LoadoutSlotItem slotItem,
        Dictionary<string, RepositoryItemInfo> stashLookup)
    {
        // Find the item in stash
        var stashItem = FindItemInStash(slotItem.Identifier, slotItem.PrototypeId, stashLookup);
        if (stashItem == null)
        {
            _sawmill.Debug($"Item not found in stash: {slotItem.PrototypeId} ({slotItem.Identifier})");
            return false;
        }

        // Note: No need to unequip current items - ApplyLoadout already cleared all equipment

        // Remove item from stash
        RemoveFromStash(repository, stashItem, stashLookup);

        // Spawn the item
        var xform = Transform(player);
        var spawned = Spawn(stashItem.ProductEntity, xform.Coordinates);

        // Restore item state
        if (stashItem.SStorageData is IItemStalkerStorage iss)
        {
            _stalkerStorage.SpawnedItem(spawned, iss);
        }

        // Equip item
        if (!_inventory.TryEquip(player, spawned, slotItem.SlotName, true, true))
        {
            _sawmill.Warning($"Failed to equip item {slotItem.PrototypeId} to slot {slotItem.SlotName}");
            // Put item in hands or drop it
            if (!_hands.TryPickup(player, spawned))
            {
                QueueDel(spawned);
                return false;
            }
        }

        // Restore nested items
        RestoreNestedItems(spawned, slotItem.NestedItems, repository, stashLookup, 0);

        return true;
    }

    private bool TryEquipHandItem(
        EntityUid player,
        Entity<StalkerRepositoryComponent> repository,
        LoadoutNestedItem handItem,
        Dictionary<string, RepositoryItemInfo> stashLookup)
    {
        // Find the item in stash
        var stashItem = FindItemInStash(handItem.Identifier, handItem.PrototypeId, stashLookup);
        if (stashItem == null)
        {
            _sawmill.Debug($"Hand item not found in stash: {handItem.PrototypeId} ({handItem.Identifier})");
            return false;
        }

        // Remove item from stash
        RemoveFromStash(repository, stashItem, stashLookup);

        // Spawn the item
        var xform = Transform(player);
        var spawned = Spawn(stashItem.ProductEntity, xform.Coordinates);

        // Restore item state
        if (stashItem.SStorageData is IItemStalkerStorage iss)
        {
            _stalkerStorage.SpawnedItem(spawned, iss);
        }

        // Try to pick up the item
        if (!_hands.TryPickup(player, spawned))
        {
            _sawmill.Warning($"Failed to pick up hand item {handItem.PrototypeId}");
            QueueDel(spawned);
            return false;
        }

        // Restore nested items
        RestoreNestedItems(spawned, handItem.NestedItems, repository, stashLookup, 0);

        return true;
    }

    private void RestoreNestedItems(
        EntityUid parent,
        List<LoadoutNestedItem> nestedItems,
        Entity<StalkerRepositoryComponent> repository,
        Dictionary<string, RepositoryItemInfo> stashLookup,
        int depth)
    {
        if (depth > MaxRecursionDepth)
            return;

        if (!TryComp<ContainerManagerComponent>(parent, out var containerMan))
            return;

        foreach (var nestedItem in nestedItems)
        {
            // Find the item in stash
            var stashItem = FindItemInStash(nestedItem.Identifier, nestedItem.PrototypeId, stashLookup);
            if (stashItem == null)
            {
                _sawmill.Debug($"Nested item not found in stash: {nestedItem.PrototypeId}");
                continue;
            }

            // Check if container exists
            if (!containerMan.Containers.TryGetValue(nestedItem.ContainerName, out var container))
            {
                // Try common container names in order of likelihood
                container = null;
                foreach (var fallback in ContainerFallbacks)
                {
                    if (containerMan.Containers.TryGetValue(fallback, out var fallbackContainer))
                    {
                        container = fallbackContainer;
                        break;
                    }
                }

                // Also try lowercase of original container name
                if (container == null)
                {
                    var lowerName = nestedItem.ContainerName.ToLower();
                    if (lowerName != nestedItem.ContainerName)
                        containerMan.Containers.TryGetValue(lowerName, out container);
                }

                if (container == null)
                {
                    _sawmill.Debug($"No suitable container found for {nestedItem.PrototypeId} (tried: {nestedItem.ContainerName}, storagebase, storage)");
                    continue;
                }
            }

            // Remove item from stash
            RemoveFromStash(repository, stashItem, stashLookup);

            // Spawn the item
            var xform = Transform(parent);
            var spawned = Spawn(stashItem.ProductEntity, xform.Coordinates);

            // Restore item state
            if (stashItem.SStorageData is IItemStalkerStorage iss)
            {
                _stalkerStorage.SpawnedItem(spawned, iss);
            }

            // Insert into container
            if (!_container.Insert(spawned, container))
            {
                _sawmill.Warning($"Failed to insert {nestedItem.PrototypeId} into container {nestedItem.ContainerName}");
                QueueDel(spawned);
                continue;
            }

            // Recursively restore nested items
            RestoreNestedItems(spawned, nestedItem.NestedItems, repository, stashLookup, depth + 1);
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Unequips all items from player and inserts them into the repository.
    /// This includes inventory slots and hand items.
    /// </summary>
    private void MoveEquipmentToStash(EntityUid player, Entity<StalkerRepositoryComponent> repository)
    {
        // Collect inventory items to move (can't modify while iterating)
        var itemsToMove = new List<(EntityUid item, string slot)>();
        if (_inventory.TryGetContainerSlotEnumerator(player, out var enumerator))
        {
            while (enumerator.NextItem(out var item, out var slotDef))
            {
                itemsToMove.Add((item, slotDef.Name));
            }
        }

        // Move inventory items to stash
        foreach (var (item, slot) in itemsToMove)
        {
            if (_inventory.TryUnequip(player, slot, out var unequipped, true, true))
            {
                InsertItemToStash(repository, unequipped.Value);
            }
        }

        // Collect hand items to move
        if (TryComp<HandsComponent>(player, out var hands))
        {
            var handItems = _hands.EnumerateHeld(player, hands).ToList();
            foreach (var item in handItems)
            {
                if (_hands.TryDrop(player, item, handsComp: hands))
                {
                    InsertItemToStash(repository, item);
                }
            }
        }

        // Save repository state after all insertions
        _stalkerStorage.SaveStorage(repository.Comp);
    }

    /// <summary>
    /// Inserts a single item (with nested contents) into the repository.
    /// Uses the repository's existing insert logic for proper handling of containers.
    /// </summary>
    private void InsertItemToStash(Entity<StalkerRepositoryComponent> repository, EntityUid item)
    {
        // Generate repository info using the repository system
        var info = GenerateRepositoryItemInfo(item);
        if (info == null)
        {
            _sawmill.Warning($"Could not generate repository info for item {ToPrettyString(item)}");
            return;
        }

        // Check weight limit
        var newWeight = repository.Comp.CurrentWeight + info.SumWeight;
        if (newWeight > repository.Comp.MaxWeight)
        {
            _sawmill.Debug($"Stash weight limit reached, cannot insert {info.Name}");
            return;
        }

        // Insert nested items first (items inside containers)
        if (TryComp<ContainerManagerComponent>(item, out var containerMan))
        {
            foreach (var container in containerMan.Containers)
            {
                if (IsBlacklistedContainer(container.Key))
                    continue;

                // Copy the list to avoid modification during iteration
                var containedEntities = container.Value.ContainedEntities.ToList();
                foreach (var contained in containedEntities)
                {
                    if (IsBlacklistedEntity(contained))
                        continue;

                    // Recursively insert nested items
                    InsertItemToStash(repository, contained);
                }
            }
        }

        // Insert the item itself
        InsertToRepository(repository, info);

        // Delete the physical entity
        QueueDel(item);
    }

    private Dictionary<string, RepositoryItemInfo> BuildStashLookup(List<RepositoryItemInfo> items)
    {
        var lookup = new Dictionary<string, RepositoryItemInfo>();
        foreach (var item in items)
        {
            // Primary lookup by identifier
            lookup.TryAdd(item.Identifier, item);
            // Fallback lookup by prototype
            lookup.TryAdd($"proto:{item.ProductEntity}", item);
        }
        return lookup;
    }

    private RepositoryItemInfo? FindItemInStash(string identifier, string prototypeId, Dictionary<string, RepositoryItemInfo> lookup)
    {
        // Try exact match first
        if (lookup.TryGetValue(identifier, out var item) && item.Count > 0)
            return item;

        // Fallback to prototype match
        if (lookup.TryGetValue($"proto:{prototypeId}", out item) && item.Count > 0)
            return item;

        return null;
    }

    private void RemoveFromStash(
        Entity<StalkerRepositoryComponent> repository,
        RepositoryItemInfo item,
        Dictionary<string, RepositoryItemInfo> lookup)
    {
        item.Count--;
        if (item.Count <= 0)
        {
            repository.Comp.ContainedItems.Remove(item);
            // Remove from lookup when exhausted so duplicate items are handled correctly
            lookup.Remove(item.Identifier);
            lookup.Remove($"proto:{item.ProductEntity}");
        }
    }

    private void InsertToRepository(Entity<StalkerRepositoryComponent> repository, RepositoryItemInfo info)
    {
        // Try to find existing stack
        var existing = repository.Comp.ContainedItems.FirstOrDefault(i => i.Identifier == info.Identifier);
        if (existing != null && !HasComp<StorageComponent>(GetEntity(info.Entities?.FirstOrDefault() ?? NetEntity.Invalid)))
        {
            existing.Count++;
            if (existing.SStorageData is IItemStalkerStorage iss)
                iss.CountVendingMachine++;
        }
        else
        {
            repository.Comp.ContainedItems.Add(info);
        }

        repository.Comp.CurrentWeight += info.Weight;
    }

    private RepositoryItemInfo? GenerateRepositoryItemInfo(EntityUid item)
    {
        var meta = MetaData(item);
        if (meta.EntityPrototype == null)
            return null;

        var storageData = _stalkerStorage.ConvertToIItemStalkerStorage(item);
        var identifier = "";
        if (storageData.Count > 0 && storageData[0] is IItemStalkerStorage iss)
            identifier = iss.Identifier();

        if (!TryComp<Content.Shared._Stalker.Weight.STWeightComponent>(item, out var weight))
            return null;

        return new RepositoryItemInfo
        {
            Count = 1,
            ProductEntity = meta.EntityPrototype.ID,
            Name = meta.EntityName,
            Desc = meta.EntityDescription,
            Weight = weight.Self,
            SumWeight = weight.Total,
            SStorageData = storageData.Count > 0 ? storageData[0] : null,
            Identifier = identifier
        };
    }

    private bool IsBlacklistedContainer(string containerName)
    {
        return containerName is "toggleable-clothing" or "actions";
    }

    private bool IsBlacklistedEntity(EntityUid item)
    {
        // Similar to StalkerRepositorySystem blacklist
        return HasComp<Content.Shared.Body.Organ.OrganComponent>(item) ||
               HasComp<Content.Shared.Actions.InstantActionComponent>(item) ||
               HasComp<Content.Shared.Actions.WorldTargetActionComponent>(item) ||
               HasComp<Content.Shared.Actions.EntityTargetActionComponent>(item) ||
               HasComp<Content.Shared.Implants.Components.SubdermalImplantComponent>(item) ||
               HasComp<Content.Shared.Body.Part.BodyPartComponent>(item) ||
               HasComp<Content.Shared.CartridgeLoader.CartridgeComponent>(item) ||
               HasComp<Content.Shared.Inventory.VirtualItem.VirtualItemComponent>(item) ||
               HasComp<Content.Shared.Mind.Components.MindContainerComponent>(item) ||
               HasComp<Content.Shared.Chemistry.Components.SolutionComponent>(item);
    }

    /// <summary>
    /// Sends the loadout state update to the UI asynchronously.
    /// </summary>
    public async Task SendLoadoutStateUpdateAsync(EntityUid uid, StalkerRepositoryComponent component, EntityUid? actor = null)
    {
        var owner = actor.HasValue ? GetOwner(component, actor.Value) : component.StorageOwner;
        if (string.IsNullOrEmpty(owner))
            return;

        try
        {
            var container = await GetLoadoutsAsync(owner);
            var loadouts = container?.Loadouts ?? new List<PlayerLoadout>();

            // Calculate missing items for each loadout
            var stashLookup = BuildStashLookup(component.ContainedItems);
            foreach (var loadout in loadouts)
            {
                loadout.MissingCount = CalculateMissingCount(loadout, stashLookup);
            }

            // Sort: Quick Save first, then by name
            loadouts = loadouts.OrderBy(l => l.Id != 0).ThenBy(l => l.Name).ToList();

            _ui.SetUiState(uid, StalkerRepositoryUiKey.Key, new LoadoutUpdateState(loadouts));
        }
        catch (Exception e)
        {
            _sawmill.Error($"Failed to update loadout UI state: {e}");
        }
    }

    private int CalculateMissingCount(PlayerLoadout loadout, Dictionary<string, RepositoryItemInfo> stashLookup)
    {
        // Create a copy of the lookup to track consumed items during count
        var tempLookup = new Dictionary<string, RepositoryItemInfo>();
        foreach (var kvp in stashLookup)
        {
            // Clone the item info to avoid modifying the original
            tempLookup[kvp.Key] = new RepositoryItemInfo
            {
                Count = kvp.Value.Count,
                ProductEntity = kvp.Value.ProductEntity,
                Identifier = kvp.Value.Identifier
            };
        }

        var missing = 0;

        foreach (var slotItem in loadout.SlotItems)
        {
            if (!TryConsumeFromLookup(slotItem.Identifier, slotItem.PrototypeId, tempLookup))
                missing++;
            missing += CountMissingNested(slotItem.NestedItems, tempLookup);
        }

        foreach (var handItem in loadout.HandItems)
        {
            if (!TryConsumeFromLookup(handItem.Identifier, handItem.PrototypeId, tempLookup))
                missing++;
            missing += CountMissingNested(handItem.NestedItems, tempLookup);
        }

        return missing;
    }

    private int CountMissingNested(List<LoadoutNestedItem> items, Dictionary<string, RepositoryItemInfo> lookup)
    {
        var missing = 0;
        foreach (var item in items)
        {
            if (!TryConsumeFromLookup(item.Identifier, item.PrototypeId, lookup))
                missing++;
            missing += CountMissingNested(item.NestedItems, lookup);
        }
        return missing;
    }

    private bool TryConsumeFromLookup(string identifier, string prototypeId, Dictionary<string, RepositoryItemInfo> lookup)
    {
        // Try exact match first
        if (lookup.TryGetValue(identifier, out var item) && item.Count > 0)
        {
            item.Count--;
            return true;
        }

        // Fallback to prototype match
        if (lookup.TryGetValue($"proto:{prototypeId}", out item) && item.Count > 0)
        {
            item.Count--;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Sends the loadout state update to the UI. Called from StalkerRepositorySystem when the UI opens.
    /// </summary>
    public void SendLoadoutStateUpdate(EntityUid uid, StalkerRepositoryComponent component, EntityUid? actor = null)
    {
        _ = SendLoadoutStateUpdateAsync(uid, component, actor);
    }

    #endregion

    #region Database Access

    private async Task<AllLoadoutsContainer?> GetLoadoutsAsync(string owner)
    {
        var json = await _dbManager.GetLoadouts(owner);
        if (string.IsNullOrEmpty(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<AllLoadoutsContainer>(json);
        }
        catch (Exception e)
        {
            _sawmill.Error($"Failed to deserialize loadouts for {owner}: {e}");
            return null;
        }
    }

    private async Task SetLoadoutsAsync(string owner, AllLoadoutsContainer container)
    {
        try
        {
            var json = JsonSerializer.Serialize(container);
            await _dbManager.SetLoadouts(owner, json);
        }
        catch (Exception e)
        {
            _sawmill.Error($"Failed to serialize loadouts for {owner}: {e}");
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Gets all loadouts for a player.
    /// </summary>
    public async Task<List<PlayerLoadout>> GetPlayerLoadoutsAsync(string owner)
    {
        var container = await GetLoadoutsAsync(owner);
        return container?.Loadouts ?? new List<PlayerLoadout>();
    }

    #endregion
}
