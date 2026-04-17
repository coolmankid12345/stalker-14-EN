using Content.Server.Lightning;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared._Stalker.ZoneAnomaly.Components;
using Content.Shared._Stalker.ZoneAnomaly.Effects.Components;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Whitelist;
using Robust.Shared.Map.Components;

namespace Content.Server._Stalker.ZoneAnomaly.Effects.Systems;

public sealed class ZoneAnomalyEffectLightArcSystem : EntitySystem
{
    private const int MaxIterations = 12;

    [Dependency] private readonly PredictedBatterySystem _battery = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly LightningSystem _lightning = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelistSystem = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ZoneAnomalyEffectLightArcComponent, ZoneAnomalyActivateEvent>(OnActivate);
    }

    // stalker-en We no longer have to validate recursively, because we are just filtering
    private void OnActivate(Entity<ZoneAnomalyEffectLightArcComponent> effect, ref ZoneAnomalyActivateEvent args)
    {
        var i = 0;
        // Stalker-en - filter for uncontained items
        var entities = _lookup.GetEntitiesInRange(Transform(effect).Coordinates, effect.Comp.Distance, LookupFlags.Uncontained);
        foreach (var entity in entities)
        {
            if (i > MaxIterations)
                break;

            // We don't need to shoot all the entities
            if(!_whitelistSystem.IsWhitelistPass(effect.Comp.Whitelist, entity))
                continue;

            TryRecharge(effect, entity);
            _lightning.ShootLightning(effect, entity, effect.Comp.Lighting);

            i++;
        }
    }

    private void TryRecharge(Entity<ZoneAnomalyEffectLightArcComponent> effect, Entity<PredictedBatteryComponent?> target)
    {
        // stalker-en - changed from trycomp to resolve for minor performance
        if (!Resolve(target, ref target.Comp))
            return;

        _battery.SetCharge((target, target.Comp), target.Comp.LastCharge + target.Comp.MaxCharge * effect.Comp.ChargePercent);
    }
}
