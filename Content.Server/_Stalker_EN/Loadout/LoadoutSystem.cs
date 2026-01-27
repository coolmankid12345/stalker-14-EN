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

    private ISawmill _sawmill = default!;

    private const string RateLimitKey = "Loadout";

    // Concurrent operation protection - prevents same actor from running multiple loadout operations simultaneously
    // Uses ConcurrentDictionary for thread safety during async operations
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

    // Gun-specific containers first, then general storage as fallback
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

    // Default blacklists (used when component is missing or field is null)
    private static readonly HashSet<string> DefaultSlotBlacklist = new() { "id" };
    private static readonly HashSet<string> DefaultContainerBlacklist = new() { "toggleable-clothing", "actions" };

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = Logger.GetSawmill("loadout");

        // Register rate limiter for loadout operations (save, load, delete, rename)
        _rateLimitManager.Register(RateLimitKey, new RateLimitRegistration
        {
            CVarLimitPeriodLength = STCCVars.LoadoutRateLimitPeriod,
            CVarLimitCount = STCCVars.LoadoutRateLimitCount,
            PlayerLimitedAction = session =>
                _popup.PopupEntity(Loc.GetString("loadout-rate-limited"), session.AttachedEntity!.Value, session.AttachedEntity!.Value, PopupType.SmallCaution),
            AdminLogType = LogType.Action
        });

        SubscribeLocalEvent<StalkerRepositoryComponent, LoadoutSaveMessage>(OnSaveMessage);
        SubscribeLocalEvent<StalkerRepositoryComponent, LoadoutLoadMessage>(OnLoadMessage);
        SubscribeLocalEvent<StalkerRepositoryComponent, LoadoutDeleteMessage>(OnDeleteMessage);
        SubscribeLocalEvent<StalkerRepositoryComponent, LoadoutRenameMessage>(OnRenameMessage);
        SubscribeLocalEvent<StalkerRepositoryComponent, LoadoutRequestMessage>(OnRequestMessage);
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
        if (msg.Actor == null || !_currentlyProcessingLoadouts.TryAdd(msg.Actor, 0))
            return;

        // Rate limit check
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

        // Check loadout count limit for named loadouts (Quick Save doesn't count)
        if (!msg.IsQuickSave)
        {
            var existingContainer = await GetLoadoutsAsync(owner);

            // Validate entities after async operation
            if (!ValidateEntities(uid, msg.Actor, out _))
                return;

            var namedCount = existingContainer?.Loadouts.Count(l => l.Id != 0) ?? 0;
            if (namedCount >= MaxNamedLoadouts)
            {
                _popup.PopupEntity(Loc.GetString("loadout-limit-reached", ("max", MaxNamedLoadouts)), msg.Actor, msg.Actor, PopupType.SmallCaution);
                return;
            }
        }

        // Capture loadout metadata (items stay equipped - use Quick Store to move items to stash)
        var loadout = CaptureCurrentLoadout(msg.Actor, uid, name, msg.IsQuickSave ? 0 : -1);
        if (loadout == null || loadout.SlotItems.Count == 0)
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
        if (msg.Actor == null || !_currentlyProcessingLoadouts.TryAdd(msg.Actor, 0))
            return;

        // Rate limit check
        if (TryComp<ActorComponent>(msg.Actor, out var actorComp) &&
            _rateLimitManager.CountAction(actorComp.PlayerSession, RateLimitKey) != RateLimitStatus.Allowed)
        {
            _currentlyProcessingLoadouts.TryRemove(msg.Actor, out _);
            return;
        }

        // Set flag to prevent race conditions with manual stash operations during async load
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

            // CRITICAL: Validate entities still exist after async DB operation
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

            // Use fresh component reference after validation
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
        if (msg.Actor == null || !_currentlyProcessingLoadouts.TryAdd(msg.Actor, 0))
            return;

        // Rate limit check
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
        if (msg.Actor == null || !_currentlyProcessingLoadouts.TryAdd(msg.Actor, 0))
            return;

        // Rate limit check
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

        // Capture inventory slots
        if (_inventory.TryGetContainerSlotEnumerator(player, out var enumerator))
        {
            while (enumerator.NextItem(out var item, out var slotDef))
            {
                if (IsBlacklistedSlot(slotDef.Name, loadoutComp))
                    continue;

                // Skip blacklisted entities (unremovable items, organs, etc.)
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

        // Capture nested items in containers
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

        // Skip cartridges inside magazines (same as repository system)
        // The magazine's AmmoContainerStalker already tracks ammo count
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

        // Capture storage position if parent has grid-based storage
        if (parentEntity != null &&
            TryComp<StorageComponent>(parentEntity.Value, out var parentStorage) &&
            parentStorage.StoredItems.TryGetValue(item, out var itemLocation))
        {
            nestedItem.StorageLocation = itemLocation;
        }

        // Recursively capture nested items
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
    /// Uses smart equipment handling to avoid cycling items through stash unnecessarily.
    /// Key optimizations:
    /// - Items already in correct slot: SKIP (no action needed)
    /// - Items in wrong slot but needed: MOVE directly (no stash cycle)
    /// - Items not equipped but needed: Pull from stash
    /// This prevents item duplication and loss from stash identifier mismatches.
    /// </summary>
    private LoadResult ApplyLoadout(EntityUid player, Entity<StalkerRepositoryComponent> repository, PlayerLoadout loadout)
    {
        var missingCount = 0;
        TryComp<StalkerLoadoutComponent>(repository, out var loadoutComp);

        // Build set of prototypes the loadout needs (including nested items)
        var loadoutPrototypes = new HashSet<string>();
        foreach (var slotItem in loadout.SlotItems)
        {
            loadoutPrototypes.Add(slotItem.PrototypeId);
            CollectNestedPrototypes(slotItem.NestedItems, loadoutPrototypes);
        }

        // Build maps of currently equipped items
        // equippedBySlot: slot -> (item, prototype)
        // equippedByProto: prototype -> list of (item, slot) for items that CAN be moved
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

                // Add to prototype lookup for potential moves
                if (!equippedByProto.TryGetValue(proto, out var protoList))
                {
                    protoList = new List<(EntityUid, string)>();
                    equippedByProto[proto] = protoList;
                }
                protoList.Add((item, slotDef.Name));
            }
        }

        // Determine which items to unequip (not needed by loadout) vs move (needed in different slot)
        // Items that match a loadout prototype will be MOVED, not unequipped to stash
        var itemsToUnequip = new List<(EntityUid item, string slot)>();
        var itemsToMove = new HashSet<EntityUid>(); // Track items that will be moved to a different slot

        // First pass: identify items that are already in the correct slot (perfect matches)
        // and mark them as "consumed" so they won't be moved
        var consumedItems = new HashSet<EntityUid>();
        foreach (var slotItem in loadout.SlotItems)
        {
            if (equippedBySlot.TryGetValue(slotItem.SlotName, out var equipped) &&
                equipped.proto == slotItem.PrototypeId)
            {
                consumedItems.Add(equipped.item);
            }
        }

        // Second pass: for slots that need different items, check if we can move from another slot
        foreach (var slotItem in loadout.SlotItems)
        {
            if (equippedBySlot.TryGetValue(slotItem.SlotName, out var currentEquipped) &&
                currentEquipped.proto == slotItem.PrototypeId)
                continue;

            // Need a different item in this slot - check if we have one equipped elsewhere
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

        // Identify items to unequip: not consumed AND not blacklisted
        foreach (var (slot, (item, proto)) in equippedBySlot)
        {
            if (consumedItems.Contains(item))
                continue;

            // CRITICAL: Never unequip blacklisted items (unremovable chips, implants, etc.)
            if (IsBlacklistedEntity(item, loadoutComp))
                continue;

            itemsToUnequip.Add((item, slot));
        }

        // Unequip items not needed by loadout (to stash)
        foreach (var (item, slot) in itemsToUnequip)
        {
            if (_inventory.TryUnequip(player, slot, out var unequipped, true, true))
            {
                if (!_repositorySystem.InsertEquippedItem(player, repository, unequipped.Value))
                {
                    // Item could not be stored (weight limit or whitelist) - notify user
                    var itemName = MetaData(unequipped.Value).EntityName;
                    _popup.PopupEntity(Loc.GetString("loadout-item-dropped", ("item", itemName)), player, player, PopupType.SmallCaution);
                    _sawmill.Warning($"Could not insert {ToPrettyString(unequipped.Value)} to stash - dropped near player");
                }
                equippedBySlot.Remove(slot);
            }
        }

        // Build stash lookup for items that need to be pulled
        var stashLookup = BuildStashLookup(repository.Comp.ContainedItems);

        // Process each loadout slot
        foreach (var slotItem in loadout.SlotItems)
        {
            // Case 0: Check if slot is blocked by an unremovable item (e.g., Monolith chip)
            if (equippedBySlot.TryGetValue(slotItem.SlotName, out var slotOccupant) &&
                IsBlacklistedEntity(slotOccupant.item, loadoutComp))
                continue;

            // Case 1: Check if this slot already has the correct item (perfect match)
            if (equippedBySlot.TryGetValue(slotItem.SlotName, out var currentEquipped) &&
                currentEquipped.proto == slotItem.PrototypeId)
            {
                // SECURITY: Compare identifiers to prevent duplication exploit
                // If identifiers don't match, this is a substitute item - don't restore nested items
                var equippedStorageData = _stalkerStorage.ConvertToIItemStalkerStorage(currentEquipped.item);
                var equippedIdentifier = equippedStorageData.Count > 0 && equippedStorageData[0] is IItemStalkerStorage iss
                    ? iss.Identifier()
                    : "";

                if (equippedIdentifier == slotItem.Identifier)
                {
                    // Same item - restore nested items
                    if (slotItem.NestedItems.Count > 0)
                        RestoreNestedItems(currentEquipped.item, slotItem.NestedItems, repository, stashLookup, 0, player);
                }
                else
                {
                    // Different item (substitute) - nested items stay in stash
                    _sawmill.Debug($"Smart equip: identifier mismatch for {slotItem.PrototypeId} - nested items remain in stash");
                }
                continue;
            }

            // Case 2: Check if we can MOVE an equipped item to this slot
            var movedItem = TryMoveEquippedItemToSlot(player, slotItem, equippedByProto, equippedBySlot, itemsToMove, loadoutComp);
            if (movedItem != null)
            {
                if (slotItem.NestedItems.Count > 0)
                    RestoreNestedItems(movedItem.Value, slotItem.NestedItems, repository, stashLookup, 0, player);
                continue;
            }

            // Case 3: Need to pull from stash
            if (!TryEquipSlotItem(player, repository, slotItem, stashLookup))
                missingCount++;
        }

        return new LoadResult(true, missingCount);
    }

    /// <summary>
    /// Attempts to move an already-equipped item to the target slot.
    /// Returns the moved entity if successful, null otherwise.
    /// This avoids cycling items through the stash, preventing identifier mismatch issues.
    /// </summary>
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

        // Find an item marked for moving
        foreach (var (item, sourceSlot) in availableItems)
        {
            if (!itemsToMove.Contains(item))
                continue;

            // Remove from itemsToMove so we don't try to move it twice
            itemsToMove.Remove(item);

            // First, clear the target slot if occupied
            if (equippedBySlot.TryGetValue(slotItem.SlotName, out var targetOccupant))
            {
                // Target slot has something - it should have been unequipped already
                // but if not, we can't move here
                _sawmill.Warning($"Target slot {slotItem.SlotName} still occupied, cannot move");
                continue;
            }

            // Unequip from source slot
            if (!_inventory.TryUnequip(player, sourceSlot, out var unequipped, true, true))
            {
                _sawmill.Warning($"Failed to unequip {slotItem.PrototypeId} from {sourceSlot} for move");
                continue;
            }

            // Equip to target slot
            if (!_inventory.TryEquip(player, unequipped.Value, slotItem.SlotName, true, true))
            {
                _sawmill.Warning($"Failed to equip {slotItem.PrototypeId} to {slotItem.SlotName} after move");
                // Try to put it back
                if (!_inventory.TryEquip(player, unequipped.Value, sourceSlot, true, true))
                {
                    // Couldn't put back - try hands
                    _hands.TryPickup(player, unequipped.Value);
                }
                continue;
            }

            // Success! Update tracking
            equippedBySlot.Remove(sourceSlot);
            equippedBySlot[slotItem.SlotName] = (unequipped.Value, slotItem.PrototypeId);
            // Also remove from the available items list
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
        StashLookup stashLookup)
    {
        // Find the item in stash
        var stashItem = FindItemInStash(slotItem.Identifier, slotItem.PrototypeId, stashLookup);
        if (stashItem == null)
            return false;

        // Remove item from stash (decrements count)
        RemoveFromStash(repository, stashItem, stashLookup);

        // Spawn the item
        var xform = Transform(player);
        var spawned = Spawn(stashItem.ProductEntity, xform.Coordinates);

        // Restore item state
        if (stashItem.SStorageData is IItemStalkerStorage iss)
        {
            _stalkerStorage.SpawnedItem(spawned, iss);
        }

        // Clear auto-filled contents if we have nested items to restore
        // StorageFill auto-populates on spawn, but we want exact loadout contents
        if (slotItem.NestedItems.Count > 0)
        {
            var clearedCount = 0;

            // Clear Storage containers (backpacks, cigarette packs, etc.)
            if (TryComp<StorageComponent>(spawned, out var storageComp))
            {
                clearedCount = storageComp.Container.ContainedEntities.Count;
                foreach (var contained in storageComp.Container.ContainedEntities.ToList())
                {
                    QueueDel(contained);
                }
                storageComp.StoredItems.Clear();
            }

            // Clear ItemSlots containers (pill boxes, etc.)
            if (TryComp<ItemSlotsComponent>(spawned, out var slotsComp))
            {
                foreach (var slot in slotsComp.Slots.Values)
                {
                    if (slot.ContainerSlot?.ContainedEntity is { } slotEntity)
                    {
                        _container.Remove(slotEntity, slot.ContainerSlot, force: true);
                        QueueDel(slotEntity);
                        clearedCount++;
                    }
                }
            }

        }

        // Equip item
        if (!_inventory.TryEquip(player, spawned, slotItem.SlotName, true, true))
        {
            // Put item in hands or drop it
            if (!_hands.TryPickup(player, spawned))
            {
                ReturnSpawnedItemToStash(spawned, repository, stashLookup, player, slotItem.PrototypeId);
                return false;
            }
        }

        // Restore nested items
        if (slotItem.NestedItems.Count > 0)
            RestoreNestedItems(spawned, slotItem.NestedItems, repository, stashLookup, 0, player);

        return true;
    }

    private void RestoreNestedItems(
        EntityUid parent,
        List<LoadoutNestedItem> nestedItems,
        Entity<StalkerRepositoryComponent> repository,
        StashLookup stashLookup,
        int depth,
        EntityUid player)
    {
        if (depth > MaxRecursionDepth)
        {
            _sawmill.Warning($"MaxRecursionDepth exceeded when restoring nested items in {ToPrettyString(parent)} at depth {depth}");
            return;
        }

        if (!TryComp<ContainerManagerComponent>(parent, out var containerMan))
            return;

        // Check for ItemSlotsComponent (used by guns for magazine/chamber slots)
        TryComp<ItemSlotsComponent>(parent, out var itemSlotsComp);

        foreach (var nestedItem in nestedItems)
        {
            // Check if correct item already exists at target location
            // This prevents unnecessary stash pulls when items are already in place
            var existingCorrectItem = FindExistingCorrectItem(parent, nestedItem, containerMan, itemSlotsComp);
            if (existingCorrectItem.HasValue)
            {
                // Item already in place - just restore its nested items
                if (nestedItem.NestedItems.Count > 0)
                    RestoreNestedItems(existingCorrectItem.Value, nestedItem.NestedItems, repository, stashLookup, depth + 1, player);
                continue;
            }

            // Find the item in stash
            var stashItem = FindItemInStash(nestedItem.Identifier, nestedItem.PrototypeId, stashLookup);
            if (stashItem == null)
                continue;

            // Find container and ItemSlot (if applicable) - try multiple methods
            BaseContainer? container = null;
            ItemSlot? itemSlot = null;
            string? foundSlotId = null;

            // Method 1: Try ItemSlots first (for gun_magazine, gun_chamber, etc.)
            // This is preferred because it allows proper whitelist/blacklist validation
            if (itemSlotsComp != null && _itemSlots.TryGetSlot(parent, nestedItem.ContainerName, out var slot) && slot.ContainerSlot != null)
            {
                container = slot.ContainerSlot;
                itemSlot = slot;
                foundSlotId = nestedItem.ContainerName;
            }

            // Method 2: Direct container lookup (if not an ItemSlot)
            if (container == null)
            {
                containerMan.Containers.TryGetValue(nestedItem.ContainerName, out container);
            }

            // Method 3: Try fallbacks
            if (container == null)
            {
                foreach (var fallback in ContainerFallbacks)
                {
                    // Try ItemSlots for fallbacks first
                    if (itemSlotsComp != null && _itemSlots.TryGetSlot(parent, fallback, out var fallbackSlot) && fallbackSlot.ContainerSlot != null)
                    {
                        container = fallbackSlot.ContainerSlot;
                        itemSlot = fallbackSlot;
                        foundSlotId = fallback;
                        break;
                    }
                    // Then try regular containers
                    if (containerMan.Containers.TryGetValue(fallback, out var fallbackContainer))
                    {
                        container = fallbackContainer;
                        break;
                    }
                }
            }

            // Method 4: Lowercase fallback
            if (container == null)
            {
                var lowerName = nestedItem.ContainerName.ToLower();
                if (lowerName != nestedItem.ContainerName)
                {
                    // Try ItemSlot first
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
                continue;

            // Track existing item for potential removal (but don't remove yet!)
            EntityUid? existingItem = null;
            if (container is ContainerSlot containerSlot && containerSlot.ContainedEntity is { } existing)
            {
                existingItem = existing;
            }

            // Remove item from stash AFTER confirming container exists
            RemoveFromStash(repository, stashItem, stashLookup);

            // Spawn the item
            var xform = Transform(parent);
            var spawned = Spawn(stashItem.ProductEntity, xform.Coordinates);

            // Restore item state
            if (stashItem.SStorageData is IItemStalkerStorage iss)
            {
                _stalkerStorage.SpawnedItem(spawned, iss);
            }

            // Clear auto-filled contents for nested items with children
            // StorageFill auto-populates on spawn, but we want exact loadout contents
            if (nestedItem.NestedItems.Count > 0)
            {
                // Clear Storage containers (backpacks, cigarette packs, etc.)
                if (TryComp<StorageComponent>(spawned, out var nestedStorage))
                {
                    foreach (var contained in nestedStorage.Container.ContainedEntities.ToList())
                    {
                        QueueDel(contained);
                    }
                    nestedStorage.StoredItems.Clear();
                }

                // Clear ItemSlots containers (pill boxes, etc.)
                if (TryComp<ItemSlotsComponent>(spawned, out var nestedSlotsComp))
                {
                    foreach (var nestedSlot in nestedSlotsComp.Slots.Values)
                    {
                        if (nestedSlot.ContainerSlot?.ContainedEntity is { } slotEntity)
                        {
                            _container.Remove(slotEntity, nestedSlot.ContainerSlot, force: true);
                            QueueDel(slotEntity);
                        }
                    }
                }
            }

            // Insert into container - use ItemSlots validation if available
            bool inserted;
            if (itemSlot != null && foundSlotId != null)
            {
                // Use ItemSlotsSystem.TryInsert which validates whitelist/blacklist/locked status
                // First, temporarily remove existing item if present (required for TryInsert to work)
                if (existingItem.HasValue)
                {
                    _container.Remove(existingItem.Value, container, reparent: false, force: true);
                }

                inserted = _itemSlots.TryInsert(parent, foundSlotId, spawned, null, itemSlotsComp);

                if (!inserted && existingItem.HasValue)
                {
                    // Restore the existing item since insertion failed
                    _container.Insert(existingItem.Value, container);
                    existingItem = null; // Don't delete it
                }
            }
            else if (nestedItem.StorageLocation.HasValue &&
                     TryComp<StorageComponent>(parent, out var parentStorageComp))
            {
                // Grid-based storage with saved position
                if (existingItem.HasValue)
                {
                    _container.Remove(existingItem.Value, container, reparent: false, force: true);
                }

                // Try position-based insertion first
                inserted = _storage.InsertAt((parent, parentStorageComp), spawned, nestedItem.StorageLocation.Value, out _, player, playSound: false);

                // Fallback to auto-placement if position fails (e.g., position conflict)
                if (!inserted)
                    inserted = _storage.Insert(parent, spawned, out _, player, parentStorageComp, playSound: false);

                if (!inserted && existingItem.HasValue)
                {
                    // Restore the existing item since insertion failed
                    _container.Insert(existingItem.Value, container);
                    existingItem = null; // Don't delete it
                }
            }
            else
            {
                // For non-ItemSlot containers, remove existing first then insert (auto-place)
                if (existingItem.HasValue)
                {
                    _container.Remove(existingItem.Value, container, reparent: false, force: true);
                }

                // Try position-aware insertion for StorageComponent containers (holsters, etc.)
                inserted = TryInsertAtPosition(parent, spawned, nestedItem);

                if (!inserted && existingItem.HasValue)
                {
                    // Restore the existing item since insertion failed
                    _container.Insert(existingItem.Value, container);
                    existingItem = null; // Don't delete it
                }
            }

            if (!inserted)
            {
                ReturnSpawnedItemToStash(spawned, repository, stashLookup, player, nestedItem.PrototypeId);
                continue;
            }

            // Return displaced item to stash instead of deleting it
            if (existingItem.HasValue)
            {
                var displacedName = MetaData(existingItem.Value).EntityName;
                if (!_repositorySystem.InsertEquippedItem(player, repository, existingItem.Value))
                {
                    // Stash full - drop near player (never delete)
                    Transform(existingItem.Value).Coordinates = Transform(player).Coordinates;
                    _popup.PopupEntity(Loc.GetString("loadout-item-dropped", ("item", displacedName)), player, player, PopupType.SmallCaution);
                    _sawmill.Warning($"Dropped displaced item {displacedName} near player - stash full");
                }
                else
                {
                    _sawmill.Debug($"Returned displaced item {displacedName} to stash");
                }
            }

            // Recursively restore nested items
            if (nestedItem.NestedItems.Count > 0)
                RestoreNestedItems(spawned, nestedItem.NestedItems, repository, stashLookup, depth + 1, player);
        }
    }

    /// <summary>
    /// Returns a spawned item back to the stash when insertion fails.
    /// Falls back to dropping near player if stash insertion also fails.
    /// </summary>
    private void ReturnSpawnedItemToStash(
        EntityUid spawned,
        Entity<StalkerRepositoryComponent> repository,
        StashLookup stashLookup,
        EntityUid player,
        string itemName)
    {
        if (!_repositorySystem.InsertEquippedItem(player, repository, spawned))
        {
            // Last resort: drop near player (never delete)
            var xform = Transform(player);
            Transform(spawned).Coordinates = xform.Coordinates;
        }
    }

    /// <summary>
    /// Attempts to insert an item into a StorageComponent at its saved grid position.
    /// Falls back to auto-placement if position is unavailable or occupied.
    /// For non-StorageComponent containers, uses standard container insertion.
    /// </summary>
    private bool TryInsertAtPosition(EntityUid storage, EntityUid item, LoadoutNestedItem nestedItem)
    {
        // Check if storage has StorageComponent for grid-based insertion
        if (!TryComp<StorageComponent>(storage, out var storageComp))
        {
            // Not a grid storage - use standard container insertion
            if (TryComp<ContainerManagerComponent>(storage, out var containerMan) &&
                containerMan.Containers.TryGetValue(nestedItem.ContainerName, out var container))
            {
                return _container.Insert(item, container);
            }
            return false;
        }

        // Try position-aware insertion if we have saved coordinates
        var savedLocation = nestedItem.StorageLocation;
        if (savedLocation != null)
        {
            // Check if item fits at saved position
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
                    _sawmill.Debug($"Inserted {nestedItem.PrototypeId} at saved position ({savedLocation.Value.Position.X}, {savedLocation.Value.Position.Y})");
                    return true;
                }
            }
            _sawmill.Debug($"Could not insert {nestedItem.PrototypeId} at saved position - falling back to auto-place");
        }

        // Fallback to auto-placement
        return _storage.Insert(storage, item, out _, storageComp: storageComp, playSound: false);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Lookup structure that tracks items by identifier and prototype.
    /// Supports multiple items per prototype (common after equipment cycling).
    /// </summary>
    private sealed class StashLookup
    {
        // Changed: Store list of items per identifier (not just first match)
        public Dictionary<string, List<RepositoryItemInfo>> ByIdentifier { get; } = new();
        public Dictionary<string, List<RepositoryItemInfo>> ByPrototype { get; } = new();
    }

    private StashLookup BuildStashLookup(List<RepositoryItemInfo> items)
    {
        var lookup = new StashLookup();
        foreach (var item in items)
        {
            // Store ALL items per identifier (not just first)
            if (!lookup.ByIdentifier.TryGetValue(item.Identifier, out var identList))
            {
                identList = new List<RepositoryItemInfo>();
                lookup.ByIdentifier[item.Identifier] = identList;
            }
            identList.Add(item);

            // Fallback lookup by prototype - store ALL items per prototype
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
    /// Returns the existing item if found, null otherwise.
    /// This prevents unnecessary stash pulls when items are already in position.
    /// </summary>
    private EntityUid? FindExistingCorrectItem(
        EntityUid parent,
        LoadoutNestedItem nestedItem,
        ContainerManagerComponent containerMan,
        ItemSlotsComponent? itemSlotsComp)
    {
        // Case 1: Grid-based storage with saved position
        if (nestedItem.StorageLocation.HasValue &&
            TryComp<StorageComponent>(parent, out var storageComp))
        {
            foreach (var (item, location) in storageComp.StoredItems)
            {
                if (location.Position == nestedItem.StorageLocation.Value.Position &&
                    TryComp<MetaDataComponent>(item, out var meta) &&
                    meta.EntityPrototype?.ID == nestedItem.PrototypeId)
                {
                    return item;
                }
            }
        }

        // Case 2: ItemSlots (magazines, chambers, etc.)
        if (itemSlotsComp != null &&
            _itemSlots.TryGetSlot(parent, nestedItem.ContainerName, out var slot) &&
            slot.ContainerSlot?.ContainedEntity is { } slotItem &&
            TryComp<MetaDataComponent>(slotItem, out var slotMeta) &&
            slotMeta.EntityPrototype?.ID == nestedItem.PrototypeId)
        {
            return slotItem;
        }

        // Case 3: Regular containers (check if same prototype exists in container)
        if (containerMan.Containers.TryGetValue(nestedItem.ContainerName, out var container))
        {
            foreach (var contained in container.ContainedEntities)
            {
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
        // Try exact identifier match first - iterate through list
        if (lookup.ByIdentifier.TryGetValue(identifier, out var identList))
        {
            for (var i = identList.Count - 1; i >= 0; i--)
            {
                if (identList[i].Count > 0)
                    return identList[i];
            }
        }

        // Fallback to prototype match - return ANY item with count > 0
        // This handles state-based identifier mismatches (stack counts, ammo counts, charges)
        // Prefer items at the end of the list (most recently added)
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
        // CRITICAL: Update weight and count BEFORE decrementing item count
        // Fixes permanent weight inflation and item duplication on DB reload
        repository.Comp.CurrentWeight -= item.Weight;

        // Update CountVendingMachine in the storage data
        if (item.SStorageData is IItemStalkerStorage stalker)
        {
            if (stalker.CountVendingMachine > 0)
                stalker.CountVendingMachine--;
        }

        item.Count--;

        if (item.Count <= 0)
        {
            repository.Comp.ContainedItems.Remove(item);
            // Remove from identifier list
            if (lookup.ByIdentifier.TryGetValue(item.Identifier, out var identList))
            {
                identList.Remove(item);
            }
            // Remove from prototype list
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
        // Use custom blacklist if configured
        if (loadoutComp?.EntityBlacklist != null)
            return _whitelistSystem.IsWhitelistPass(loadoutComp.EntityBlacklist, item);

        // Default component checks
        // Note: CartridgeComponent intentionally NOT blacklisted - we want to track bullets in loadouts
        return HasComp<Content.Shared.Body.Organ.OrganComponent>(item) ||
               HasComp<Content.Shared.Actions.InstantActionComponent>(item) ||
               HasComp<Content.Shared.Actions.WorldTargetActionComponent>(item) ||
               HasComp<Content.Shared.Actions.EntityTargetActionComponent>(item) ||
               HasComp<Content.Shared.Implants.Components.SubdermalImplantComponent>(item) ||
               HasComp<Content.Shared.Body.Part.BodyPartComponent>(item) ||
               HasComp<Content.Shared.Inventory.VirtualItem.VirtualItemComponent>(item) ||
               HasComp<Content.Shared.Mind.Components.MindContainerComponent>(item) ||
               HasComp<Content.Shared.Chemistry.Components.SolutionComponent>(item) ||
               // Unremovable components
               HasComp<Content.Shared.Interaction.Components.UnremoveableComponent>(item) ||
               HasComp<Content.Shared.Clothing.Components.SelfUnremovableClothingComponent>(item);
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
            // Skip control characters
            if (char.IsControl(c))
                continue;

            // Skip format characters (zero-width joiners, marks, etc.)
            var category = char.GetUnicodeCategory(c);
            if (category == UnicodeCategory.Format)
                continue;

            // Collapse whitespace
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
            var loadouts = container?.Loadouts ?? new List<PlayerLoadout>();

            // Build equipped prototypes lookup (items are not "missing" if currently worn)
            var equippedPrototypes = BuildEquippedPrototypesLookup(actor);

            // Calculate missing items for each loadout
            var stashLookup = BuildStashLookup(component.ContainedItems);
            foreach (var loadout in loadouts)
            {
                CalculateMissingItems(loadout, stashLookup, equippedPrototypes);
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

    /// <summary>
    /// Builds a lookup of currently equipped item prototypes (with counts) for the player.
    /// Used to determine which loadout items are already satisfied by worn equipment.
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

        // Recursively check containers
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
    /// Calculates missing items for a loadout by checking each item against equipped items and stash.
    /// Note: Nested items are stored as separate stash entries, not physically inside parents.
    /// Each item (parent or nested) must be checked individually.
    /// Items are first checked against equipped items (on player), then against stash.
    /// </summary>
    private void CalculateMissingItems(PlayerLoadout loadout, StashLookup stashLookup, Dictionary<string, int> equippedPrototypes)
    {
        var tempLookup = CloneLookup(stashLookup);
        var tempEquipped = new Dictionary<string, int>(equippedPrototypes); // Clone to allow consumption
        var missingItems = new List<MissingLoadoutItem>();

        foreach (var slotItem in loadout.SlotItems)
        {
            var parentMissing = !TryConsumeFromEquippedOrStash(slotItem.PrototypeId, tempEquipped, slotItem.Identifier, tempLookup);

            if (parentMissing)
            {
                // Parent is missing - add it, then check which nested items are also missing
                var missing = new MissingLoadoutItem
                {
                    Name = GetPrototypeName(slotItem.PrototypeId),
                    Location = slotItem.SlotName,
                    Count = 1
                };

                // Only add nested items as children if they're ALSO missing from stash/equipped
                CollectMissingNestedItems(slotItem.NestedItems, tempLookup, tempEquipped, missing.Children);
                missingItems.Add(missing);
            }
            else
            {
                // Parent found - but nested items are stored separately, check them too
                CollectMissingNestedItems(slotItem.NestedItems, tempLookup, tempEquipped, missingItems);
            }
        }

        loadout.MissingItems = GroupMissingItems(missingItems);
        loadout.MissingCount = CountAllMissing(loadout.MissingItems);
    }

    /// <summary>
    /// Tries to consume an item from equipped items first, then from stash.
    /// Returns true if the item was found (not missing), false if missing.
    /// </summary>
    private bool TryConsumeFromEquippedOrStash(string prototypeId, Dictionary<string, int> equipped, string identifier, StashLookup stashLookup)
    {
        // First check equipped items (by prototype only)
        if (equipped.TryGetValue(prototypeId, out var count) && count > 0)
        {
            equipped[prototypeId] = count - 1;
            return true;
        }

        // Then check stash (by identifier first, then prototype)
        return TryConsumeFromLookup(identifier, prototypeId, stashLookup);
    }

    /// <summary>
    /// Recursively checks nested items against the stash lookup and equipped items.
    /// Only adds items that are actually missing from both sources.
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
                // This nested item is missing from stash and equipped
                var missing = new MissingLoadoutItem
                {
                    Name = GetPrototypeName(item.PrototypeId),
                    Location = item.ContainerName,
                    Count = 1
                };

                // Recursively check its children (only add if also missing)
                CollectMissingNestedItems(item.NestedItems, lookup, equipped, missing.Children);
                target.Add(missing);
            }
            else
            {
                // Item found - still check its nested items
                CollectMissingNestedItems(item.NestedItems, lookup, equipped, target);
            }
        }
    }

    /// <summary>
    /// Groups items by name, preserving hierarchy.
    /// Items with the same name at the same level are merged with increased count.
    /// Children are recursively grouped as well.
    /// </summary>
    private List<MissingLoadoutItem> GroupMissingItems(List<MissingLoadoutItem> items)
    {
        var groups = new Dictionary<string, MissingLoadoutItem>();

        foreach (var item in items)
        {
            if (groups.TryGetValue(item.Name, out var existing))
            {
                existing.Count++;
                // Merge children recursively
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

    /// <summary>
    /// Counts total missing items including nested children.
    /// </summary>
    private int CountAllMissing(List<MissingLoadoutItem> items)
    {
        return items.Sum(m => m.Count + CountAllMissing(m.Children));
    }

    /// <summary>
    /// Creates a deep clone of the stash lookup for consumption tracking.
    /// </summary>
    private StashLookup CloneLookup(StashLookup original)
    {
        var clone = new StashLookup();

        // Clone identifier lookup (now stores lists)
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

                // Also add to prototype lookup
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
        // Try exact match first - iterate through list
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

        // Fallback to prototype match - find any item with count > 0
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
