using Content.Server.Afk.Events;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Server.Players.PlayTimeTracking;
using Content.Shared._Stalker_EN.CharacterRank;
using Content.Shared.Actions;
using Content.Shared.GameTicking;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Players.PlayTimeTracking;
using Robust.Shared.Console;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Stalker_EN.CharacterRank;

/// <summary>
/// Server-side system that tracks per-character playtime, persists it to the database,
/// and derives a rank from configurable time thresholds.
/// </summary>
public sealed class STCharacterRankSystem : EntitySystem
{
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly PlayTimeTrackingManager _playtime = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;

    private static readonly ProtoId<STCharacterRankPrototype> DefaultConfig = "Default";

    /// <summary>
    /// Interval between in-memory time flushes (accumulate elapsed real-time).
    /// </summary>
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Interval between database auto-saves.
    /// </summary>
    private static readonly TimeSpan DbSaveInterval = TimeSpan.FromSeconds(300);

    private STCharacterRankPrototype _config = default!;
    private readonly Dictionary<EntityUid, CharacterRankTrackingData> _tracked = new();
    private TimeSpan _nextFlush;
    private TimeSpan _nextDbSave;

    public override void Initialize()
    {
        base.Initialize();

        _config = _proto.Index(DefaultConfig);

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<STCharacterRankComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<STCharacterRankComponent, PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<STCharacterRankComponent, PlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<STCharacterRankComponent, ComponentRemove>(OnComponentRemove);
        SubscribeLocalEvent<STCharacterRankComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<STCharacterRankComponent, STCharacterRankToggleEvent>(OnToggleRank);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<AFKEvent>(OnAfk);
        SubscribeLocalEvent<UnAFKEvent>(OnUnAfk);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
    }

    private void OnComponentInit(EntityUid uid, STCharacterRankComponent comp, ComponentInit args)
    {
        _actions.AddAction(uid, ref comp.ActionEntity, comp.Action, uid);
    }

    private void OnToggleRank(EntityUid uid, STCharacterRankComponent comp, STCharacterRankToggleEvent args)
    {
        if (args.Handled || !_mobState.IsAlive(uid))
            return;

        comp.Enabled = !comp.Enabled;
        Dirty(uid, comp);

        args.Handled = true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var realTime = _timing.RealTime;

        if (realTime >= _nextFlush)
        {
            _nextFlush = realTime + FlushInterval;
            FlushAll();
        }

        if (realTime >= _nextDbSave)
        {
            _nextDbSave = realTime + DbSaveInterval;
            SaveAllToDb();
        }
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        var uid = args.Mob;
        var userId = args.Player.UserId;
        var characterName = args.Profile.Name;

        // If old entity is still being tracked (respawn), flush+save old data.
        foreach (var (oldUid, oldData) in _tracked)
        {
            if (oldData.UserId == userId && oldData.CharacterName == characterName && oldUid != uid)
            {
                FlushEntity(oldUid, oldData);
                SaveSingleToDb(oldData);
                _tracked.Remove(oldUid);
                break;
            }
        }

        // Add component and start loading from DB.
        var comp = EnsureComp<STCharacterRankComponent>(uid);

        var data = new CharacterRankTrackingData
        {
            UserId = userId,
            CharacterName = characterName,
            AccumulatedTime = TimeSpan.Zero,
            LastFlushTime = _timing.RealTime,
            IsActive = true,
        };

        _tracked[uid] = data;

        // Capture overall playtime to seed new characters.
        TimeSpan? overallPlaytime = null;
        if (_playtime.TryGetTrackerTimes(args.Player, out var times)
            && times.TryGetValue(PlayTimeTrackingShared.TrackerOverall, out var overall))
        {
            overallPlaytime = overall;
        }

        // Load existing time from database.
        LoadFromDbAsync(uid, comp, data, overallPlaytime);
    }

    private async void LoadFromDbAsync(
        EntityUid uid,
        STCharacterRankComponent comp,
        CharacterRankTrackingData data,
        TimeSpan? overallPlaytime)
    {
        try
        {
            var record = await _db.GetStalkerCharacterRankAsync(data.UserId, data.CharacterName);

            // Post-await safety: entity may have been deleted during the await.
            if (Deleted(uid))
                return;

            if (record != null)
            {
                data.AccumulatedTime = record.TimeSpent;
            }
            else if (overallPlaytime.HasValue)
            {
                // Seed new characters from the player's overall playtime.
                data.AccumulatedTime = overallPlaytime.Value;
                SaveSingleToDb(data);
            }

            UpdateRank(uid, comp, data);
        }
        catch (Exception e)
        {
            Log.Error($"Failed to load character rank for {data.CharacterName}: {e}");
        }
    }

