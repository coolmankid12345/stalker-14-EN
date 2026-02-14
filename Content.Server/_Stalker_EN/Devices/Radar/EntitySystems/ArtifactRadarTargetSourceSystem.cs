using Content.Server._Stalker.ZoneArtifact.Components.Detector;
using Content.Server._Stalker.ZoneArtifact.Components.Spawner;
using Content.Server._Stalker.ZoneArtifact.Systems;
using Content.Shared._Stalker_EN.Devices.Radar;
using Content.Shared._Stalker_EN.Devices.Radar.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components;

namespace Content.Server._Stalker_EN.Devices.Radar.EntitySystems;

/// <summary>
/// System that provides artifact targets to radar displays.
/// Listens for RadarTargetSourceUpdateEvent and adds artifact blips.
/// </summary>
public sealed class ArtifactRadarTargetSourceSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly ZoneArtifactSpawnerSystem _artifactSpawner = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ArtifactRadarTargetSourceComponent, RadarTargetSourceUpdateEvent>(OnRadarUpdate);
    }

    private void OnRadarUpdate(Entity<ArtifactRadarTargetSourceComponent> entity, ref RadarTargetSourceUpdateEvent args)
    {
        var xformQuery = GetEntityQuery<TransformComponent>();

        // Get the grid the user is on for consistent angle calculation
        MapGridComponent? userGrid = null;
        if (args.UserGridUid != null)
            TryComp(args.UserGridUid.Value, out userGrid);

        var entities = _entityLookup.GetEntitiesInRange<ZoneArtifactDetectorTargetComponent>(
            args.UserMapCoords, entity.Comp.DetectionRange);

        foreach (var target in entities)
        {
            if (!target.Comp.Detectable)
                continue;

            if (target.Comp.DetectedLevel > entity.Comp.Level)
                continue;

            var targetXform = xformQuery.GetComponent(target);
            var targetWorldPos = _transform.GetWorldPosition(targetXform, xformQuery);

            var diff = targetWorldPos - args.UserWorldPos;
            var distance = diff.Length();

            // Skip spawners that don't have artifacts ready (empty cooldown spawners)
            if (TryComp<ZoneArtifactSpawnerComponent>(target, out var spawner))
            {
                if (!_artifactSpawner.Ready((target, spawner)))
                    continue;

                // Spawn artifact if player is close enough (like regular detectors do)
                if (distance <= entity.Comp.ActivationDistance)
                {
                    _artifactSpawner.TrySpawn((target, spawner));
                    continue; // Spawner no longer ready - actual artifact will be found next update
                }
            }

            // Calculate angle in grid-local space for consistency across restarts
            float radarAngle;
            if (userGrid != null && args.UserGridUid != null)
            {
                // Convert positions to grid-local coordinates
                var userLocalPos = _map.WorldToLocal(args.UserGridUid.Value, userGrid, args.UserWorldPos);
                var targetLocalPos = _map.WorldToLocal(args.UserGridUid.Value, userGrid, targetWorldPos);
                var localDiff = targetLocalPos - userLocalPos;

                // Angle in grid-local space (consistent regardless of grid rotation)
                var localAngle = new Angle(localDiff);
                radarAngle = (float)(Math.PI / 2 - localAngle.Theta);
            }
            else
            {
                // Fallback to world-space if not on a grid
                var worldAngle = new Angle(diff);
                radarAngle = (float)(Math.PI / 2 - worldAngle.Theta);
            }

            // Normalize to -PI to PI range
            while (radarAngle > MathF.PI)
                radarAngle -= MathF.PI * 2;
            while (radarAngle < -MathF.PI)
                radarAngle += MathF.PI * 2;

            args.Blips.Add(new RadarBlip(
                GetNetEntity(target),
                radarAngle,
                distance,
                target.Comp.DetectedLevel,
                RadarBlipType.Artifact));
        }
    }
}
