using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Server._Stalker.StationEvents.Components;
using Content.Server.Lightning;
using Content.Shared._Stalker_EN.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Physics;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server._Stalker_EN.Emission;

public sealed class EmissionLightningSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _configurationManager = default!;
    [Dependency] private readonly IRobustRandom _robustRandom = default!;
    [Dependency] private readonly PhysicsSystem _physicsSystem = default!;
    [Dependency] private readonly TransformSystem _transformSystem = default!;
    [Dependency] private readonly LightningSystem _lightningSystem = default!;

    private EntityQuery<StalkerSafeZoneComponent> _safeZoneQuery;
    private EntityQuery<BlowoutTargetComponent> _emissionTargetQuery;

    private bool _doRaycasts = false;

    /// <summary>
    ///     Unit vectors of directions to check when raycasting.
    /// </summary>
    private static readonly Vector2[] RaycastCheckedDirections = [
        Direction.North.ToVec(),
        Direction.West.ToVec(),
        Direction.East.ToVec(),
        Direction.South.ToVec(),

        Direction.NorthEast.ToVec(),
        Direction.NorthWest.ToVec(),
        Direction.SouthEast.ToVec(),
        Direction.SouthWest.ToVec()
    ];

    /// <summary>
    ///     Minimum number of rays that didn't hit anything
    ///         in a raycast check, at which it will be deemed
    ///         that the coordinates being tested are indoors.
    ///
    ///     This is so that you cant so easily just have an open door
    ///         and let lightning in.
    /// </summary>
    private const int RaycastMinimumUnintersectedRays = 2;

    private const float RaycastCheckRange = 6.5f;

    private const int MaximumRetries = 6;

    /// <summary>
    ///     List of map-coordinates of lightning blockers
    ///         and the SQUARED radius of area that they block lightning in,
    ///         categorised by their map.
    /// </summary>
    private Dictionary<
        MapId,
        List<(Vector2, float)>
    > _lightingBlockerMap = new();

    /// <summary>
    ///     Maps where lightning is allowed to spawning.
    /// </summary>
    private HashSet<MapId> _lightningTargetMaps = new();

    public override void Initialize()
    {
        base.Initialize();

        _safeZoneQuery = GetEntityQuery<StalkerSafeZoneComponent>();
        _emissionTargetQuery = GetEntityQuery<BlowoutTargetComponent>();

        _configurationManager.OnValueChanged(STCCVars.EmissionLightningRaycast, (x) => _doRaycasts = x, invokeImmediately: true);

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent args)
    {
        _lightingBlockerMap.Clear();
    }

    /// <summary>
    ///     Refreshes map of lightning blockers.
    /// </summary>
    public void Refresh()
    {
        Clear();
        var stopwatch = Stopwatch.StartNew();

        var otherQuery = EntityQueryEnumerator<MapEmissionLightningTargetComponent, MapComponent>();
        while (otherQuery.MoveNext(out var _, out var mapComponent))
        {
            if (mapComponent.MapId == MapId.Nullspace)
                continue;

            _lightningTargetMaps.Add(mapComponent.MapId);
        }

        var query = EntityQueryEnumerator<EmissionLightningSpawnBlockerComponent, TransformComponent>();
        while (query.MoveNext(out var blockerComponent, out var transformComponent))
        {
            var mapCoordinates = _transformSystem.GetMapCoordinates(transformComponent);
            if (!_lightningTargetMaps.Contains(mapCoordinates.MapId))
                continue;

            if (!_lightingBlockerMap.ContainsKey(mapCoordinates.MapId))
                _lightingBlockerMap.Add(mapCoordinates.MapId, []);

            _lightingBlockerMap[mapCoordinates.MapId].Add((mapCoordinates.Position, blockerComponent.Radius * blockerComponent.Radius));
        }

        stopwatch.Stop();
        Log.Info($"Took {stopwatch.ElapsedMilliseconds}ms to rebuild emission lightning map.");
    }

    /// <summary>
    ///     Frees some memory; clears map of lightning data.
    /// </summary>
    public void Clear()
    {
        _lightingBlockerMap.Clear();
        _lightningTargetMaps.Clear();
    }

    // I dont care that there will naturally be more lightning spawning with more players in an area
    public bool TryGetSpawnedLightningMapCoordinates(EntityUid targetUid, float maximumSpawnRadius, [NotNullWhen(true)] out MapCoordinates? candidateMapCoordinates, float minimumSpawnRadius = 0f)
    {
        var targetTransform = Transform(targetUid);
        var targetMapId = targetTransform.MapID;

        // Map of target is not allowed to have lightning
        if (!_lightningTargetMaps.Contains(targetMapId))
        {
            candidateMapCoordinates = null;
            return false;
        }

        var targetMapCoordinates = _transformSystem.GetMapCoordinates(targetTransform);

        var hasLocalMap = _lightingBlockerMap.TryGetValue(targetMapId, out var localMap);

        var minimumRadiusSq = minimumSpawnRadius * minimumSpawnRadius;
        var maximumRadiusSq = maximumSpawnRadius * maximumSpawnRadius;

        candidateMapCoordinates = new();
        for (var i = 0; i < MaximumRetries; i++)
        {
            var lightningDistance =
                MathF.Sqrt(_robustRandom.NextFloat() * (maximumRadiusSq - minimumRadiusSq) + minimumRadiusSq);

            candidateMapCoordinates =
                targetMapCoordinates.Offset(_robustRandom.NextAngle().ToVec() * lightningDistance);

            // var lightningDistance = maximumSpawnRadius * MathF.Sqrt(_robustRandom.NextFloat());
            // candidateMapCoordinates = targetMapCoordinates.Offset(_robustRandom.NextAngle().ToVec() * lightningDistance);

            var areCoordinatesValid = true;

            if (hasLocalMap) // If this is false, then no blockers are on the target's map, so just use the first value and return. Otherwise do alltis
            {
                foreach (var (blockerWorldPosition, blockerRadiusSq) in localMap!)
                {
                    // compare squared distance with squared blocker radius, no need to sqrt
                    // we are checking if distance(sq) of blocker to candidate position is in blocker radius(sq)
                    if (
                        (candidateMapCoordinates.Value.Position - blockerWorldPosition).LengthSquared() <= blockerRadiusSq)
                    {
                        // candiate position is in a blocker, go to retry
                        areCoordinatesValid = false;
                        break;
                    }
                }
            }

            if (areCoordinatesValid)
            {
                // if we are doing raycasts, and raycast check said coords are indoors, then abort
                if (_doRaycasts &&
                    AreCoordinatesMaybeIndoors(candidateMapCoordinates.Value))
                    continue;

                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Tries to estimate whether some mapcoordinates are
    ///         probably indoors or not.
    /// </summary>
    /// <returns>Whether coords are estimated to be indoors.</returns>
    private bool AreCoordinatesMaybeIndoors(MapCoordinates candidateMapCoordinates)
    {
        var unintersectedRays = 0;
        foreach (var direction in RaycastCheckedDirections)
        {
            var ray = new CollisionRay(candidateMapCoordinates.Position, direction, (int)CollisionGroup.HighImpassable);
            var rayResults = _physicsSystem.IntersectRay(candidateMapCoordinates.MapId, ray, RaycastCheckRange, returnOnFirstHit: true).FirstOrNull();

            // fail upon nothing-hitting-ray
            if (rayResults is not { })
            {
                // too many rays failed so just fail, also increment number of failed rays
                if (++unintersectedRays == RaycastMinimumUnintersectedRays) // >= isnt gonna do anything here
                    return false;
            }
        }

        // every ray had hit something; succeed
        return true;
    }

    // Lightning can only hit if target is: 1. not in safezone, 2. is an emission target
    private bool LightningHitPredicate(in EntityUid uid) => _emissionTargetQuery.HasComponent(uid) && !_safeZoneQuery.HasComponent(uid);

    public void TrySpawnLightningNearby(EntityUid targetUid, float maximumSpawnRadius, EntProtoId emissionLightningEntityId, float boltRange, int boltCount, float minimumSpawnRadius = 0f)
    {
        if (!TryGetSpawnedLightningMapCoordinates(targetUid, maximumSpawnRadius, out var lightningMapCoordinates, minimumSpawnRadius: minimumSpawnRadius))
            return;

        var lightningEntityId = Spawn(emissionLightningEntityId, lightningMapCoordinates.Value);
        _lightningSystem.ShootRandomLightnings(
            lightningEntityId,
            boltRange,
            boltCount,
            lightningPrototype: "EmissionLightningBolt",
            triggerLightningEvents: false,
            predicate: LightningHitPredicate
        );
    }
}
