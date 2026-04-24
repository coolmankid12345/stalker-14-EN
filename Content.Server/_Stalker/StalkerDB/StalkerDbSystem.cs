using System.Collections.Concurrent;
using System.Threading.Tasks;
using Content.Server._Stalker.Teleports.DuplicateTeleport;
using Content.Server.Database;
using Content.Shared.GameTicking;
using Content.Shared._Stalker.Teleport;
using Robust.Shared.Prototypes;
using System.Linq;
using Content.Server._Stalker.Storage;
using Content.Server._Stalker.Characteristics;

namespace Content.Server._Stalker.StalkerDB;

// TODO: Idk if it should be cached, probably remove caching to avoid 635 DB operations on start???
public sealed class StalkerDbSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IServerDbManager _dbManager = default!;
    [Dependency] private readonly StalkerStorageSystem _stalkerStorageSystem = default!;

    // stalker-en-changes-start: track fire-and-forget DB writes so shutdown can drain them
    private readonly ConcurrentBag<Task> _pendingDbWrites = new();
    private ISawmill _sawmill = default!;
    // stalker-en-changes-end

    public const string DefaultStalkerItems =
"""
{
  "AllItems": [
    {
      "ClassType": "StackItemStalker",
      "PrototypeName": "Roubles",
      "StackCount": 3000,
      "CountVendingMachine": 1
    },
    {
      "ClassType": "SimpleItemStalker",
      "PrototypeName": "StalkerHunterCrate",
      "CountVendingMachine": 1
    },
    {
      "ClassType": "SimpleItemStalker",
      "PrototypeName": "StalkerArtefactHunterCrate",
      "CountVendingMachine": 1
    }
  ]
}
""";

    // login - json
    public ConcurrentDictionary<string, string> Stalkers = new();
    private List<string> _symbols = new();

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = Logger.GetSawmill("stalker.db"); // stalker-en-changes
        InitializeGroupRecords();
        LoadPrototypes();
        SubscribeLocalEvent<PlayerBeforeSpawnEvent>(BeforeSpawn);
    }

    // stalker-en-changes-start: drain orphaned DB writes so shutdown isn't racing the Npgsql pool
    public override void Shutdown()
    {
        base.Shutdown();
        // Safety net; EntryPoint.Shutdown drained earlier, but writes may have occurred since.
        FlushPendingWrites(TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// Call BEFORE EntryPoint.Dispose so the Npgsql pool isn't saturated when it runs ClearAllCrashRecovery.
    /// </summary>
    public bool FlushPendingWrites(TimeSpan timeout)
    {
        var pending = _pendingDbWrites.ToArray();
        if (pending.Length == 0)
        {
            _sawmill.Info("[shutdown] StalkerDbSystem: no pending writes");
            return true;
        }

        _sawmill.Info($"[shutdown] StalkerDbSystem flushing {pending.Length} pending write(s)");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var completed = false;
        try
        {
            completed = Task.WhenAll(pending).Wait(timeout);
        }
        catch (Exception e)
        {
            _sawmill.Error($"[shutdown] StalkerDbSystem error awaiting writes: {e}");
        }
        sw.Stop();
        _sawmill.Info($"[shutdown] StalkerDbSystem flush {(completed ? "completed" : "TIMED OUT")} after {sw.ElapsedMilliseconds}ms");
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
    // stalker-en-changes-end

    #region InventoryOperations

    private void LoadPrototypes()
    {
        var prototypes = _prototype.EnumeratePrototypes<DuplicateSymbolsPrototype>();
        foreach (var proto in prototypes)
        {
            _symbols.AddRange(proto.Symbols);
        }
    }
    public void InitializeGroupRecords()
    {
        var prototypes = _prototype.EnumeratePrototypes<StalkerBandPrototype>();
        foreach (var prototype in prototypes)
        {
            foreach (var item in prototype.BandTeleports)
            {
                _ = LoadPlayer(item, false);
            }
        }
    }
    public string GetInventoryJson(string login)
    {
        return !Stalkers.TryGetValue(login, out var value) ? "" : value;
    }

    public void SetInventoryJson(string login, string inputInventoryJson)
    {
        Stalkers[login] = inputInventoryJson;
        TrackDbWrite(() => _dbManager.SetLoginItems(login, inputInventoryJson), $"SetLoginItems({login})"); // stalker-en-changes
    }

    public void ClearAllRepositories(string login)
    {
        TrackDbWrite(() => _dbManager.SetAllLoginItems(login, DefaultStalkerItems), $"SetAllLoginItems({login})"); // stalker-en-changes
    }

    public void ClearInventoryJson(string login)
    {
        TrackDbWrite(() => _dbManager.SetLoginItems(login, DefaultStalkerItems), $"ClearInventoryJson({login})"); // stalker-en-changes
    }

    private async Task LoadPlayer(string login, bool loadSymbols = true)
    {
        if (Stalkers.ContainsKey(login))
            return;

        var record = await _dbManager.GetLoginItems(login) ?? DefaultStalkerItems;
        Stalkers.TryAdd(login, record);
        if (loadSymbols)
            await LoadSymbolsPlayers(login);
        var ev = new NewRecordAddedEvent(login);
        if (await _dbManager.EnsureRecordCreated(login, DefaultStalkerItems))
            RaiseLocalEvent(ref ev);
    }

    private async Task LoadSymbolsPlayers(string login)
    {
        foreach (var item in _symbols)
        {
            var concat = item + login;
            if (Stalkers.ContainsKey(concat))
                continue;
            var ev = new NewRecordAddedEvent(concat);
            if (await _dbManager.EnsureRecordCreated(concat, DefaultStalkerItems))
                RaiseLocalEvent(ref ev);

            var items = await _dbManager.GetLoginItems(concat) ?? DefaultStalkerItems;

            Stalkers.TryAdd(concat, items);
        }
    }

    /// <summary>
    /// Reset every stash record in the database to the default starting items and update in-world repositories.
    /// </summary>
    public async Task ResetAllStashes()
    {
        // 1) Update DB: set every stalker storage row to default JSON
        await _dbManager.SetAllStalkerItems(DefaultStalkerItems);

            // Also clear all stalker stats in the DB so stats are reset to defaults as well.
            try
            {
                await _dbManager.ClearAllStalkerStats();
            }
            catch (Exception e)
            {
                Logger.ErrorS("stalker.db", $"Failed to clear stalker stats during ResetAllStashes: {e}");
                throw;
            }

            // If there's an in-memory cache of stalker stats, clear it as well by calling the characteristic system.
            try
            {
                var charSys = EntityManager.System<CharacteristicContainerSystem>();
                charSys.ClearAllStatsCache();
            }
            catch (Exception e)
            {
                // Non-fatal: log but continue. We don't want to abort the reset for this.
                Logger.WarningS("stalker.db", $"Failed to clear in-memory stalker stat caches: {e}");
            }

        // 2) Update in-memory cache so newly loaded players don't observe stale empty values
        foreach (var key in Stalkers.Keys.ToList())
        {
            Stalkers[key] = DefaultStalkerItems;
        }

        // 3) Reset all in-world repository components so their contained items and loaded JSON are set to defaults
        try
        {
            _stalkerStorageSystem.ClearAllStorages();
        }
        catch (Exception e)
        {
            // If something goes wrong clearing storages, log and rethrow so callers see the failure
            Logger.ErrorS("stalker.db", $"Failed to clear in-world storages during ResetAllStashes: {e}");
            throw;
        }
    }

    #endregion

    private async void BeforeSpawn(PlayerBeforeSpawnEvent ev)
    {
        await LoadPlayer(ev.Player.Name);
    }
}

[ByRefEvent]
public record struct NewRecordAddedEvent(string Login);
