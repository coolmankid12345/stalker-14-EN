using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Tasks;
using Content.Server._Stalker.StalkerDB;
using Content.Server._Stalker.Storage;
using Content.Server.Database;
using Content.Server.GameTicking.Rules;
using Content.Server.Popups;
using Content.Shared._Stalker.StalkerRepository;
using Content.Shared._Stalker.Storage;
using Content.Shared._Stalker_EN.CCVar;
using Content.Shared._Stalker_EN.CrashRecovery;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Clothing.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Implants.Components;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Mind.Components;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew.Components;
using Content.Shared.Actions.Components;
using Content.Shared.Interaction.Components;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Player;

namespace Content.Server._Stalker_EN.CrashRecovery;

public sealed class CrashRecoverySystem : GameRuleSystem<CrashRecoveryRuleComponent>
{
    [Dependency] private readonly IServerDbManager _dbManager = default!;
    [Dependency] private readonly StalkerStorageSystem _stalkerStorage = default!;
    [Dependency] private readonly StalkerDbSystem _stalkerDb = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    private ISawmill _sawmill = default!;

    private bool _enabled = true;
    private float _saveInterval = 300f;
    private float _timeSinceLastSave;

    // Staggering: queue of players to snapshot this cycle
    private readonly Queue<ICommonSession> _snapshotQueue = new();
    private int _playersPerTick = 1;

    // Dirty tracking: players whose equipment changed since last snapshot
    private readonly HashSet<EntityUid> _dirtyPlayers = new();

    // Concurrent operation protection for claims
    private readonly ConcurrentDictionary<EntityUid, byte> _currentlyProcessingClaims = new();

    // Logins with unclaimed recovery data — snapshots must not overwrite these
    private readonly HashSet<string> _pendingRecoveryLogins = new();

    private readonly ConcurrentBag<Task> _pendingDbWrites = new();

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = Logger.GetSawmill("crash-recovery");

        _cfg.OnValueChanged(STCCVars.CrashRecoveryEnabled, v => _enabled = v, true);
        _cfg.OnValueChanged(STCCVars.CrashRecoverySaveInterval, v => _saveInterval = v, true);

        // BUI message for crash recovery claim
        SubscribeLocalEvent<StalkerRepositoryComponent, CrashRecoveryClaimMessage>(OnCrashRecoveryClaim);

        // Dirty tracking: mark players when equipment changes
        SubscribeLocalEvent<InventoryComponent, DidEquipEvent>(OnEquipChanged);
        SubscribeLocalEvent<InventoryComponent, DidUnequipEvent>(OnUnequipChanged);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        // Safety net; EntryPoint.Shutdown drained earlier, but writes may have occurred since.
        FlushPendingWrites(TimeSpan.FromSeconds(5));

