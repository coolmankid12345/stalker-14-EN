using Content.Server._Stalker_EN.MiracleMachine.MiracleMachineComponents;
using Content.Server._Stalker_EN.PsyHelmet;
using Content.Shared.Inventory;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Robust.Shared.Map;
using Robust.Shared.Physics.Events;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Stalker_EN.MiracleMachine;


/// <summary>
/// This handles the spawning of psy ghosts within the range of the psy field. Reused code from SpawnOnApproach so might be slightly scuffed.
/// Keeps track of ghosts and limits them.
/// </summary>
public sealed class MiracleMachineSpawnerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;

    private TimeSpan _miracleMachineSpawnerTime = TimeSpan.Zero;

    private List<EntityUid> _activeGhosts = new List<EntityUid>();

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<MiracleMachineSpawnerComponent, StartCollideEvent>(OnStartCollide);
        SubscribeLocalEvent<MiracleMachineSpawnerComponent, EndCollideEvent>(OnEndCollide);

        _miracleMachineSpawnerTime = _timing.CurTime + TimeSpan.FromSeconds(20);
    }

    public override void Update(float frameTime)
    {
        if(_miracleMachineSpawnerTime >= _timing.CurTime)
            return;

        _activeGhosts.RemoveAll(x => x == EntityUid.Invalid || Deleted(x));

        var query = EntityQueryEnumerator<MiracleMachineSpawnerComponent>();
            while (query.MoveNext(out var uid, out var comp))
            {
                if (comp.Inside.Count == 0)
                    continue;

                foreach (var player in comp.Inside)
                {
                    if (!Exists(player))
                        continue;

                    if (HasComp<PsyHelmetComponent>(player))
                    {
                        if (_inventory.TryGetSlotEntity(player, "ears", out var helmet))
                        {
                            if(HasComp<PsyHelmetComponent>(helmet))
                                continue;
                        }
                        RemCompDeferred<PsyHelmetComponent>(player);
                    }

                    if (_activeGhosts.Count >= 30)
                        break;

                    if (!TryComp<TransformComponent>(player, out var xform))
                        continue;

                    var origin = xform.Coordinates;

                    var spawnCoords = FindValidSpawn(
                        origin,
                        minOffset: 10f,
                        maxOffset: 15f,
                        playerAvoidRadius: 5f);

                    if (spawnCoords == null)
                        continue;

                    var ghost = _random.Pick(comp.Ghosts);
                    var activeGhost = Spawn(ghost, spawnCoords.Value);

                    if (activeGhost == EntityUid.Invalid)
                        continue;

                    _activeGhosts.Add(activeGhost);
                }

            }

        _miracleMachineSpawnerTime = _timing.CurTime + TimeSpan.FromSeconds(20);
    }

    private void OnStartCollide(EntityUid uid, MiracleMachineSpawnerComponent comp, StartCollideEvent args)
    {
        if (TryComp<ActorComponent>(args.OtherEntity, out var actor) && !HasComp<PsyEffectImmuneComponent>(args.OtherEntity))
        {
            if (comp.Inside.Contains(args.OtherEntity))
                return;

            comp.Inside.Add(args.OtherEntity);
        }
    }

    private void OnEndCollide(EntityUid uid, MiracleMachineSpawnerComponent comp, EndCollideEvent args)
    {
        if (!comp.Inside.Contains(args.OtherEntity))
            return;

        comp.Inside.Remove(args.OtherEntity);
    }
    private EntityCoordinates? FindValidSpawn(
        EntityCoordinates origin,
        float minOffset,
        float maxOffset,
        float playerAvoidRadius,
        int maxTries = 15)
    {
        var tries = 0;

        while (tries++ < maxTries)
        {
            var coords = GetRandomOffset(origin, minOffset, maxOffset);

            if (IsBlocked(coords))
                continue;

            if (IsNearPlayer(coords, playerAvoidRadius))
                continue;

            return coords;
        }

        return null;
    }

    private EntityCoordinates GetRandomOffset(EntityCoordinates origin, float min, float max)
    {
        var dist = min + _random.NextFloat() * (max - min);
        var offset = _random.NextAngle().ToVec() * dist;
        return origin.Offset(offset);
    }

    private bool IsBlocked(EntityCoordinates coords)
    {
        var tile = _turf.GetTileRef(coords);
        return tile != null && _turf.IsTileBlocked(tile.Value, CollisionGroup.Impassable);
    }

    private bool IsNearPlayer(EntityCoordinates coords, float radius)
    {
        var actorQuery = GetEntityQuery<ActorComponent>();

        foreach (var uid in _lookup.GetEntitiesInRange(
                     coords,
                     MathF.Max(radius, float.Epsilon),
                     flags: LookupFlags.Approximate | LookupFlags.Dynamic))
        {
            if (actorQuery.HasComponent(uid))
                return true;
        }

        return false;
    }
}