    private void OnPlayerAttached(EntityUid uid, STCharacterRankComponent comp, PlayerAttachedEvent args)
    {
        // Resume tracking on reconnect (SSD return), but not if dead.
        if (!_tracked.TryGetValue(uid, out var data))
            return;

        if (TryComp<MobStateComponent>(uid, out var mobState) && mobState.CurrentState == MobState.Dead)
            return;

        data.IsActive = true;
        data.LastFlushTime = _timing.RealTime;
    }

    private void OnPlayerDetached(EntityUid uid, STCharacterRankComponent comp, PlayerDetachedEvent args)
    {
        if (!_tracked.TryGetValue(uid, out var data))
            return;

        FlushEntity(uid, data);
        data.IsActive = false;

        SaveSingleToDb(data);
    }

    private void OnComponentRemove(EntityUid uid, STCharacterRankComponent comp, ComponentRemove args)
    {
        if (comp.ActionEntity.HasValue)
            _actions.RemoveAction(uid, comp.ActionEntity.Value);

        if (_tracked.TryGetValue(uid, out var data))
        {
            FlushEntity(uid, data);
            SaveSingleToDb(data);
            _tracked.Remove(uid);
        }
    }

    private void OnMobStateChanged(EntityUid uid, STCharacterRankComponent comp, MobStateChangedEvent args)
    {
        if (!_tracked.TryGetValue(uid, out var data))
            return;

        if (args.NewMobState == MobState.Dead)
        {
            FlushEntity(uid, data);
            data.IsActive = false;
        }
        else if (args.OldMobState == MobState.Dead && args.NewMobState == MobState.Alive)
        {
            // Revived - resume tracking.
            data.IsActive = true;
            data.LastFlushTime = _timing.RealTime;
        }
    }

    private void OnAfk(ref AFKEvent args)
    {
        if (args.Session.AttachedEntity is not { } uid)
            return;

        if (!_tracked.TryGetValue(uid, out var data))
            return;

        FlushEntity(uid, data);
        data.IsActive = false;
    }

    private void OnUnAfk(ref UnAFKEvent args)
    {
        if (args.Session.AttachedEntity is not { } uid)
            return;

        if (!_tracked.TryGetValue(uid, out var data))
            return;

        // Don't resume tracking for dead mobs.
        if (TryComp<MobStateComponent>(uid, out var mobState) && mobState.CurrentState == MobState.Dead)
            return;

        data.IsActive = true;
        data.LastFlushTime = _timing.RealTime;
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        // Flush and save all before round cleanup.
        FlushAll();
        SaveAllToDb();
        _tracked.Clear();
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (!args.WasModified<STCharacterRankPrototype>())
            return;

        _config = _proto.Index(DefaultConfig);
    }

    /// <summary>
    /// Flushes elapsed real-time into accumulated time for all active tracked entities.
    /// </summary>
    private void FlushAll()
    {
        var realTime = _timing.RealTime;

        foreach (var (uid, data) in _tracked)
        {
            if (!data.IsActive)
                continue;

            var elapsed = realTime - data.LastFlushTime;
            data.AccumulatedTime += elapsed;
            data.LastFlushTime = realTime;

            if (TryComp<STCharacterRankComponent>(uid, out var comp))
            {
                UpdateRank(uid, comp, data);
            }
        }
    }

    /// <summary>
    /// Flushes a single entity's elapsed time.
    /// </summary>
    private void FlushEntity(EntityUid uid, CharacterRankTrackingData data)
    {
        if (!data.IsActive)
            return;

        var elapsed = _timing.RealTime - data.LastFlushTime;
        data.AccumulatedTime += elapsed;
        data.LastFlushTime = _timing.RealTime;

        if (TryComp<STCharacterRankComponent>(uid, out var comp))
        {
            UpdateRank(uid, comp, data);
        }
    }

    /// <summary>
    /// Recalculates the rank from accumulated time and updates the component if rank changed.
    /// </summary>
    private void UpdateRank(EntityUid uid, STCharacterRankComponent comp, CharacterRankTrackingData data)
    {
        var newRank = CalculateRank(data.AccumulatedTime);

        // Always update server-only accumulated time.
        comp.AccumulatedTime = data.AccumulatedTime;

        if (newRank.Index == comp.RankIndex)
            return;

        // Rank changed - update networked fields and dirty.
        comp.RankIndex = newRank.Index;
        comp.RankIconId = newRank.IconId;
        comp.RankName = newRank.Name;
        Dirty(uid, comp);
    }

    /// <summary>
    /// Finds the highest rank whose TimeRequired is met.
    /// </summary>
    private STRankDefinition CalculateRank(TimeSpan accumulatedTime)
    {
        var ranks = _config.Ranks;
        var result = ranks[0];

        for (var i = ranks.Count - 1; i >= 0; i--)
        {
            if (accumulatedTime >= ranks[i].TimeRequired)
            {
                result = ranks[i];
                break;
            }
        }

        return result;
    }