        _cfg.UnsubValueChanged(STCCVars.CrashRecoveryEnabled, v => _enabled = v);
        _cfg.UnsubValueChanged(STCCVars.CrashRecoverySaveInterval, v => _saveInterval = v);
    }

    /// <summary>
    /// Call BEFORE EntryPoint.Dispose so the Npgsql pool isn't saturated when it runs ClearAllCrashRecovery.
    /// </summary>
    public bool FlushPendingWrites(TimeSpan timeout)
    {
        var pending = _pendingDbWrites.ToArray();
        if (pending.Length == 0)
        {
            _sawmill.Info("[shutdown] CrashRecoverySystem: no pending writes");
            return true;
        }

        _sawmill.Info($"[shutdown] CrashRecoverySystem flushing {pending.Length} pending write(s)");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var completed = false;
        try
        {
            completed = Task.WhenAll(pending).Wait(timeout);
        }
        catch (Exception e)
        {
            _sawmill.Error($"[shutdown] CrashRecoverySystem error flushing writes: {e}");
        }
        sw.Stop();
        _sawmill.Info($"[shutdown] CrashRecoverySystem flush {(completed ? "completed" : "TIMED OUT")} after {sw.ElapsedMilliseconds}ms");
        return completed;
    }

    private void TrackDbWrite(Func<Task> dbCall, string label)
    {
        _pendingDbWrites.Add(Task.Run(async () =>
        {
            try
            {
                await dbCall();
            }
            catch (Exception e)
            {
                _sawmill.Error($"{label}: {e}");
            }
        }));
    }

    #region Game Rule Lifecycle

    protected override void Started(EntityUid uid, CrashRecoveryRuleComponent component,
        GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);
        _timeSinceLastSave = 0;
        _snapshotQueue.Clear();
        _dirtyPlayers.Clear();
        _pendingRecoveryLogins.Clear();
        _sawmill.Info("Crash recovery game rule started");

        // Load logins with unclaimed recovery data so periodic snapshots don't overwrite them
        LoadPendingRecoveryLogins();
    }

    private async void LoadPendingRecoveryLogins()
    {
        try
        {
            var logins = await _dbManager.GetAllCrashRecoveryLogins();
            foreach (var login in logins)
                _pendingRecoveryLogins.Add(login);

            if (logins.Count > 0)
                _sawmill.Info($"Loaded {logins.Count} pending crash recovery logins");
        }
        catch (Exception e)
        {
            _sawmill.Error($"Failed to load pending crash recovery logins: {e}");
        }
    }

    protected override void Ended(EntityUid uid, CrashRecoveryRuleComponent component,
        GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        base.Ended(uid, component, gameRule, args);

        if (!_enabled)
            return;

        _sawmill.Info("Clearing all crash recovery data (round ended)");
        TrackDbWrite(() => _dbManager.ClearAllCrashRecovery(), "ClearAllCrashRecovery(round-end)");
        _snapshotQueue.Clear();
        _dirtyPlayers.Clear();
        _pendingRecoveryLogins.Clear();
        _timeSinceLastSave = 0;
    }

    protected override void ActiveTick(EntityUid uid, CrashRecoveryRuleComponent component,
        GameRuleComponent gameRule, float frameTime)
    {
        if (!_enabled || _saveInterval <= 0)
            return;

        ProcessSnapshotQueue();

        _timeSinceLastSave += frameTime;
        if (_timeSinceLastSave < _saveInterval)
            return;

        _timeSinceLastSave = 0;
        StartSnapshotCycle();
    }

    #endregion

    #region Dirty Tracking

    private void OnEquipChanged(EntityUid uid, InventoryComponent component, DidEquipEvent args)
    {
        _dirtyPlayers.Add(uid);
    }

    private void OnUnequipChanged(EntityUid uid, InventoryComponent component, DidUnequipEvent args)
    {
        _dirtyPlayers.Add(uid);
    }

    #endregion

    #region Crash Recovery Check (BUI Open)

    /// <summary>
    /// Called from StalkerRepositorySystem.OnBeforeActivate when stash UI opens.
    /// Async check — state arrives on next tick, after RepositoryUpdateState.
    /// </summary>
    public async void CheckAndSendCrashRecoveryState(EntityUid repository, EntityUid actor)
    {
        if (!_enabled)
            return;

        if (!TryComp<ActorComponent>(actor, out var actorComp))
            return;

        // Don't show banner if a claim is already being processed
        if (_currentlyProcessingClaims.ContainsKey(actor))
            return;

        var login = actorComp.PlayerSession.Name;

        // Only show recovery for data that existed at round start (from a crash).
        // Data written during the current session by periodic/immediate snapshots is not recoverable.
        if (!_pendingRecoveryLogins.Contains(login))
            return;

        try
        {
            var json = await _dbManager.GetCrashRecovery(login);

            if (!Exists(repository) || !Exists(actor))
                return;

            if (string.IsNullOrEmpty(json))
            {
                _pendingRecoveryLogins.Remove(login);
                return;
            }

            var itemCount = 0;
            try
            {
                var data = JsonSerializer.Deserialize<CrashRecoveryData>(json);
                if (data != null && !string.IsNullOrEmpty(data.EquippedJson))
                {
                    var equipped = StalkerStorageSystem.InventoryFromJson(data.EquippedJson);
                    itemCount = equipped.AllItems.Count;
                }
            }
            catch
            {
                // Malformed data, still show banner with 0 count
            }

            _ui.SetUiState(repository, StalkerRepositoryUiKey.Key,
                new CrashRecoveryUpdateState(true, itemCount));
        }
        catch (Exception e)
        {
            _sawmill.Error($"Failed to check crash recovery for {login}: {e}");
        }
    }

    #endregion

    #region Claim Handler — Spawn Items at Feet

    private async void OnCrashRecoveryClaim(EntityUid uid, StalkerRepositoryComponent component,
        CrashRecoveryClaimMessage msg)
    {
        if (msg.Actor == null)
            return;

        if (!_currentlyProcessingClaims.TryAdd(msg.Actor, 0))
            return;

        try
        {
            if (!TryComp<ActorComponent>(msg.Actor, out var actorComp))
                return;

            var login = actorComp.PlayerSession.Name;

            var json = await _dbManager.GetCrashRecovery(login);
            if (string.IsNullOrEmpty(json))
                return;

            // Clear FIRST (optimistic — prevents duplication if spawn crashes)
            await _dbManager.SetCrashRecovery(login, null);
            _pendingRecoveryLogins.Remove(login);

            if (!Exists(uid) || !Exists(msg.Actor))
                return;

            CrashRecoveryData? data;
            try
            {
                data = JsonSerializer.Deserialize<CrashRecoveryData>(json);
            }
            catch (Exception e)
            {
                _sawmill.Error($"Failed to deserialize crash recovery for {login}: {e}");
                return;
            }

            if (data == null || string.IsNullOrEmpty(data.EquippedJson))
                return;

            var equipped = StalkerStorageSystem.InventoryFromJson(data.EquippedJson);
            var coords = Transform(msg.Actor).Coordinates;
            var spawnedCount = 0;

            foreach (var item in equipped.AllItems)
            {
                if (item is not IItemStalkerStorage storageItem)
                    continue;

                if (string.IsNullOrEmpty(storageItem.PrototypeName))
                    continue;

                try
                {
                    var entity = Spawn(storageItem.PrototypeName, coords);
                    _stalkerStorage.SpawnedItem(entity, storageItem);
                    spawnedCount++;
                }
                catch (Exception e)
                {
                    _sawmill.Warning($"Failed to spawn recovery item {storageItem.PrototypeName}: {e}");
                }
            }

            _sawmill.Info($"Crash recovery: spawned {spawnedCount} items for {login}");

            _popup.PopupEntity(Loc.GetString("crash-recovery-claimed"), msg.Actor, msg.Actor,
                PopupType.Medium);
        }
        catch (Exception e)
        {
            _sawmill.Error($"Failed to process crash recovery claim: {e}");
        }
        finally
        {
            _currentlyProcessingClaims.TryRemove(msg.Actor, out _);
        }
    }

    #endregion

    #region Periodic Snapshot

    /// <summary>
    /// Manually trigger an immediate snapshot for all online players.
    /// Called by the crash_recovery_snapshot admin command.
    /// </summary>
    public void ForceSnapshot()
    {
        _sawmill.Info("Force snapshot triggered by admin command");

        var batch = new Dictionary<string, string>();

        foreach (var session in _playerManager.Sessions)
        {
            if (session.AttachedEntity is not { } playerEntity || !Exists(playerEntity))
                continue;

            if (!HasComp<InventoryComponent>(playerEntity))
                continue;

            var login = session.Name;

            // Don't overwrite unclaimed recovery data from a previous crash
            if (_pendingRecoveryLogins.Contains(login))
                continue;

            try
            {
                var data = CapturePlayerState(playerEntity);
                if (data != null)
                    batch[login] = data;
            }
            catch (Exception e)
            {
                _sawmill.Error($"Failed to snapshot player {login}: {e}");
            }
        }

        if (batch.Count > 0)
        {
            TrackDbWrite(() => _dbManager.SetCrashRecoveryBatch(batch), $"SetCrashRecoveryBatch(force,{batch.Count})");
            _sawmill.Info($"Force snapshot saved for {batch.Count} players");
        }
        else
        {
            _sawmill.Info("Force snapshot: no players to snapshot");
        }
    }

    private void StartSnapshotCycle()
    {
        _snapshotQueue.Clear();

        foreach (var session in _playerManager.Sessions)
        {
            if (session.AttachedEntity is not { } playerEntity)
                continue;

            if (!HasComp<InventoryComponent>(playerEntity))
                continue;

            if (_dirtyPlayers.Count > 0 && !_dirtyPlayers.Contains(playerEntity))
                continue;

            _snapshotQueue.Enqueue(session);
        }

        _dirtyPlayers.Clear();

        var totalTicks = (int)(_saveInterval * 0.5f * 30f);
        _playersPerTick = Math.Max(1, _snapshotQueue.Count > 0
            ? (int)Math.Ceiling((float)_snapshotQueue.Count / Math.Max(1, totalTicks))
            : 1);
    }

    private void ProcessSnapshotQueue()
    {
        if (_snapshotQueue.Count == 0)
            return;

        var batch = new Dictionary<string, string>();
        var processed = 0;

        while (_snapshotQueue.Count > 0 && processed < _playersPerTick)
        {
            var session = _snapshotQueue.Dequeue();
            processed++;

            if (session.AttachedEntity is not { } playerEntity || !Exists(playerEntity))
                continue;

            var login = session.Name;

            // Don't overwrite unclaimed recovery data from a previous crash
            if (_pendingRecoveryLogins.Contains(login))
                continue;

            try
            {
                var data = CapturePlayerState(playerEntity);
                if (data != null)
                    batch[login] = data;
            }
            catch (Exception e)
            {
                _sawmill.Error($"Failed to snapshot player {login}: {e}");
            }
        }

        if (batch.Count > 0)
            TrackDbWrite(() => _dbManager.SetCrashRecoveryBatch(batch), $"SetCrashRecoveryBatch(periodic,{batch.Count})");
    }

    /// <summary>
    /// Captures only equipped items — stash is already persisted separately.
    /// </summary>
    private string? CapturePlayerState(EntityUid player)
    {
        var equippedInventory = new AllStorageInventory();

        if (!_inventory.TryGetContainerSlotEnumerator(player, out var enumerator))
            return null;

        while (enumerator.NextItem(out var item, out _))
        {
            CaptureItemRecursive(item, equippedInventory);
        }

        if (equippedInventory.AllItems.Count == 0)
            return null;

        var equippedJson = StalkerStorageSystem.InventoryToJson(equippedInventory);
        var recoveryData = new CrashRecoveryData
        {
            EquippedJson = equippedJson,
        };

        return JsonSerializer.Serialize(recoveryData);
    }

    private void CaptureItemRecursive(EntityUid item, AllStorageInventory inventory, int depth = 0)
    {
        if (depth > 5)
            return;

        // Blacklist (same as StalkerRepositorySystem.GetRecursiveContainerElements)
        if (HasComp<OrganComponent>(item) ||
            HasComp<InstantActionComponent>(item) ||
            HasComp<WorldTargetActionComponent>(item) ||
            HasComp<EntityTargetActionComponent>(item) ||
            HasComp<SubdermalImplantComponent>(item) ||
            HasComp<BodyPartComponent>(item) ||
            HasComp<VirtualItemComponent>(item) ||
            HasComp<MindContainerComponent>(item) ||
            HasComp<StatusEffectComponent>(item) ||
            HasComp<UnremoveableComponent>(item) ||
            HasComp<SelfUnremovableClothingComponent>(item))
            return;

        var converted = _stalkerStorage.ConvertToIItemStalkerStorage(item);
        foreach (var obj in converted)
        {
            if (obj is IItemStalkerStorage)
                inventory.AllItems.Add(obj);
        }

        if (!TryComp<ContainerManagerComponent>(item, out var containerMan))
            return;

        foreach (var container in containerMan.Containers)
        {
            if (container.Key is "toggleable-clothing" or "program-container")
                continue;

            foreach (var contained in container.Value.ContainedEntities)
            {
                if (HasComp<BallisticAmmoProviderComponent>(item) &&
                    HasComp<CartridgeAmmoComponent>(contained))
                    continue;

                CaptureItemRecursive(contained, inventory, depth + 1);
            }
        }
    }

    /// <summary>
    /// Immediately snapshots a player's equipped state to DB.
    /// Called when stash contents change to keep crash recovery in sync.
    /// </summary>
    public void ImmediateSnapshot(EntityUid player)
    {
        if (!_enabled)
            return;

        if (!TryComp<ActorComponent>(player, out var actorComp))
            return;

        if (!HasComp<InventoryComponent>(player))
            return;

        var login = actorComp.PlayerSession.Name;

        // Don't overwrite unclaimed recovery data from a previous crash
        if (_pendingRecoveryLogins.Contains(login))
            return;

        try
        {
            var data = CapturePlayerState(player);
            if (data != null)
            {
                var payload = new Dictionary<string, string> { { login, data } };
                TrackDbWrite(() => _dbManager.SetCrashRecoveryBatch(payload), $"SetCrashRecoveryBatch(immediate,{login})");
            }
            else
            {
                TrackDbWrite(() => _dbManager.SetCrashRecovery(login, null), $"SetCrashRecovery(clear,{login})");
            }

            _dirtyPlayers.Remove(player);
        }
        catch (Exception e)
        {
            _sawmill.Error($"Failed immediate snapshot for {login}: {e}");
        }
    }

    #endregion
}
