using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Server._Stalker.Anomaly.Generation.Components;
using Content.Server._Stalker.StationEvents.Components;
using Content.Server.Lightning;
using Content.Shared.GameTicking;
using Content.Shared.Whitelist;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Stalker_EN.Emission;

public sealed class EmissionLightningSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _robustRandom = default!;
    [Dependency] private readonly TransformSystem _transformSystem = default!;
    [Dependency] private readonly LightningSystem _lightningSystem = default!;

    private EntityQuery<StalkerSafeZoneComponent> _safeZoneQuery;
    private EntityQuery<BlowoutTargetComponent> _emissionTargetQuery;

    private const int MaximumRetries = 6;

    private static readonly EntityWhitelist LightningTargetBlacklist = new();

    /// <summary>
    ///     List of map-coordinates of lightning blockers
    ///         and radius of area that they block lightning in,
    ///         categorised by their map.
    /// </summary>
    private Dictionary<
        MapId,
        List<(Vector2, float)>
    > _lightingBlockerMap = new();

    /// <summary>
    ///     Maps where lightning is blocked from spawning.
    /// </summary>
    private HashSet<MapId> _lightningTargetMaps = new();

    public override void Initialize()
    {
        base.Initialize();

        _safeZoneQuery = GetEntityQuery<StalkerSafeZoneComponent>();
        _emissionTargetQuery = GetEntityQuery<BlowoutTargetComponent>();

        LightningTargetBlacklist.Components = [EntityManager.ComponentFactory.GetComponentName<StalkerSafeZoneComponent>()];
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

        var otherQuery = EntityQueryEnumerator<STAnomalyGeneratorTargetComponent, MapComponent>();
        while (otherQuery.MoveNext(out var _, out var mapComponent))
        {
            if (mapComponent.MapId == MapId.Nullspace)
                continue;

            _lightningTargetMaps.Add(mapComponent.MapId);
        }

        var query = EntityQueryEnumerator<STAnomalyGeneratorSpawnBlockerComponent, TransformComponent>();
        while (query.MoveNext(out var blockerComponent, out var transformComponent))
        {
            var mapCoordinates = _transformSystem.GetMapCoordinates(transformComponent);
            if (!_lightningTargetMaps.Contains(mapCoordinates.MapId))
                continue;

            if (!_lightingBlockerMap.ContainsKey(mapCoordinates.MapId))
                _lightingBlockerMap.Add(mapCoordinates.MapId, new());

            _lightingBlockerMap[mapCoordinates.MapId].Add((mapCoordinates.Position, blockerComponent.Size));
        }
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
    public bool TryGetSpawnedLightningMapCoordinates(EntityUid targetUid, float maximumLightningRadius, [NotNullWhen(true)] out MapCoordinates? candidateMapCoordinates)
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

        candidateMapCoordinates = new();
        for (var i = 0; i < MaximumRetries; i++)
        {
            var lightningDistance = maximumLightningRadius * MathF.Sqrt(_robustRandom.NextFloat());
            candidateMapCoordinates = targetMapCoordinates.Offset(_robustRandom.NextAngle().ToVec() * lightningDistance);

            var valid = true;

            if (hasLocalMap)
            {
                foreach (var (blockerWorldPosition, blockerRadius) in localMap!)
                {
                    // compare squared distance with squared blocker radius, no need to sqrt
                    // we are checking if distance(sq) of blocker to candidate position is in blocker radius(sq)
                    if (
                        (candidateMapCoordinates.Value.Position - blockerWorldPosition).LengthSquared() <=
                        blockerRadius * blockerRadius)
                    {
                        // candiate position is in a blocker, go to retry
                        valid = false;
                        break;
                    }
                }
            }
            else // No blockers on the target's map, so just use the first value and return
                return true;

            if (valid)
                return true;
        }

        return false;
    }

    // Lightning can only hit if target is: 1. not in safezone, 2. is an emission target
    private bool LightningHitPredicate(in EntityUid uid) => _emissionTargetQuery.HasComponent(uid) && !_safeZoneQuery.HasComponent(uid);

    public void TrySpawnLightningNearby(EntityUid targetUid, float maximumLightningRadius, EntProtoId emissionLightningEntityId, float boltRange, int boltCount)
    {
        if (!TryGetSpawnedLightningMapCoordinates(targetUid, maximumLightningRadius, out var lightningMapCoordinates))
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
