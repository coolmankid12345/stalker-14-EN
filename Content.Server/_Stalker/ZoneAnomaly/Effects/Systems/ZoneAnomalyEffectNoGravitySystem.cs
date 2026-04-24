using Content.Shared._Stalker.ZoneAnomaly.Components;
using Content.Shared._Stalker.ZoneAnomaly.Effects.Components;
using Content.Shared.Movement.Components;
using Content.Shared._Stalker.ZoneAnomaly.Systems;
using Content.Shared.Gravity;

namespace Content.Server._Stalker.ZoneAnomaly.Effects.Systems;

public sealed class ZoneAnomalyEffectNoGravitySystem : SharedZoneAnomalyEffectNoGravitySystem
{
    [Dependency] private readonly SharedGravitySystem _gravity = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ZoneAnomalyEffectNoGravityComponent, ZoneAnomalyEntityAddEvent>(OnAdd);
        SubscribeLocalEvent<ZoneAnomalyEffectNoGravityComponent, ZoneAnomalyEntityRemoveEvent>(OnRemove);
    }

    private void OnAdd(Entity<ZoneAnomalyEffectNoGravityComponent> anomaly, ref ZoneAnomalyEntityAddEvent args)
    {
        var grav = EnsureComp<MovementIgnoreGravityComponent>(args.Entity);

        grav.Weightless = true;

        Dirty(args.Entity, grav);

        _gravity.RefreshWeightless(args.Entity, true);

        var speedModifier = EnsureComp<MovementSpeedModifierComponent>(args.Entity);

        speedModifier.WeightlessFriction = anomaly.Comp.WeightlessFriction;
        speedModifier.WeightlessFrictionNoInput = anomaly.Comp.WeightlessFrictionNoInput;
        speedModifier.WeightlessAcceleration = anomaly.Comp.WeightlessAcceleration;

        Dirty(args.Entity, speedModifier);
    }

    private void OnRemove(Entity<ZoneAnomalyEffectNoGravityComponent> anomaly, ref ZoneAnomalyEntityRemoveEvent args)
    {
        if (!HasComp<MovementIgnoreGravityComponent>(args.Entity))
            return;

        RemComp<MovementIgnoreGravityComponent>(args.Entity);

        _gravity.RefreshWeightless(args.Entity);

        var speedModifier = EnsureComp<MovementSpeedModifierComponent>(args.Entity);

        speedModifier.WeightlessFriction = MovementSpeedModifierComponent.DefaultWeightlessFriction;
        speedModifier.WeightlessFrictionNoInput = MovementSpeedModifierComponent.DefaultFrictionNoInput;
        speedModifier.WeightlessAcceleration = MovementSpeedModifierComponent.DefaultWeightlessAcceleration;

        Dirty(args.Entity, speedModifier);
    }
}
