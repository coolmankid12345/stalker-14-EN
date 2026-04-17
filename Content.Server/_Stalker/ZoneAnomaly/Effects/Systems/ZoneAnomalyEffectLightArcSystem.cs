using System.Linq; // Stalker-EN
using Content.Server.Lightning;
using Content.Shared._Stalker.ZoneAnomaly.Components;
using Content.Shared._Stalker.ZoneAnomaly.Effects.Components;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Whitelist;
using NetCord;
using Robust.Shared.Random;

namespace Content.Server._Stalker.ZoneAnomaly.Effects.Systems;

public sealed class ZoneAnomalyEffectLightArcSystem : EntitySystem
{
    private const int MaxIterations = 12;

    [Dependency] private readonly PredictedBatterySystem _battery = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly LightningSystem _lightning = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelistSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!; // Stalker-EN changes

    public override void Initialize()
    {
        SubscribeLocalEvent<ZoneAnomalyEffectLightArcComponent, ZoneAnomalyActivateEvent>(OnActivate);
    }

    // Stalker-EN changes - We rewrote everything below here, dont take upstream changes
    private void OnActivate(Entity<ZoneAnomalyEffectLightArcComponent> effect, ref ZoneAnomalyActivateEvent args)
    {
        var entities = _lookup.GetEntitiesInRange(Transform(effect).Coordinates,
            effect.Comp.Distance,
            flags: LookupFlags.Uncontained);

        var i = 0;
        foreach (var entity in entities)
        {
            if (i >= MaxIterations)
                break;

            if (!_whitelistSystem.IsWhitelistPass(effect.Comp.Whitelist, entity))
                continue;

            TryRecharge(effect, entity);
            _lightning.ShootLightning(effect, entity, effect.Comp.Lighting);
            i++;
        }
    }

    private void TryRecharge(Entity<ZoneAnomalyEffectLightArcComponent> effect, Entity<PredictedBatteryComponent?> target)
    {
        if(!Resolve(target, ref target.Comp, false))
            return;

        _battery.SetCharge(target, target.Comp.LastCharge + target.Comp.MaxCharge * effect.Comp.ChargePercent);
    }
    // stalker-en-end
}
