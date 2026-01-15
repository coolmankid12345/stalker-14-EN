using Content.Shared._Stalker.ZoneAnomaly;
using Content.Shared._Stalker.ZoneAnomaly.Components;
using Content.Shared._Stalker.ZoneAnomaly.Effects.Components;
using Content.Shared._Stalker_EN.ZoneAnomaly.Effects.Components;
using Content.Shared.Standing;
using Content.Shared.Whitelist;
using Robust.Server.GameObjects;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;
using System.Numerics;

namespace Content.Server._Stalker.ZoneAnomaly.Effects.Systems;

public sealed class ZoneAnomalyEffectGravityWellSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly PhysicsSystem _physics = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelistSystem = default!;

    private EntityQuery<PhysicsComponent> _physicsQuery;
    private EntityQuery<TransformComponent> _transformQuery;

    public override void Initialize()
    {
        base.Initialize();

        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        _transformQuery = GetEntityQuery<TransformComponent>();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ZoneAnomalyEffectGravityWellComponent, ZoneAnomalyComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var effect, out var anomaly, out _))
        {
            if (anomaly.State != ZoneAnomalyState.Activated)
                continue;

            if (effect.PeriodTime > _timing.CurTime)
                continue;

            GravPulse((uid, effect));
            effect.PeriodTime = _timing.CurTime + effect.Period;
        }
    }

    private void GravPulse(Entity<ZoneAnomalyEffectGravityWellComponent> effect)
    {
        var epicenter = _transform.GetMapCoordinates(effect);
        var targets = _lookup.GetEntitiesInRange(epicenter, effect.Comp.Distance);
        var bodyQuery = GetEntityQuery<PhysicsComponent>();
        var xformQuery = GetEntityQuery<TransformComponent>();

        foreach (var entity in targets)
        {
            if (effect.Comp.Whitelist is { } whitelist && !_whitelistSystem.IsWhitelistPass(whitelist, entity))
                continue;

            if (!bodyQuery.TryGetComponent(entity, out var physics) || physics.BodyType == BodyType.Static)
                continue;

            var entityPosition = _transform.GetWorldPosition(entity, xformQuery);
            var displacement = epicenter.Position - entityPosition;
            var distance = displacement.Length();

            if (distance == 0)
                continue; // Avoid division by zero

            // Skip pulling entities already in gib core radius
            if (TryComp<ZoneAnomalyEffectGibComponent>(effect.Owner, out var gib) &&
                distance <= gib.CoreRadius)
            {
                continue;
            }

            // Normalized vector pointing towards the epicenter
            var radialDirection = displacement / distance;

            // **Adjust radial direction based on mode**
            if (effect.Comp.Mode == ZoneAnomalyEffectGravityWellMode.Repel)
            {
                // Reverse the radial direction for repulsion
                radialDirection = -radialDirection;
            }

            // Perpendicular vector for tangential force (rotating around the center)
            var tangentialDirection = new Vector2(-radialDirection.Y, radialDirection.X);

            // Calculate scaling factor based on distance and gradient
            var scaling = GetScaling(effect, distance);

            // Calculate radial and tangential forces
            var radialForce = radialDirection * effect.Comp.Radial * scaling;
            var tangentialForce = tangentialDirection * effect.Comp.Tangential * scaling;

            // Reduce force for prone entities
            var forceMultiplier = 1.0f;
            if (TryComp<StandingStateComponent>(entity, out var standing) && !standing.Standing)
            {
                forceMultiplier = 0.5f;
            }

            // Total force
            var totalForce = (radialForce + tangentialForce) * physics.Mass * forceMultiplier;

            // Apply the impulse to the entity
            _physics.ApplyLinearImpulse(entity, totalForce, body: physics);
        }
    }

    private float GetScaling(Entity<ZoneAnomalyEffectGravityWellComponent> effect, float distance)
    {
        var maxDistance = effect.Comp.Distance;

        // Clamp distance to maxDistance to prevent scaling beyond the effect's range
        distance = Math.Min(distance, maxDistance);

        switch (effect.Comp.Gradient)
        {
            case ZoneAnomalyEffectGravityWellGradient.Linear:
                // Scaling increases linearly with distance
                return distance / maxDistance;
            case ZoneAnomalyEffectGravityWellGradient.ReversedLinear:
                // Scaling decreases linearly with distance
                return 1f - (distance / maxDistance);
            default:
                // Default to constant scaling
                return 1f;
        }
    }
}
