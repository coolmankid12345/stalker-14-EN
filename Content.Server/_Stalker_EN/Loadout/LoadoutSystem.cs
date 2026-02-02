using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Content.Server._Stalker.StalkerDB;
using Content.Server._Stalker.StalkerRepository;
using Content.Server._Stalker.Storage;
using Content.Server.Database;
using Content.Server.Players.RateLimiting;
using Content.Shared._Stalker_EN.CCVar;
using Content.Shared.Database;
using Content.Shared.Players.RateLimiting;
using Content.Server.Hands.Systems;
using Content.Server.Popups;
using Content.Shared._Stalker.StalkerRepository;
using Content.Shared._Stalker.Storage;
using Content.Shared._Stalker_EN.Loadout;
using Content.Shared.Hands.Components;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.UserInterface;
using Content.Shared.Whitelist;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Item;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using EntityPrototype = Robust.Shared.Prototypes.EntityPrototype;

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
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelistSystem = default!;
    [Dependency] private readonly StalkerRepositorySystem _repositorySystem = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;
    [Dependency] private readonly PlayerRateLimitManager _rateLimitManager = default!;
    [Dependency] private readonly SharedItemSystem _itemSystem = default!;

    private ISawmill _sawmill = default!;

    private const string RateLimitKey = "Loadout";

    // Prevents race conditions when same actor triggers multiple async loadout operations
    private readonly ConcurrentDictionary<EntityUid, byte> _currentlyProcessingLoadouts = new();

    /// <summary>
    /// Validates that entities still exist and are valid after an async operation.
    /// Returns false if any entity has been deleted or the repository component is missing.
    /// </summary>
    private bool ValidateEntities(
        EntityUid repositoryUid,
        EntityUid actorUid,
        [NotNullWhen(true)] out StalkerRepositoryComponent? component)
    {
        component = null;

        if (!Exists(repositoryUid))
        {
            _sawmill.Warning($"Repository entity {repositoryUid} no longer exists after async operation");
            return false;
        }

        if (!Exists(actorUid))
        {
            _sawmill.Warning($"Actor entity {actorUid} no longer exists after async operation");
            return false;
        }

        if (!TryComp(repositoryUid, out component))
        {
            _sawmill.Warning($"Repository component missing from {repositoryUid} after async operation");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Maximum depth for capturing/restoring nested container items.
    /// Prevents infinite recursion and excessive nesting.
    /// Typical nesting: Equipment > Storage > Item > SubContainer > SubItem (3-4 levels).
    /// Value of 5 provides comfortable margin of safety.
    /// </summary>
    private const int MaxRecursionDepth = 5;

    /// <summary>
    /// Maximum length for loadout names. Names exceeding this are truncated.
    /// </summary>
    private const int MaxLoadoutNameLength = 32;

    /// <summary>
    /// Maximum number of named loadouts allowed per player.
    /// Quick Save (ID 0) does not count against this limit.
    /// </summary>
    private const int MaxNamedLoadouts = 20;

    private static readonly string[] ContainerFallbacks =
    {
        "gun_magazine",           // Magazine slot
        "gun_chamber",            // Chambered round
        "gun_module_muzzle",      // Silencers
        "gun_module_scope",       // Scopes
        "gun_module_underbarrel", // Grips, flashlights
        "gun_auto_sear",          // Auto-sear module
        "revolver-ammo",          // Revolver ammunition
        "item",                   // Boot slots and similar ItemSlots
        "storagebase",            // General storage
        "storage"                 // Fallback storage
    };

    private static readonly HashSet<string> DefaultSlotBlacklist = new() { "id" };
    private static readonly HashSet<string> DefaultContainerBlacklist = new() { "toggleable-clothing", "actions" };

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = Logger.GetSawmill("loadout");

        _rateLimitManager.Register(RateLimitKey, new RateLimitRegistration(
            STCCVars.LoadoutRateLimitPeriod,
            STCCVars.LoadoutRateLimitCount,
            session =>
            {
                if (session.AttachedEntity is { } entity)
                    _popup.PopupEntity(Loc.GetString("loadout-rate-limited"), entity, entity, PopupType.SmallCaution);
            },
            adminLogType: LogType.Action
        ));

        SubscribeLocalEvent<StalkerRepositoryComponent, LoadoutSaveMessage>(OnSaveMessage);
        SubscribeLocalEvent<StalkerRepositoryComponent, LoadoutLoadMessage>(OnLoadMessage);
        SubscribeLocalEvent<StalkerRepositoryComponent, LoadoutDeleteMessage>(OnDeleteMessage);
        SubscribeLocalEvent<StalkerRepositoryComponent, LoadoutRenameMessage>(OnRenameMessage);
        SubscribeLocalEvent<StalkerRepositoryComponent, LoadoutRequestMessage>(OnRequestMessage);
        SubscribeLocalEvent<ActorComponent, ComponentRemove>(OnActorRemoved);
    }

    private void OnActorRemoved(EntityUid uid, ActorComponent component, ComponentRemove args)
    {
        _currentlyProcessingLoadouts.TryRemove(uid, out _);
    }

    #region Message Handlers

    /// <summary>
    /// Gets the owner for loadout operations.
    /// Falls back to actor's session name for map stashes that bypass the portal system.
    /// </summary>
    private string? GetOwner(StalkerRepositoryComponent component, EntityUid actor)
    {
        if (!string.IsNullOrEmpty(component.StorageOwner))
            return component.StorageOwner;

        if (TryComp<ActorComponent>(actor, out var actorComp))
            return actorComp.PlayerSession.Name;

        return null;
    }

    private async void OnSaveMessage(EntityUid uid, StalkerRepositoryComponent component, LoadoutSaveMessage msg)
    {
        if (msg.Actor == null || !_currentlyProcessingLoadouts.TryAdd(msg.Actor, 0))
            return;

        if (TryComp<ActorComponent>(msg.Actor, out var actorComp) &&
            _rateLimitManager.CountAction(actorComp.PlayerSession, RateLimitKey) != RateLimitStatus.Allowed)
        {
            _currentlyProcessingLoadouts.TryRemove(msg.Actor, out _);
            return;
        }

        try
        {
            await ProcessSaveMessage(uid, component, msg);
        }
        finally
        {
            _currentlyProcessingLoadouts.TryRemove(msg.Actor, out _);
        }
    }

    private async Task ProcessSaveMessage(EntityUid uid, StalkerRepositoryComponent component, LoadoutSaveMessage msg)
    {
        if (msg.Actor == null)
            return;

        var owner = GetOwner(component, msg.Actor);
        if (string.IsNullOrEmpty(owner))
        {
            _sawmill.Warning("Cannot save loadout: could not determine owner");
            return;
        }

        var name = msg.IsQuickSave ? "Quick Save" : SanitizeLoadoutName(msg.Name);
        if (string.IsNullOrEmpty(name))
        {
            _popup.PopupEntity(Loc.GetString("loadout-name-required"), msg.Actor, msg.Actor, PopupType.SmallCaution);
            return;
        }

        if (name.Length > MaxLoadoutNameLength)
            name = name[..MaxLoadoutNameLength];

        if (!msg.IsQuickSave)
        {
            var existingContainer = await GetLoadoutsAsync(owner);

            if (!ValidateEntities(uid, msg.Actor, out _))
                return;

            var namedCount = existingContainer?.Loadouts.Count(l => l.Id != 0) ?? 0;
            if (namedCount >= MaxNamedLoadouts)
            {
                _popup.PopupEntity(Loc.GetString("loadout-limit-reached", ("max", MaxNamedLoadouts)), msg.Actor, msg.Actor, PopupType.SmallCaution);
                return;
            }
        }

        var loadout = CaptureCurrentLoadout(msg.Actor, uid, name, msg.IsQuickSave ? 0 : -1);
        if (loadout == null || loadout.SlotItems.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("loadout-empty"), msg.Actor, msg.Actor, PopupType.SmallCaution);
            return;
        }

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
        if (msg.Actor == null || !_currentlyProcessingLoadouts.TryAdd(msg.Actor, 0))
            return;

        if (TryComp<ActorComponent>(msg.Actor, out var actorComp) &&
            _rateLimitManager.CountAction(actorComp.PlayerSession, RateLimitKey) != RateLimitStatus.Allowed)
        {
            _currentlyProcessingLoadouts.TryRemove(msg.Actor, out _);
            return;
        }

        component.LoadoutOperationInProgress = true;
        try
        {
            await ProcessLoadMessage(uid, component, msg);
        }
        finally
        {
            component.LoadoutOperationInProgress = false;
            _currentlyProcessingLoadouts.TryRemove(msg.Actor, out _);
        }
    }

    private async Task ProcessLoadMessage(EntityUid uid, StalkerRepositoryComponent component, LoadoutLoadMessage msg)
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

            if (!ValidateEntities(uid, msg.Actor, out var freshComponent))
                return;

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

            var result = ApplyLoadout(msg.Actor, (uid, freshComponent), loadout);
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
        if (msg.Actor == null || !_currentlyProcessingLoadouts.TryAdd(msg.Actor, 0))
            return;

        if (TryComp<ActorComponent>(msg.Actor, out var actorComp) &&
            _rateLimitManager.CountAction(actorComp.PlayerSession, RateLimitKey) != RateLimitStatus.Allowed)
        {
            _currentlyProcessingLoadouts.TryRemove(msg.Actor, out _);
            return;
        }

        try
        {
            await ProcessDeleteMessage(uid, component, msg);
        }
        finally
        {
            _currentlyProcessingLoadouts.TryRemove(msg.Actor, out _);
        }
    }

    private async Task ProcessDeleteMessage(EntityUid uid, StalkerRepositoryComponent component, LoadoutDeleteMessage msg)
    {
        if (msg.Actor == null)
            return;

        var owner = GetOwner(component, msg.Actor);
        if (string.IsNullOrEmpty(owner))
            return;

        try
        {
            var container = await GetLoadoutsAsync(owner);

            if (!ValidateEntities(uid, msg.Actor, out var freshComponent))
                return;

            if (container == null)
                return;

            var loadout = container.Loadouts.FirstOrDefault(l => l.Id == msg.LoadoutId);
            if (loadout == null)
                return;

            container.Loadouts.Remove(loadout);
            await SetLoadoutsAsync(owner, container);

            if (!ValidateEntities(uid, msg.Actor, out freshComponent))
                return;

            _popup.PopupEntity(Loc.GetString("loadout-deleted"), msg.Actor, msg.Actor, PopupType.Small);
            await SendLoadoutStateUpdateAsync(uid, freshComponent, msg.Actor);
        }
        catch (Exception e)
        {
            _sawmill.Error($"Failed to delete loadout: {e}");
        }
    }

    private async void OnRenameMessage(EntityUid uid, StalkerRepositoryComponent component, LoadoutRenameMessage msg)
    {
        if (msg.Actor == null || !_currentlyProcessingLoadouts.TryAdd(msg.Actor, 0))
            return;

        if (TryComp<ActorComponent>(msg.Actor, out var actorComp) &&
            _rateLimitManager.CountAction(actorComp.PlayerSession, RateLimitKey) != RateLimitStatus.Allowed)
        {
            _currentlyProcessingLoadouts.TryRemove(msg.Actor, out _);
            return;
        }

        try
        {
            await ProcessRenameMessage(uid, component, msg);
        }
        finally
        {
            _currentlyProcessingLoadouts.TryRemove(msg.Actor, out _);
        }
    }

    private async Task ProcessRenameMessage(EntityUid uid, StalkerRepositoryComponent component, LoadoutRenameMessage msg)
    {
        if (msg.Actor == null)
            return;

        var owner = GetOwner(component, msg.Actor);
        if (string.IsNullOrEmpty(owner))
            return;

        var newName = SanitizeLoadoutName(msg.NewName);
        if (string.IsNullOrEmpty(newName))
            return;

        if (newName.Length > MaxLoadoutNameLength)
            newName = newName[..MaxLoadoutNameLength];

        try
        {
            var container = await GetLoadoutsAsync(owner);

            if (!ValidateEntities(uid, msg.Actor, out var freshComponent))
                return;

            if (container == null)
                return;

            var loadout = container.Loadouts.FirstOrDefault(l => l.Id == msg.LoadoutId);
            if (loadout == null)
                return;

            loadout.Name = newName;
            await SetLoadoutsAsync(owner, container);

            if (!ValidateEntities(uid, msg.Actor, out freshComponent))
                return;

            await SendLoadoutStateUpdateAsync(uid, freshComponent, msg.Actor);
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

    #endregion

    #region Save Logic

    /// <summary>
    /// Captures the current equipment of a player as a loadout.
    /// </summary>
    private PlayerLoadout? CaptureCurrentLoadout(EntityUid player, EntityUid repository, string name, int forcedId = -1)
    {
        TryComp<StalkerLoadoutComponent>(repository, out var loadoutComp);

        var loadout = new PlayerLoadout
        {
            Name = name,
            CreatedAt = DateTime.UtcNow
        };

        if (_inventory.TryGetContainerSlotEnumerator(player, out var enumerator))
        {
            while (enumerator.NextItem(out var item, out var slotDef))
            {
                if (IsBlacklistedSlot(slotDef.Name, loadoutComp))
                    continue;

                if (IsBlacklistedEntity(item, loadoutComp))
                    continue;

                var slotItem = CaptureSlotItem(item, slotDef.Name, loadoutComp);
                if (slotItem != null)
                    loadout.SlotItems.Add(slotItem);
            }
        }

        if (forcedId >= 0)
            loadout.Id = forcedId;

        return loadout;
    }

    private LoadoutSlotItem? CaptureSlotItem(EntityUid item, string slotName, StalkerLoadoutComponent? loadoutComp = null)
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

        if (TryComp<ContainerManagerComponent>(item, out var containerMan))
        {
            foreach (var container in containerMan.Containers)
            {
                if (IsBlacklistedContainer(container.Key, loadoutComp))
                    continue;

                foreach (var contained in container.Value.ContainedEntities)
                {
                    var nested = CaptureNestedItem(contained, container.Key, 1, item, loadoutComp);
                    if (nested != null)
                        slotItem.NestedItems.Add(nested);
                }
            }
        }

        return slotItem;
    }

    private LoadoutNestedItem? CaptureNestedItem(EntityUid item, string containerName, int depth,
        EntityUid? parentEntity = null, StalkerLoadoutComponent? loadoutComp = null)
    {
        if (depth > MaxRecursionDepth)
        {
            _sawmill.Warning($"MaxRecursionDepth exceeded when capturing {MetaData(item).EntityPrototype?.ID ?? "unknown"} at depth {depth}");
            return null;
        }

        if (IsBlacklistedEntity(item, loadoutComp))
            return null;

        // Magazine's AmmoContainerStalker already tracks ammo count
        if (parentEntity != null &&
            HasComp<BallisticAmmoProviderComponent>(parentEntity.Value) &&
            _tags.HasTag(item, "Cartridge"))
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

        if (parentEntity != null &&
            TryComp<StorageComponent>(parentEntity.Value, out var parentStorage) &&
            parentStorage.StoredItems.TryGetValue(item, out var itemLocation))
        {
            nestedItem.StorageLocation = itemLocation;
        }

        if (TryComp<ContainerManagerComponent>(item, out var containerMan))
        {
            foreach (var container in containerMan.Containers)
            {
                if (IsBlacklistedContainer(container.Key, loadoutComp))
                    continue;

                foreach (var contained in container.Value.ContainedEntities)
                {
                    var nested = CaptureNestedItem(contained, container.Key, depth + 1, item, loadoutComp);
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
    /// Moves already-equipped items directly instead of cycling through stash to prevent identifier mismatch issues.
    /// </summary>
    private LoadResult ApplyLoadout(EntityUid player, Entity<StalkerRepositoryComponent> repository, PlayerLoadout loadout)
    {
        var missingCount = 0;
        TryComp<StalkerLoadoutComponent>(repository, out var loadoutComp);

        var loadoutPrototypes = new HashSet<string>();
        foreach (var slotItem in loadout.SlotItems)
        {
            loadoutPrototypes.Add(slotItem.PrototypeId);
            CollectNestedPrototypes(slotItem.NestedItems, loadoutPrototypes);
        }

        var equippedBySlot = new Dictionary<string, (EntityUid item, string proto)>();
        var equippedByProto = new Dictionary<string, List<(EntityUid item, string slot)>>();

        if (_inventory.TryGetContainerSlotEnumerator(player, out var enumerator))
        {
            while (enumerator.NextItem(out var item, out var slotDef))
            {
                if (IsBlacklistedSlot(slotDef.Name, loadoutComp))
                    continue;

                var proto = MetaData(item).EntityPrototype?.ID;
                if (proto == null)
                    continue;

                equippedBySlot[slotDef.Name] = (item, proto);

                if (!equippedByProto.TryGetValue(proto, out var protoList))
                {
                    protoList = new List<(EntityUid, string)>();
                    equippedByProto[proto] = protoList;
                }
                protoList.Add((item, slotDef.Name));
            }
        }

        var itemsToUnequip = new List<(EntityUid item, string slot)>();
        var itemsToMove = new HashSet<EntityUid>();
        var consumedItems = new HashSet<EntityUid>();
        foreach (var slotItem in loadout.SlotItems)
        {
            if (equippedBySlot.TryGetValue(slotItem.SlotName, out var equipped) &&
                equipped.proto == slotItem.PrototypeId)
            {
                consumedItems.Add(equipped.item);
            }
        }

        foreach (var slotItem in loadout.SlotItems)
        {
            if (equippedBySlot.TryGetValue(slotItem.SlotName, out var currentEquipped) &&
                currentEquipped.proto == slotItem.PrototypeId)
                continue;

            if (equippedByProto.TryGetValue(slotItem.PrototypeId, out var availableItems))
            {
                foreach (var (availItem, _) in availableItems)
                {
                    if (!consumedItems.Contains(availItem))
                    {
                        itemsToMove.Add(availItem);
                        consumedItems.Add(availItem);
                        break;
                    }
                }
            }
        }

        foreach (var (slot, (item, proto)) in equippedBySlot)
        {
            if (consumedItems.Contains(item))
                continue;

            // Unremovable chips, implants, etc. must stay equipped
            if (IsBlacklistedEntity(item, loadoutComp))
                continue;

            itemsToUnequip.Add((item, slot));
        }

        foreach (var (item, slot) in itemsToUnequip)
        {
            if (_inventory.TryUnequip(player, slot, out var unequipped, true, true))
            {
                if (!_repositorySystem.InsertEquippedItem(player, repository, unequipped.Value))
                {
                    var itemName = MetaData(unequipped.Value).EntityName;
                    _popup.PopupEntity(Loc.GetString("loadout-item-dropped", ("item", itemName)), player, player, PopupType.SmallCaution);
                    _sawmill.Warning($"Could not insert {ToPrettyString(unequipped.Value)} to stash - dropped near player");
                }
                equippedBySlot.Remove(slot);
            }
        }

        var stashLookup = BuildStashLookup(repository.Comp.ContainedItems);

        // Tracks items already matched to loadout entries, preventing duplicates from matching again
        var consumedExistingItems = new HashSet<EntityUid>();

        foreach (var slotItem in loadout.SlotItems)
        {
            if (equippedBySlot.TryGetValue(slotItem.SlotName, out var slotOccupant) &&
                IsBlacklistedEntity(slotOccupant.item, loadoutComp))
                continue;

            if (equippedBySlot.TryGetValue(slotItem.SlotName, out var currentEquipped) &&
                currentEquipped.proto == slotItem.PrototypeId)
            {
                // Compare identifiers to prevent duplication - substitute items don't get nested items restored
                var equippedStorageData = _stalkerStorage.ConvertToIItemStalkerStorage(currentEquipped.item);
                var equippedIdentifier = equippedStorageData.Count > 0 && equippedStorageData[0] is IItemStalkerStorage iss
                    ? iss.Identifier()
                    : "";

                if (equippedIdentifier == slotItem.Identifier)
                {
                    if (slotItem.NestedItems.Count > 0)
                        RestoreNestedItems(currentEquipped.item, slotItem.NestedItems, repository, stashLookup, 0, player, consumedExistingItems);
                }
                continue;
            }

            var movedItem = TryMoveEquippedItemToSlot(player, slotItem, equippedByProto, equippedBySlot, itemsToMove, loadoutComp);
            if (movedItem != null)
            {
                if (slotItem.NestedItems.Count > 0)
                    RestoreNestedItems(movedItem.Value, slotItem.NestedItems, repository, stashLookup, 0, player, consumedExistingItems);
                continue;
            }

            if (!TryEquipSlotItem(player, repository, slotItem, stashLookup, consumedExistingItems))
                missingCount++;
        }

        return new LoadResult(true, missingCount);
    }

    /// <summary>
    /// Attempts to move an already-equipped item to the target slot.
    /// Avoids cycling items through stash to prevent identifier mismatch issues.
    /// </summary>
    /// <returns>The moved entity if successful, null otherwise.</returns>
    private EntityUid? TryMoveEquippedItemToSlot(
        EntityUid player,
        LoadoutSlotItem slotItem,
        Dictionary<string, List<(EntityUid item, string slot)>> equippedByProto,
        Dictionary<string, (EntityUid item, string proto)> equippedBySlot,
        HashSet<EntityUid> itemsToMove,
        StalkerLoadoutComponent? loadoutComp)
    {
        if (!equippedByProto.TryGetValue(slotItem.PrototypeId, out var availableItems))
            return null;

        foreach (var (item, sourceSlot) in availableItems)
        {
            if (!itemsToMove.Contains(item))
                continue;

            itemsToMove.Remove(item);

            if (equippedBySlot.TryGetValue(slotItem.SlotName, out var targetOccupant))
            {
                _sawmill.Warning($"Target slot {slotItem.SlotName} still occupied, cannot move");
                continue;
            }

            if (!_inventory.TryUnequip(player, sourceSlot, out var unequipped, true, true))
            {
                _sawmill.Warning($"Failed to unequip {slotItem.PrototypeId} from {sourceSlot} for move");
                continue;
            }

            if (!_inventory.TryEquip(player, unequipped.Value, slotItem.SlotName, true, true))
            {
                _sawmill.Warning($"Failed to equip {slotItem.PrototypeId} to {slotItem.SlotName} after move");
                if (!_inventory.TryEquip(player, unequipped.Value, sourceSlot, true, true))
                {
                    if (!_hands.TryPickup(player, unequipped.Value))
                    {
                        Transform(unequipped.Value).Coordinates = Transform(player).Coordinates;
                        _sawmill.Warning($"Failed to return item to hands - dropped near player");
                    }
                }
                continue;
            }

            equippedBySlot.Remove(sourceSlot);
            equippedBySlot[slotItem.SlotName] = (unequipped.Value, slotItem.PrototypeId);
            availableItems.RemoveAll(x => x.item == item);

            return unequipped.Value;
        }

        return null;
    }

    /// <summary>
    /// Recursively collects all prototype IDs from nested items into the provided set.
    /// </summary>
    private void CollectNestedPrototypes(List<LoadoutNestedItem> nestedItems, HashSet<string> prototypes)
    {
        foreach (var nested in nestedItems)
        {
            prototypes.Add(nested.PrototypeId);
            CollectNestedPrototypes(nested.NestedItems, prototypes);
        }
    }

    private bool TryEquipSlotItem(
        EntityUid player,
        Entity<StalkerRepositoryComponent> repository,
        LoadoutSlotItem slotItem,
        StashLookup stashLookup,
        HashSet<EntityUid> consumedExistingItems)
    {
        var stashItem = FindItemInStash(slotItem.Identifier, slotItem.PrototypeId, stashLookup);
        if (stashItem == null)
            return false;

        var xform = Transform(player);
        var spawned = Spawn(stashItem.ProductEntity, xform.Coordinates);

        if (stashItem.SStorageData is IItemStalkerStorage iss)
            _stalkerStorage.SpawnedItem(spawned, iss);

        // Equip before removing from stash - preserves item state if equip fails
        if (!_inventory.TryEquip(player, spawned, slotItem.SlotName, true, true))
        {
            if (!_hands.TryPickup(player, spawned))
            {
                QueueDel(spawned);
                return false;
            }
        }

        RemoveFromStash(repository, stashItem, stashLookup);

        if (slotItem.NestedItems.Count > 0)
        {
            ClearAutoFilledContents(spawned);
            RestoreNestedItems(spawned, slotItem.NestedItems, repository, stashLookup, 0, player, consumedExistingItems);
        }

        return true;
    }

    private void RestoreNestedItems(
        EntityUid parent,
        List<LoadoutNestedItem> nestedItems,
        Entity<StalkerRepositoryComponent> repository,
        StashLookup stashLookup,
        int depth,
        EntityUid player,
        HashSet<EntityUid> consumedExistingItems)
    {
        if (depth > MaxRecursionDepth)
        {
            _sawmill.Warning($"MaxRecursionDepth exceeded when restoring nested items in {ToPrettyString(parent)} at depth {depth}");
            return;
        }

        if (!Exists(parent))
        {
            _sawmill.Warning($"Parent entity {parent} no longer exists during nested item restoration");
            return;
        }

        if (!TryComp<ContainerManagerComponent>(parent, out var containerMan))
            return;

        TryComp<ItemSlotsComponent>(parent, out var itemSlotsComp);

        // Sort by grid size (largest first) to reduce fragmentation in grid-based storage
        var sortedNestedItems = nestedItems
            .OrderByDescending(item => GetItemGridSize(item.PrototypeId))
            .ToList();

        foreach (var nestedItem in sortedNestedItems)
        {
            var existingCorrectItem = FindExistingCorrectItem(parent, nestedItem, containerMan, itemSlotsComp, consumedExistingItems);
            if (existingCorrectItem.HasValue)
            {
                consumedExistingItems.Add(existingCorrectItem.Value);

                if (nestedItem.NestedItems.Count > 0)
                    RestoreNestedItems(existingCorrectItem.Value, nestedItem.NestedItems, repository, stashLookup, depth + 1, player, consumedExistingItems);
                continue;
            }

            var stashItem = FindItemInStash(nestedItem.Identifier, nestedItem.PrototypeId, stashLookup);
            if (stashItem == null)
            {
                _sawmill.Warning($"Nested item not found in stash: {nestedItem.PrototypeId} (identifier: {nestedItem.Identifier}) for container '{nestedItem.ContainerName}' on parent {ToPrettyString(parent)}");
                continue;
            }

            BaseContainer? container = null;
            ItemSlot? itemSlot = null;
            string? foundSlotId = null;

            // ItemSlots first (gun_magazine, gun_chamber, etc.) - allows whitelist/blacklist validation
            if (itemSlotsComp != null && _itemSlots.TryGetSlot(parent, nestedItem.ContainerName, out var slot) && slot.ContainerSlot != null)
            {
                container = slot.ContainerSlot;
                itemSlot = slot;
                foundSlotId = nestedItem.ContainerName;
            }

            if (container == null)
                containerMan.Containers.TryGetValue(nestedItem.ContainerName, out container);

            if (container == null)
            {
                foreach (var fallback in ContainerFallbacks)
                {
                    if (itemSlotsComp != null && _itemSlots.TryGetSlot(parent, fallback, out var fallbackSlot) && fallbackSlot.ContainerSlot != null)
                    {
                        container = fallbackSlot.ContainerSlot;
                        itemSlot = fallbackSlot;
                        foundSlotId = fallback;
                        break;
                    }
                    if (containerMan.Containers.TryGetValue(fallback, out var fallbackContainer))
                    {
                        container = fallbackContainer;
                        break;
                    }
                }
            }

            if (container == null)
            {
                var lowerName = nestedItem.ContainerName.ToLower();
                if (lowerName != nestedItem.ContainerName)
                {
                    if (itemSlotsComp != null && _itemSlots.TryGetSlot(parent, lowerName, out var lowerSlot) && lowerSlot.ContainerSlot != null)
                    {
                        container = lowerSlot.ContainerSlot;
                        itemSlot = lowerSlot;
                        foundSlotId = lowerName;
                    }
                    else
                    {
                        containerMan.Containers.TryGetValue(lowerName, out container);
                    }
                }
            }

            if (container == null)
            {
                _sawmill.Warning($"Container not found for nested item {nestedItem.PrototypeId}: tried '{nestedItem.ContainerName}' and fallbacks on parent {ToPrettyString(parent)}");
                continue;
            }

            if (itemSlot != null && itemSlot.Locked)
                continue;

            EntityUid? displacedItem = null;
            if (container is ContainerSlot containerSlot && containerSlot.ContainedEntity is { } existing)
                displacedItem = existing;

            var xform = Transform(parent);
            var spawned = Spawn(stashItem.ProductEntity, xform.Coordinates);

            if (stashItem.SStorageData is IItemStalkerStorage iss)
                _stalkerStorage.SpawnedItem(spawned, iss);

            // Clear auto-filled contents BEFORE inserting into parent storage to prevent
            // StorageFill contents from leaking into parent's StoredItems dictionary
            if (nestedItem.NestedItems.Count > 0)
                ClearAutoFilledContents(spawned);

            bool inserted;
            if (itemSlot != null && foundSlotId != null)
            {
                if (displacedItem.HasValue)
                {
                    _container.Remove(displacedItem.Value, container, reparent: false, force: true);
                }

                inserted = _itemSlots.TryInsert(parent, foundSlotId, spawned, null, itemSlotsComp);

                if (!inserted && displacedItem.HasValue)
                {
                    if (!_container.Insert(displacedItem.Value, container))
                    {
                        if (!_repositorySystem.InsertEquippedItem(player, repository, displacedItem.Value))
                        {
                            Transform(displacedItem.Value).Coordinates = Transform(player).Coordinates;
                            _sawmill.Warning($"Could not restore existing item to container or stash - dropped near player");
                        }
                    }
                    displacedItem = null;
                }
            }
            else if (nestedItem.StorageLocation.HasValue &&
                     TryComp<StorageComponent>(parent, out var parentStorageComp))
            {
                if (displacedItem.HasValue)
                {
                    _container.Remove(displacedItem.Value, container, reparent: false, force: true);
                }

                inserted = _storage.InsertAt((parent, parentStorageComp), spawned, nestedItem.StorageLocation.Value, out _, player, playSound: false);

                if (!inserted)
                    inserted = _storage.Insert(parent, spawned, out _, player, parentStorageComp, playSound: false);

                if (!inserted && displacedItem.HasValue)
                {
                    if (!_container.Insert(displacedItem.Value, container))
                    {
                        if (!_repositorySystem.InsertEquippedItem(player, repository, displacedItem.Value))
                        {
                            Transform(displacedItem.Value).Coordinates = Transform(player).Coordinates;
                            _sawmill.Warning($"Could not restore existing item to container or stash - dropped near player");
                        }
                    }
                    displacedItem = null;
                }
            }
            else
            {
                if (displacedItem.HasValue)
                    _container.Remove(displacedItem.Value, container, reparent: false, force: true);

                inserted = TryInsertAtPosition(parent, spawned, nestedItem);

                if (!inserted && displacedItem.HasValue)
                {
                    if (!_container.Insert(displacedItem.Value, container))
                    {
                        if (!_repositorySystem.InsertEquippedItem(player, repository, displacedItem.Value))
                        {
                            Transform(displacedItem.Value).Coordinates = Transform(player).Coordinates;
                            _sawmill.Warning($"Could not restore existing item to container or stash - dropped near player");
                        }
                    }
                    displacedItem = null;
                }
            }

            if (!inserted)
            {
                QueueDel(spawned);
                continue;
            }

            RemoveFromStash(repository, stashItem, stashLookup);

            consumedExistingItems.Add(spawned);

            if (displacedItem.HasValue)
            {
                var displacedName = MetaData(displacedItem.Value).EntityName;
                if (!_repositorySystem.InsertEquippedItem(player, repository, displacedItem.Value))
                {
                    Transform(displacedItem.Value).Coordinates = Transform(player).Coordinates;
                    _popup.PopupEntity(Loc.GetString("loadout-item-dropped", ("item", displacedName)), player, player, PopupType.SmallCaution);
                    _sawmill.Warning($"Dropped displaced item {displacedName} near player - stash full");
                }
            }
            if (nestedItem.NestedItems.Count > 0 && Exists(spawned))
            {
                RestoreNestedItems(spawned, nestedItem.NestedItems, repository, stashLookup, depth + 1, player, consumedExistingItems);
            }
        }
    }

    /// <summary>
    /// Attempts to insert an item into a StorageComponent at its saved grid position.
    /// Falls back to auto-placement if position is unavailable.
    /// </summary>
    private bool TryInsertAtPosition(EntityUid storage, EntityUid item, LoadoutNestedItem nestedItem)
    {
        if (!TryComp<StorageComponent>(storage, out var storageComp))
        {
            if (TryComp<ContainerManagerComponent>(storage, out var containerMan) &&
                containerMan.Containers.TryGetValue(nestedItem.ContainerName, out var container))
            {
                return _container.Insert(item, container);
            }
            return false;
        }

        var savedLocation = nestedItem.StorageLocation;
        if (savedLocation != null)
        {
            if (_storage.ItemFitsInGridLocation(
                (item, null),
                (storage, storageComp),
                savedLocation.Value))
            {
                if (_storage.InsertAt(
                    (storage, storageComp),
                    (item, null),
                    savedLocation.Value,
                    out _,
                    playSound: false))
                {
                    return true;
                }
            }
        }

        return _storage.Insert(storage, item, out _, storageComp: storageComp, playSound: false);
    }

    /// <summary>
    /// Gets the grid footprint (cell count) for an item prototype.
    /// </summary>
    private int GetItemGridSize(string prototypeId)
    {
        if (!_prototypeManager.TryIndex<EntityPrototype>(prototypeId, out var proto))
            return 1;

        if (!proto.TryGetComponent<ItemComponent>(out var itemComp, EntityManager.ComponentFactory))
            return 1;

        var shape = _itemSystem.GetItemShape(itemComp);
        return shape.GetArea();
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Tracks stash items by identifier and prototype for efficient lookup.
    /// </summary>
    private sealed class StashLookup
    {
        public Dictionary<string, List<RepositoryItemInfo>> ByIdentifier { get; } = new();
        public Dictionary<string, List<RepositoryItemInfo>> ByPrototype { get; } = new();
    }

    private StashLookup BuildStashLookup(List<RepositoryItemInfo> items)
    {
        var lookup = new StashLookup();
        foreach (var item in items)
        {
            if (!lookup.ByIdentifier.TryGetValue(item.Identifier, out var identList))
            {
                identList = new List<RepositoryItemInfo>();
                lookup.ByIdentifier[item.Identifier] = identList;
            }
            identList.Add(item);

            var protoKey = item.ProductEntity;
            if (!lookup.ByPrototype.TryGetValue(protoKey, out var protoList))
            {
                protoList = new List<RepositoryItemInfo>();
                lookup.ByPrototype[protoKey] = protoList;
            }
            protoList.Add(item);
        }
        return lookup;
    }

    /// <summary>
    /// Checks if the correct item already exists at the target location.
    /// Prevents unnecessary stash pulls when items are already in position.
    /// </summary>
    /// <param name="consumedItems">Items already matched to other loadout entries.</param>
    /// <returns>The existing item if found, null otherwise.</returns>
    private EntityUid? FindExistingCorrectItem(
        EntityUid parent,
        LoadoutNestedItem nestedItem,
        ContainerManagerComponent containerMan,
        ItemSlotsComponent? itemSlotsComp,
        HashSet<EntityUid> consumedItems)
    {
        if (nestedItem.StorageLocation.HasValue &&
            TryComp<StorageComponent>(parent, out var storageComp))
        {
            foreach (var (item, location) in storageComp.StoredItems)
            {
                if (consumedItems.Contains(item))
                    continue;

                if (location.Position == nestedItem.StorageLocation.Value.Position &&
                    TryComp<MetaDataComponent>(item, out var meta) &&
                    meta.EntityPrototype?.ID == nestedItem.PrototypeId)
                {
                    return item;
                }
            }
        }

        if (itemSlotsComp != null &&
            _itemSlots.TryGetSlot(parent, nestedItem.ContainerName, out var slot) &&
            slot.ContainerSlot?.ContainedEntity is { } slotItem &&
            !consumedItems.Contains(slotItem) &&
            TryComp<MetaDataComponent>(slotItem, out var slotMeta) &&
            slotMeta.EntityPrototype?.ID == nestedItem.PrototypeId)
        {
            return slotItem;
        }

        if (containerMan.Containers.TryGetValue(nestedItem.ContainerName, out var container))
        {
            foreach (var contained in container.ContainedEntities)
            {
                if (consumedItems.Contains(contained))
                    continue;

                if (TryComp<MetaDataComponent>(contained, out var containedMeta) &&
                    containedMeta.EntityPrototype?.ID == nestedItem.PrototypeId)
                {
                    return contained;
                }
            }
        }

        return null;
    }

    private RepositoryItemInfo? FindItemInStash(string identifier, string prototypeId, StashLookup lookup)
    {
        if (lookup.ByIdentifier.TryGetValue(identifier, out var identList))
        {
            for (var i = identList.Count - 1; i >= 0; i--)
            {
                if (identList[i].Count > 0)
                    return identList[i];
            }
        }

        // Fallback handles state-based identifier mismatches (stack counts, ammo counts, charges)
        if (lookup.ByPrototype.TryGetValue(prototypeId, out var protoList))
        {
            for (var i = protoList.Count - 1; i >= 0; i--)
            {
                if (protoList[i].Count > 0)
                    return protoList[i];
            }
        }

        return null;
    }

    private void RemoveFromStash(
        Entity<StalkerRepositoryComponent> repository,
        RepositoryItemInfo item,
        StashLookup lookup)
    {
        // Update weight before decrementing count to prevent weight inflation on DB reload
        repository.Comp.CurrentWeight -= item.Weight;

        if (item.SStorageData is IItemStalkerStorage stalker)
        {
            if (stalker.CountVendingMachine > 0)
                stalker.CountVendingMachine--;
        }

        item.Count--;

        if (item.Count <= 0)
        {
            repository.Comp.ContainedItems.Remove(item);
            if (lookup.ByIdentifier.TryGetValue(item.Identifier, out var identList))
                identList.Remove(item);
            if (lookup.ByPrototype.TryGetValue(item.ProductEntity, out var protoList))
                protoList.Remove(item);
        }
    }

    private bool IsBlacklistedContainer(string containerName, StalkerLoadoutComponent? loadoutComp = null)
    {
        if (loadoutComp?.ContainerBlacklist != null)
            return loadoutComp.ContainerBlacklist.Contains(containerName);
        return DefaultContainerBlacklist.Contains(containerName);
    }

    private bool IsBlacklistedSlot(string slotName, StalkerLoadoutComponent? loadoutComp = null)
    {
        if (loadoutComp?.SlotBlacklist != null)
            return loadoutComp.SlotBlacklist.Contains(slotName);

        return DefaultSlotBlacklist.Contains(slotName);
    }

    private bool IsBlacklistedEntity(EntityUid item, StalkerLoadoutComponent? loadoutComp = null)
    {
        if (loadoutComp?.EntityBlacklist != null)
            return _whitelistSystem.IsWhitelistPass(loadoutComp.EntityBlacklist, item);

        return HasComp<Content.Shared.Body.Organ.OrganComponent>(item) ||
               HasComp<Content.Shared.Actions.InstantActionComponent>(item) ||
               HasComp<Content.Shared.Actions.WorldTargetActionComponent>(item) ||
               HasComp<Content.Shared.Actions.EntityTargetActionComponent>(item) ||
               HasComp<Content.Shared.Implants.Components.SubdermalImplantComponent>(item) ||
               HasComp<Content.Shared.Body.Part.BodyPartComponent>(item) ||
               HasComp<Content.Shared.Inventory.VirtualItem.VirtualItemComponent>(item) ||
               HasComp<Content.Shared.Mind.Components.MindContainerComponent>(item) ||
               HasComp<Content.Shared.Chemistry.Components.SolutionComponent>(item) ||
               HasComp<Content.Shared.Interaction.Components.UnremoveableComponent>(item) ||
               HasComp<Content.Shared.Clothing.Components.SelfUnremovableClothingComponent>(item);
    }

    /// <summary>
    /// Clears auto-filled storage contents to restore exact loadout contents instead.
    /// Removes from container before QueueDel because queued entities still occupy slots.
    /// </summary>
    private void ClearAutoFilledContents(EntityUid item)
    {
        if (TryComp<StorageComponent>(item, out var storageComp))
        {
            foreach (var contained in storageComp.Container.ContainedEntities.ToList())
            {
                _container.Remove(contained, storageComp.Container, force: true);
                QueueDel(contained);
            }
            storageComp.StoredItems.Clear();
        }

        if (TryComp<ItemSlotsComponent>(item, out var slotsComp))
        {
            foreach (var slot in slotsComp.Slots.Values)
            {
                if (slot.ContainerSlot?.ContainedEntity is { } slotEntity)
                {
                    _container.Remove(slotEntity, slot.ContainerSlot, force: true);
                    QueueDel(slotEntity);
                }
            }
        }
    }

    /// <summary>
    /// Sanitizes a loadout name by removing control characters, zero-width unicode,
    /// and collapsing multiple whitespace characters.
    /// </summary>
    private static string SanitizeLoadoutName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var sb = new StringBuilder(name.Length);
        var lastWasSpace = true;

        foreach (var c in name)
        {
            if (char.IsControl(c))
                continue;

            var category = char.GetUnicodeCategory(c);
            if (category == UnicodeCategory.Format)
                continue;

            if (char.IsWhiteSpace(c))
            {
                if (!lastWasSpace)
                {
                    sb.Append(' ');
                    lastWasSpace = true;
                }
                continue;
            }

            sb.Append(c);
            lastWasSpace = false;
        }

        return sb.ToString().TrimEnd();
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

            if (!Exists(uid) || !TryComp<StalkerRepositoryComponent>(uid, out var freshComponent))
                return;

            var loadouts = container?.Loadouts ?? new List<PlayerLoadout>();

            var equippedPrototypes = BuildEquippedPrototypesLookup(actor);

            var stashLookup = BuildStashLookup(freshComponent.ContainedItems);
            foreach (var loadout in loadouts)
            {
                CalculateMissingItems(loadout, stashLookup, equippedPrototypes);
            }

            loadouts = loadouts.OrderBy(l => l.Id != 0).ThenBy(l => l.Name).ToList();

            _ui.SetUiState(uid, StalkerRepositoryUiKey.Key, new LoadoutUpdateState(loadouts));
        }
        catch (Exception e)
        {
            _sawmill.Error($"Failed to update loadout UI state: {e}");
        }
    }

    /// <summary>
    /// Builds a lookup of currently equipped item prototypes (with counts) for the player.
    /// </summary>
    private Dictionary<string, int> BuildEquippedPrototypesLookup(EntityUid? player)
    {
        var equipped = new Dictionary<string, int>();
        if (!player.HasValue)
            return equipped;

        if (_inventory.TryGetContainerSlotEnumerator(player.Value, out var enumerator))
        {
            while (enumerator.NextItem(out var item, out _))
            {
                CollectItemAndNestedPrototypes(item, equipped);
            }
        }

        return equipped;
    }

    /// <summary>
    /// Recursively collects the prototype of an item and all its nested contents.
    /// </summary>
    private void CollectItemAndNestedPrototypes(EntityUid item, Dictionary<string, int> lookup)
    {
        var proto = MetaData(item).EntityPrototype?.ID;
        if (proto != null)
        {
            lookup.TryGetValue(proto, out var count);
            lookup[proto] = count + 1;
        }

        if (TryComp<ContainerManagerComponent>(item, out var containerMan))
        {
            foreach (var container in containerMan.Containers)
            {
                foreach (var contained in container.Value.ContainedEntities)
                {
                    CollectItemAndNestedPrototypes(contained, lookup);
                }
            }
        }
    }

    /// <summary>
    /// Calculates missing items for a loadout by checking against equipped items and stash.
    /// </summary>
    private void CalculateMissingItems(PlayerLoadout loadout, StashLookup stashLookup, Dictionary<string, int> equippedPrototypes)
    {
        var tempLookup = CloneLookup(stashLookup);
        var tempEquipped = new Dictionary<string, int>(equippedPrototypes);
        var missingItems = new List<MissingLoadoutItem>();

        foreach (var slotItem in loadout.SlotItems)
        {
            var parentMissing = !TryConsumeFromEquippedOrStash(slotItem.PrototypeId, tempEquipped, slotItem.Identifier, tempLookup);

            if (parentMissing)
            {
                var missing = new MissingLoadoutItem
                {
                    Name = GetPrototypeName(slotItem.PrototypeId),
                    Location = slotItem.SlotName,
                    Count = 1
                };

                CollectMissingNestedItems(slotItem.NestedItems, tempLookup, tempEquipped, missing.Children);
                missingItems.Add(missing);
            }
            else
            {
                CollectMissingNestedItems(slotItem.NestedItems, tempLookup, tempEquipped, missingItems);
            }
        }

        loadout.MissingItems = GroupMissingItems(missingItems);
        loadout.MissingCount = CountAllMissing(loadout.MissingItems);
    }

    /// <summary>
    /// Tries to consume an item from equipped items first, then from stash.
    /// </summary>
    /// <returns>True if the item was found (not missing), false if missing.</returns>
    private bool TryConsumeFromEquippedOrStash(string prototypeId, Dictionary<string, int> equipped, string identifier, StashLookup stashLookup)
    {
        if (equipped.TryGetValue(prototypeId, out var count) && count > 0)
        {
            equipped[prototypeId] = count - 1;
            return true;
        }

        return TryConsumeFromLookup(identifier, prototypeId, stashLookup);
    }

    /// <summary>
    /// Recursively checks nested items against the stash lookup and equipped items.
    /// </summary>
    private void CollectMissingNestedItems(
        List<LoadoutNestedItem> items,
        StashLookup lookup,
        Dictionary<string, int> equipped,
        List<MissingLoadoutItem> target)
    {
        foreach (var item in items)
        {
            var itemMissing = !TryConsumeFromEquippedOrStash(item.PrototypeId, equipped, item.Identifier, lookup);

            if (itemMissing)
            {
                var missing = new MissingLoadoutItem
                {
                    Name = GetPrototypeName(item.PrototypeId),
                    Location = item.ContainerName,
                    Count = 1
                };

                CollectMissingNestedItems(item.NestedItems, lookup, equipped, missing.Children);
                target.Add(missing);
            }
            else
            {
                CollectMissingNestedItems(item.NestedItems, lookup, equipped, target);
            }
        }
    }

    /// <summary>
    /// Groups items by name, merging duplicates and preserving hierarchy.
    /// </summary>
    private List<MissingLoadoutItem> GroupMissingItems(List<MissingLoadoutItem> items)
    {
        var groups = new Dictionary<string, MissingLoadoutItem>();

        foreach (var item in items)
        {
            if (groups.TryGetValue(item.Name, out var existing))
            {
                existing.Count++;
                foreach (var child in item.Children)
                    existing.Children.Add(child);
                existing.Children = GroupMissingItems(existing.Children);
            }
            else
            {
                groups[item.Name] = new MissingLoadoutItem
                {
                    Name = item.Name,
                    Location = item.Location,
                    Count = item.Count,
                    Children = GroupMissingItems(item.Children)
                };
            }
        }

        return groups.Values.ToList();
    }

    private int CountAllMissing(List<MissingLoadoutItem> items)
    {
        return items.Sum(m => m.Count + CountAllMissing(m.Children));
    }

    private StashLookup CloneLookup(StashLookup original)
    {
        var clone = new StashLookup();

        foreach (var kvp in original.ByIdentifier)
        {
            var clonedList = new List<RepositoryItemInfo>();
            foreach (var item in kvp.Value)
            {
                var clonedItem = new RepositoryItemInfo
                {
                    Count = item.Count,
                    ProductEntity = item.ProductEntity,
                    Identifier = item.Identifier
                };
                clonedList.Add(clonedItem);

                if (!clone.ByPrototype.TryGetValue(clonedItem.ProductEntity, out var protoList))
                {
                    protoList = new List<RepositoryItemInfo>();
                    clone.ByPrototype[clonedItem.ProductEntity] = protoList;
                }
                protoList.Add(clonedItem);
            }
            clone.ByIdentifier[kvp.Key] = clonedList;
        }

        return clone;
    }

    private string GetPrototypeName(string prototypeId)
    {
        if (_prototypeManager.TryIndex<EntityPrototype>(prototypeId, out var proto))
            return proto.Name;
        return prototypeId;
    }

    private bool TryConsumeFromLookup(string identifier, string prototypeId, StashLookup lookup)
    {
        if (lookup.ByIdentifier.TryGetValue(identifier, out var identList))
        {
            foreach (var item in identList)
            {
                if (item.Count > 0)
                {
                    item.Count--;
                    return true;
                }
            }
        }

        if (lookup.ByPrototype.TryGetValue(prototypeId, out var protoList))
        {
            foreach (var protoItem in protoList)
            {
                if (protoItem.Count > 0)
                {
                    protoItem.Count--;
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Sends the loadout state update to the UI.
    /// </summary>
    public void SendLoadoutStateUpdate(EntityUid uid, StalkerRepositoryComponent component, EntityUid? actor = null)
    {
        _ = SendLoadoutStateUpdateAsync(uid, component, actor).ContinueWith(task =>
        {
            if (task.Exception != null)
                _sawmill.Error($"SendLoadoutStateUpdateAsync failed: {task.Exception}");
        }, TaskContinuationOptions.OnlyOnFaulted);
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
