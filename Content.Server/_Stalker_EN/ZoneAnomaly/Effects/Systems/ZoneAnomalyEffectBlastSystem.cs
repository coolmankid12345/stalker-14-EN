using System.Numerics;
using Content.Shared._Stalker.ZoneAnomaly;
using Content.Shared._Stalker.ZoneAnomaly.Components;
using Content.Shared._Stalker_EN.ZoneAnomaly.Effects.Components;
using Content.Shared.Whitelist;
using Robust.Server.GameObjects;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;

namespace Content.Server._Stalker_EN.ZoneAnomaly.Effects.Systems;

/// <summary>
/// Handles the blast effect that throws entities outward from anomaly center on charging.
/// </summary>
public sealed class ZoneAnomalyEffectBlastSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly PhysicsSystem _physics = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ZoneAnomalyEffectBlastComponent, ZoneAnomalyComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var blast, out var anomaly, out var xform))
        {
            var currentState = (int)anomaly.State;

            // Detect state transition - reset when entering charging
            if (blast.LastState != currentState)
            {
                blast.LastState = currentState;
                if (anomaly.State == ZoneAnomalyState.Charging)
                {
                    blast.Triggered = false;
                    blast.AccumulatedTime = 0f;
                }
            }

            // Only process during charging state
            if (anomaly.State != ZoneAnomalyState.Charging)
                continue;

            // Already triggered this cycle
            if (blast.Triggered)
                continue;

            // Accumulate time and check delay
            blast.AccumulatedTime += frameTime;
            if (blast.AccumulatedTime < blast.Delay)
                continue;

            // Execute blast
            blast.Triggered = true;
            BlastEntities(uid, blast, xform);
        }
    }

    private void BlastEntities(EntityUid uid, ZoneAnomalyEffectBlastComponent blast, TransformComponent xform)
    {
        var center = _transform.GetWorldPosition(xform);
        var epicenter = _transform.GetMapCoordinates(uid);
        var targets = _lookup.GetEntitiesInRange(epicenter, blast.ThrowRange);

        foreach (var target in targets)
        {
            // Check whitelist
            if (blast.Whitelist is { } whitelist && !_whitelist.IsWhitelistPass(whitelist, target))
                continue;

            if (!TryComp<PhysicsComponent>(target, out var physics) || physics.BodyType == BodyType.Static)
                continue;

            var targetPos = _transform.GetWorldPosition(target);
            var direction = targetPos - center;
            var distance = direction.Length();

            Vector2 normalizedDir;
            if (distance < 0.1f)
            {
                // Entity at center - random scatter direction
                var angle = _random.NextDouble() * Math.PI * 2;
                normalizedDir = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
                distance = 0.1f;
            }
            else
            {
                normalizedDir = direction / distance;
            }

            // Force scales inversely with distance (closer = stronger)
            var forceMult = Math.Max(0.5f, 1f - (distance / blast.ThrowRange));
            var force = normalizedDir * blast.ThrowForce * forceMult * physics.Mass;

            _physics.ApplyLinearImpulse(target, force, body: physics);
        }
    }
}
