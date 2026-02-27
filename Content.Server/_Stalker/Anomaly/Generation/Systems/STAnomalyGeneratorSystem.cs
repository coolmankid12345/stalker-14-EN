using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Content.Server._Stalker.Anomaly.Generation.Components;
using Content.Server._Stalker.Anomaly.Generation.Jobs;
using Content.Shared._Stalker.Anomaly.Data;
using Content.Shared.GameTicking;
using Robust.Shared.CPUJob.JobQueues.Queues;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Stalker.Anomaly.Generation.Systems;

public sealed partial class STAnomalyGeneratorSystem : EntitySystem
{
    [Dependency] private readonly IMapManager _map = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    private const double JobTime = 0.005;

    private readonly JobQueue _jobQueue = new(JobTime);
    private readonly Dictionary<STAnomalyGenerationJob, CancellationTokenSource> _jobs = new();

    private Entity<STAnomalyGeneratorComponent> Data
    {
        get
        {
            var query = EntityQueryEnumerator<STAnomalyGeneratorComponent>();
            while (query.MoveNext(out var uid, out var component))
            {
                return (uid, component);
            }

            var newUid = Spawn(null, MapCoordinates.Nullspace);
            var newComponent = EnsureComp<STAnomalyGeneratorComponent>(newUid);
            return (newUid, newComponent);
        }
    }

    public override void Initialize()
    {
        base.Initialize();

        InitializeCommands();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundCleanup);
        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStart);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _jobQueue.Process();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        CleanJobs();
    }

    private void OnRoundCleanup(RoundRestartCleanupEvent ev)
    {
        CleanJobs();
    }

    private void OnRoundStart(RoundStartedEvent ev)
    {
        var query = EntityQueryEnumerator<MapComponent, STAnomalyGeneratorTargetComponent>();
        while (query.MoveNext(out var entityUid, out var mapComponent, out var targetComponent))
        {
            if (!_prototype.TryIndex(targetComponent.OptionsId, out var options))
            {
                Log.Error($"Can't start generation on {ToPrettyString(entityUid)}!");
#if DEBUG
                throw new KeyNotFoundException();
#endif
                continue;
            }

            StartGeneration(mapComponent.MapId, options.Options);
        }
    }

    private void CleanJobs()
    {
        foreach (var token in _jobs.Values)
        {
            token.Cancel();
        }

        _jobs.Clear();
    }

    // stalker-en-changes: made public for emission anomaly regeneration
    public async Task<STAnomalyGenerationJobData> StartGeneration(MapId mapId, STAnomalyGenerationOptions options)
    {
        var cancelToken = new CancellationTokenSource();
        var job = new STAnomalyGenerationJob(options with { MapId = mapId }, JobTime, cancelToken.Token);

        _jobs.Add(job, cancelToken);
        _jobQueue.EnqueueJob(job);

        Log.Info($"Generation {job.AsTask.Id} for {mapId} started");

        await job.AsTask;

        if (job.Exception is not null)
            throw job.Exception;

        var count = job.Result!.SpawnedAnomalies.Count;
        var total = options.TotalCount;
        var percent = (float)Math.Round(count / (float)total * 100f, 2);

        Log.Info($"Generation {job.AsTask.Id} end, count: {count}\\{total} ({percent}%)");

        if (count == 0)
            Log.Warning($"Generation {job.AsTask.Id} for {mapId} produced 0 anomalies out of {total} requested!");

        Data.Comp.MapGeneratedAnomalies[mapId] = job.Result!.SpawnedAnomalies;
        return job.Result!;
    }

    // stalker-en-changes: made public for emission anomaly regeneration
    public void ClearGeneration(MapId mapId)
    {
        if (!Data.Comp.MapGeneratedAnomalies.TryGetValue(mapId, out var anomalies))
            return;

        Log.Info($"Clearing for {mapId} started");

        var count = 0;
        foreach (var anomaly in anomalies)
        {
            QueueDel(anomaly);
            count++;
        }

        Data.Comp.MapGeneratedAnomalies.Remove(mapId);

        Log.Info($"Clearing for {mapId} ended, count: {count}");
    }
}
