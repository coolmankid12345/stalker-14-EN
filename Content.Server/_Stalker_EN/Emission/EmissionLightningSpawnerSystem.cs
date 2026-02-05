using System.Diagnostics.CodeAnalysis;
using Content.Server._Stalker.StationEvents.Components;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Stalker_EN.Emission;

/// <summary>
/// Manages lightning spawner components on entities during emissions.
/// </summary>
public sealed class EmissionLightningSpawnerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IRobustRandom _robustRandom = default!;
    [Dependency] private readonly EmissionLightningSystem _emissionLightningSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BlowoutTargetComponent, ComponentStartup>(OnBlowoutTargetStartup);
        SubscribeLocalEvent<BlowoutTargetComponent, ComponentShutdown>(OnBlowoutTargetShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var spawnerQuery = EntityQueryEnumerator<EmissionLightningSpawnerComponent>();
        while (spawnerQuery.MoveNext(out var spawnerUid, out var spawnerComponent))
        {
            if (_gameTiming.CurTime < spawnerComponent.NextLightning)
                continue;

            spawnerComponent.NextLightning = _gameTiming.CurTime + TimeSpan.FromSeconds(_robustRandom.NextFloat(spawnerComponent.LightningIntervalRange.X, spawnerComponent.LightningIntervalRange.Y));
            _emissionLightningSystem.TrySpawnLightningNearby(spawnerUid, spawnerComponent.SpawnRadius, spawnerComponent.LightningEffectProtoId, spawnerComponent.BoltRange, spawnerComponent.BoltCount);
        }
    }

    private void OnBlowoutTargetStartup(Entity<BlowoutTargetComponent> entity, ref ComponentStartup args)
    {
        if (HasComp<EmissionLightningSpawnerComponent>(entity) ||
            !TryGetActiveEmissionInStage2(out var emissionComp))
            return;

        if (emissionComp.LightningEffectProtoId is not { } lightningProtoId)
            return;

        var spawnerComp = EnsureComp<EmissionLightningSpawnerComponent>(entity);
        spawnerComp.SpawnRadius = emissionComp.LightningSpawnRadius;
        spawnerComp.LightningIntervalRange = emissionComp.LightningIntervalRange;
        spawnerComp.LightningEffectProtoId = lightningProtoId;
    }

    private void OnBlowoutTargetShutdown(Entity<BlowoutTargetComponent> entity, ref ComponentShutdown args)
    {
        RemComp<EmissionLightningSpawnerComponent>(entity);
    }

    private bool TryGetActiveEmissionInStage2([NotNullWhen(true)] out EmissionEventRuleComponent? emissionComp)
    {
        emissionComp = null;

        var query = EntityQueryEnumerator<ActiveGameRuleComponent, EmissionEventRuleComponent>();
        while (query.MoveNext(out _, out _, out var emission))
        {
            if (emission.Stage != EmissionStage.Stage2)
                continue;

            emissionComp = emission;
            return true;
        }

        return false;
    }
}