    /// <summary>
    /// Saves all tracked entries to the database in a single bulk upsert.
    /// </summary>
    private async void SaveAllToDb()
    {
        if (_tracked.Count == 0)
            return;

        try
        {
            var updates = new List<(Guid UserId, string CharacterName, TimeSpan Time)>(_tracked.Count);

            foreach (var (_, data) in _tracked)
            {
                updates.Add((data.UserId, data.CharacterName, data.AccumulatedTime));
            }

            await _db.UpdateStalkerCharacterRankTimesAsync(updates);
        }
        catch (Exception e)
        {
            Log.Error($"Failed to save character ranks: {e}");
        }
    }

    /// <summary>
    /// Saves a single entry to the database.
    /// </summary>
    private async void SaveSingleToDb(CharacterRankTrackingData data)
    {
        try
        {
            var updates = new List<(Guid UserId, string CharacterName, TimeSpan Time)>(1)
            {
                (data.UserId, data.CharacterName, data.AccumulatedTime),
            };

            await _db.UpdateStalkerCharacterRankTimesAsync(updates);
        }
        catch (Exception e)
        {
            Log.Error($"Failed to save character rank for {data.CharacterName}: {e}");
        }
    }

    /// <summary>
    /// Gets the tracking data for an entity, if tracked. Used by admin commands.
    /// </summary>
    public CharacterRankTrackingData? GetTrackingData(EntityUid uid)
    {
        return _tracked.GetValueOrDefault(uid);
    }

    /// <summary>
    /// Flushes an entity's time and returns updated data. Used by admin commands for accurate reads.
    /// </summary>
    public void FlushTracking(EntityUid uid)
    {
        if (_tracked.TryGetValue(uid, out var data))
            FlushEntity(uid, data);
    }

    /// <summary>
    /// Sets accumulated time for an entity and recalculates rank. Used by admin commands.
    /// </summary>
    public void SetAccumulatedTime(EntityUid uid, TimeSpan time)
    {
        if (!_tracked.TryGetValue(uid, out var data))
            return;

        if (!TryComp<STCharacterRankComponent>(uid, out var comp))
            return;

        data.AccumulatedTime = time;
        UpdateRank(uid, comp, data);
        SaveSingleToDb(data);
    }

    /// <summary>
    /// Gets the rank definition for a given index. Used by admin commands.
    /// </summary>
    public STRankDefinition? GetRankByIndex(int index)
    {
        var ranks = _config.Ranks;
        foreach (var rank in ranks)
        {
            if (rank.Index == index)
                return rank;
        }

        return null;
    }

    /// <summary>
    /// The maximum valid rank index.
    /// </summary>
    public int MaxRankIndex => _config.Ranks.Count - 1;

    /// <summary>
    /// Gets a rank definition by its localization name key. Used by admin commands.
    /// </summary>
    public STRankDefinition? GetRankByName(LocId name)
    {
        foreach (var rank in _config.Ranks)
        {
            if (rank.Name == name)
                return rank;
        }

        return null;
    }

    /// <summary>
    /// Returns all configured rank definitions. Used by admin commands for tab-completion.
    /// </summary>
    public IReadOnlyList<STRankDefinition> GetAllRanks() => _config.Ranks;

    /// <summary>
    /// Transfers accumulated time from an old character name to the currently tracked entity.
    /// Writes success/failure output to the provided shell.
    /// </summary>
    public async void TransferRankAsync(EntityUid uid, string oldCharacterName, string username, IConsoleShell shell)
    {
        try
        {
            var data = GetTrackingData(uid);
            if (data == null)
            {
                shell.WriteError(Loc.GetString("cmd-strank-error-no-component"));
                return;
            }

            var record = await _db.GetStalkerCharacterRankAsync(data.UserId, oldCharacterName);

            if (Deleted(uid))
                return;

            if (record == null)
            {
                shell.WriteError(Loc.GetString("cmd-strank-transfer-not-found", ("oldName", oldCharacterName)));
                return;
            }

            if (!TryComp<STCharacterRankComponent>(uid, out var comp))
                return;

            var newName = data.CharacterName;
            data.AccumulatedTime = record.TimeSpent;
            UpdateRank(uid, comp, data);
            SaveSingleToDb(data);

            shell.WriteLine(Loc.GetString("cmd-strank-transfer-success",
                ("time", $"{record.TimeSpent.TotalHours:F1}h"),
                ("oldName", oldCharacterName),
                ("newName", newName),
                ("username", username)));
        }
        catch (Exception e)
        {
            Log.Error($"Failed to transfer rank from {oldCharacterName}: {e}");
            shell.WriteError($"Transfer failed: {e.Message}");
        }
    }
}
