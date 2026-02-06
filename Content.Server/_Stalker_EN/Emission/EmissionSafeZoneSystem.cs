using System.Diagnostics.CodeAnalysis;
using Content.Server._Stalker.StationEvents.Components;
using Content.Shared.GameTicking.Components;

namespace Content.Server._Stalker_EN.Emission;

/// <summary>
/// Manages lightning spawner components on entities entering/exiting safe zones during emissions.
/// Ensures entities in safe zones are protected from emission lightning strikes.
/// </summary>
public sealed class EmissionSafeZoneSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StalkerSafeZoneComponent, ComponentStartup>(OnSafeZoneEnter);
        SubscribeLocalEvent<StalkerSafeZoneComponent, ComponentRemove>(OnSafeZoneExit);
    }

    private void OnSafeZoneEnter(Entity<StalkerSafeZoneComponent> entity, ref ComponentStartup args)
    {
        RemComp<EmissionLightningSpawnerComponent>(entity);
    }

    private void OnSafeZoneExit(Entity<StalkerSafeZoneComponent> entity, ref ComponentRemove args)
    {
        if (!TryGetActiveEmissionInStage2(out var emissionComp))
            return;

        if (HasComp<EmissionLightningSpawnerComponent>(entity))
            return;

        if (emissionComp.LightningEffectProtoId is not { } lightningProtoId)
            return;

        var spawnerComp = EnsureComp<EmissionLightningSpawnerComponent>(entity);
        spawnerComp.SpawnRadius = emissionComp.LightningSpawnRadius;
        spawnerComp.LightningIntervalRange = emissionComp.LightningIntervalRange;
        spawnerComp.LightningEffectProtoId = lightningProtoId;
    }

    private bool TryGetActiveEmissionInStage2([NotNullWhen(true)] out EmissionEventRuleComponent? emissionComp)
    {
        emissionComp = null;

        var query = EntityQueryEnumerator<ActiveGameRuleComponent, EmissionEventRuleComponent>();
        while (query.MoveNext(out _, out _, out var emission))
        {
            if (emission.Stage == EmissionStage.Stage2)
            {
                emissionComp = emission;
                return true;
            }
        }

        return false;
    }
}
