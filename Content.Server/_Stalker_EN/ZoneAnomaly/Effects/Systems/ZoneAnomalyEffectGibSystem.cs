using System.Numerics;
using Content.Server.Body.Systems;
using Content.Shared._Stalker.ZoneAnomaly;
using Content.Shared._Stalker.ZoneAnomaly.Components;
using Content.Shared._Stalker_EN.ZoneAnomaly.Effects;
using Content.Shared._Stalker_EN.ZoneAnomaly.Effects.Components;
using Content.Shared.Body.Components;
using Content.Shared.Stunnable;
using Content.Shared.Whitelist;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using ZoneAnomalySystem = Content.Shared._Stalker.ZoneAnomaly.ZoneAnomalySystem;

namespace Content.Server._Stalker_EN.ZoneAnomaly.Effects.Systems;

/// <summary>
/// Handles the delayed gib effect for vortex-type anomalies.
/// </summary>
/// <remarks>
/// When an entity reaches the anomaly's core radius, it becomes "doomed":
/// <list type="number">
///   <item>Entity is paralyzed (stunned + knocked down)</item>
///   <item>Entity is teleported to the exact center</item>
///   <item>Entity is pinned at center until the gib timer expires</item>
///   <item>Entity is gibbed and nearby objects are thrown outward</item>
/// </list>
/// The gib timer continues processing even if the anomaly state changes,
/// ensuring doomed entities are always gibbed once marked.
/// </remarks>
public sealed class ZoneAnomalyEffectGibSystem : EntitySystem
{
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelistSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly PhysicsSystem _physics = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly ZoneAnomalySystem _zoneAnomaly = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ZoneAnomalyEffectGibComponent, ZoneAnomalyComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var gib, out var anomaly, out var xform))
        {
            var anomalyPos = _transform.GetWorldPosition(xform);
            var anyNewDoomed = false;
            var anyGibbed = false;

            // Phase 1: Track entities in core (escapable doom timer)
            // Entities can escape if they leave core radius before timer expires
            if (anomaly.State == ZoneAnomalyState.Activated)
            {
                gib.PendingRemovalBuffer.Clear();

                // Check all entities in anomaly range
                foreach (var entityUid in anomaly.InAnomaly)
                {
                    // Skip if already doomed (being gibbed)
                    if (gib.DoomedEntities.ContainsKey(entityUid))
                        continue;

                    // Check whitelist
                    if (gib.Whitelist is { } whitelist &&
                        !_whitelistSystem.IsWhitelistPass(whitelist, entityUid))
                        continue;

                    // Must have a body to gib
                    if (!HasComp<BodyComponent>(entityUid))
                        continue;

                    // Check distance to core
                    var entityPos = _transform.GetWorldPosition(entityUid);
                    var distance = (anomalyPos - entityPos).Length();
                    var inCore = distance <= gib.CoreRadius;

                    if (inCore)
                    {
                        // Entity is in core - start or continue doom timer
                        if (!gib.PendingDoom.ContainsKey(entityUid))
                        {
                            // Just entered core - start timer
                            gib.PendingDoom[entityUid] = _timing.CurTime + gib.GibDelay;

                            // Visual feedback: show "doomed" sprite when first entity enters pending
                            if (gib.PendingDoom.Count == 1)
                                _appearance.SetData(uid, ZoneAnomalyGibVisuals.Doomed, true);

                            // Audio feedback: play warning sound
                            if (gib.PendingDoomSound != null)
                                _audio.PlayPvs(gib.PendingDoomSound, uid);
                        }
                        else if (_timing.CurTime >= gib.PendingDoom[entityUid])
                        {
                            // Timer expired while in core - DOOMED!
                            gib.DoomedEntities[entityUid] = _timing.CurTime; // Gib immediately
                            gib.PendingDoom.Remove(entityUid);
                            anyNewDoomed = true;

                            // Paralyze to prevent escape
                            var stunDuration = TimeSpan.FromSeconds(2);
                            _stun.TryParalyze(entityUid, stunDuration, true);
                        }
                        // else: still waiting, keep pulling them
                    }
                    else
                    {
                        // Entity escaped core - remove from pending
                        if (gib.PendingDoom.ContainsKey(entityUid))
                        {
                            gib.PendingRemovalBuffer.Add(entityUid);
                        }
                    }
                }

                // Clean up escaped entities
                foreach (var escaped in gib.PendingRemovalBuffer)
                {
                    gib.PendingDoom.Remove(escaped);
                }

                // Check if we should enter grace period after escapes
                if (gib.PendingRemovalBuffer.Count > 0)
                {
                    TryEnterGracePeriod(uid, gib, anomaly);
                }

                // Check grace period even if no escapes occurred
                // This handles the case where anomaly activated but no valid targets entered core
                if (gib.PendingDoom.Count == 0 && gib.DoomedEntities.Count == 0)
                {
                    TryEnterGracePeriod(uid, gib, anomaly);
                }
            }

            // Phase 2: ALWAYS process gib timers if there are doomed entities
            // (even if anomaly state changed to Charging/Idle)
            if (gib.DoomedEntities.Count > 0)
            {
                gib.GibRemovalBuffer.Clear();

                foreach (var (entityUid, gibTime) in gib.DoomedEntities)
                {
                    // Check if entity was deleted
                    if (Deleted(entityUid))
                    {
                        gib.GibRemovalBuffer.Add(entityUid);
                        continue;
                    }

                    // Check if timer expired
                    if (_timing.CurTime < gibTime)
                        continue;

                    // Time to gib!
                    if (TryComp<BodyComponent>(entityUid, out var body))
                    {
                        _body.GibBody(entityUid, gib.GibOrgans, body);
                        anyGibbed = true;
                    }

                    gib.GibRemovalBuffer.Add(entityUid);
                }

                // Remove gibbed/deleted entities from tracking
                foreach (var entityUid in gib.GibRemovalBuffer)
                {
                    gib.DoomedEntities.Remove(entityUid);
                }

                // Throw nearby entities after gibbing (skip doomed ones)
                if (anyGibbed && gib.ThrowOnGib)
                {
                    ThrowNearbyEntities(uid, gib, anomalyPos);
                }

                // Check if we should enter grace period after gib
                if (anyGibbed)
                {
                    TryEnterGracePeriod(uid, gib, anomaly);
                }

                // Pin remaining doomed entities to center
                foreach (var entityUid in gib.DoomedEntities.Keys)
                {
                    if (Deleted(entityUid))
                        continue;

                    // Keep them at center
                    _transform.SetWorldPosition(entityUid, anomalyPos);

                    // Zero velocity so they don't drift
                    if (TryComp<PhysicsComponent>(entityUid, out var physics))
                    {
                        _physics.SetLinearVelocity(entityUid, Vector2.Zero, body: physics);
                    }
                }
            }

            // Phase 3: Update appearance based on pending + doomed count (only when changed)
            var hasDanger = gib.DoomedEntities.Count > 0 || gib.PendingDoom.Count > 0;
            if (hasDanger != gib.LastDangerState)
            {
                gib.LastDangerState = hasDanger;
                _appearance.SetData(uid, ZoneAnomalyGibVisuals.Doomed, hasDanger);
            }
        }
    }

    /// <summary>
    /// Throws all nearby non-static entities away from the center point.
    /// </summary>
    /// <remarks>
    /// Entities at or very near the center receive a random direction.
    /// Force is scaled by distance (closer = stronger) and entity mass.
    /// </remarks>
    private void ThrowNearbyEntities(EntityUid anomalyUid, ZoneAnomalyEffectGibComponent gib, Vector2 center)
    {
        var epicenter = _transform.GetMapCoordinates(anomalyUid);
        var targets = _lookup.GetEntitiesInRange(epicenter, gib.ThrowRange);

        foreach (var target in targets)
        {
            // Skip entities that are still doomed (they're pinned anyway)
            if (gib.DoomedEntities.ContainsKey(target))
                continue;

            if (!TryComp<PhysicsComponent>(target, out var physics) || physics.BodyType == BodyType.Static)
                continue;

            var targetPos = _transform.GetWorldPosition(target);
            var direction = targetPos - center;
            var distance = direction.Length();

            Vector2 normalizedDir;
            if (distance < 0.1f)
            {
                // Entity is at/near center - give random direction to scatter
                var angle = _random.NextDouble() * Math.PI * 2;
                normalizedDir = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
                distance = 0.1f; // Treat as very close for force calculation
            }
            else
            {
                normalizedDir = direction / distance;
            }

            var forceMult = Math.Max(0.5f, 1f - (distance / gib.ThrowRange));
            var force = normalizedDir * gib.ThrowForce * forceMult * physics.Mass;

            _physics.ApplyLinearImpulse(target, force, body: physics);
        }
    }

    /// <summary>
    /// Checks if the anomaly should enter grace period.
    /// Triggers when: no pending doom, no doomed entities, and no valid targets in range.
    /// </summary>
    private void TryEnterGracePeriod(
        EntityUid uid,
        ZoneAnomalyEffectGibComponent gib,
        ZoneAnomalyComponent anomaly)
    {
        // Only enter grace period from Activated state
        if (anomaly.State != ZoneAnomalyState.Activated)
            return;

        // Must have no entities being processed
        if (gib.PendingDoom.Count > 0 || gib.DoomedEntities.Count > 0)
            return;

        // Respect the configured activation delay as minimum duration
        if (_timing.CurTime < anomaly.ActivationTime)
            return;

        // Check for remaining valid targets in range
        gib.StaleEntityBuffer.Clear();
        foreach (var entityUid in anomaly.InAnomaly)
        {
            // Clean up stale (deleted) entities
            if (Deleted(entityUid))
            {
                gib.StaleEntityBuffer.Add(entityUid);
                continue;
            }

            if (gib.Whitelist is { } whitelist &&
                !_whitelistSystem.IsWhitelistPass(whitelist, entityUid))
                continue;

            if (!HasComp<BodyComponent>(entityUid))
                continue;

            // Found a valid target - don't enter grace period
            return;
        }

        // Remove stale entities from tracking
        foreach (var stale in gib.StaleEntityBuffer)
        {
            anomaly.InAnomaly.Remove(stale);
        }

        // No pending, no doomed, no valid targets → grace period
        _zoneAnomaly.TryRecharge((uid, anomaly));
    }
}
