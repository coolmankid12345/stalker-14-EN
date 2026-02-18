using System.Numerics;
using System.Runtime.CompilerServices;
using Content.Server._Stalker.ApproachTrigger;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Content.Shared.Trigger;
using Robust.Shared.Map;
using Robust.Shared.Player; // ST14-EN: Addition
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Stalker.SpawnOnApproach;

public sealed class SpawnOnApproachSystem : EntitySystem
{
    [Robust.Shared.IoC.Dependency] private readonly IRobustRandom _random = default!;
    [Robust.Shared.IoC.Dependency] private readonly IGameTiming _timing = default!;
    [Robust.Shared.IoC.Dependency] private readonly TurfSystem _turf = default!;
    [Robust.Shared.IoC.Dependency] private readonly EntityLookupSystem _lookupSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpawnOnApproachComponent, TriggerEvent>(OnTrigger);
        SubscribeLocalEvent<SpawnOnApproachComponent, ComponentInit>(OnInit);
    }

    private void OnInit(Entity<SpawnOnApproachComponent> entity, ref ComponentInit args)
    {
        if (_timing.CurTime < entity.Comp.MinStartAction)
            return;
        // Check components with instant spawn
        if (!entity.Comp.InstantSpawn)
            return;

        SpawnWithOffset(entity);
    }
    private void OnTrigger(Entity<SpawnOnApproachComponent> entity, ref TriggerEvent args)
    {
        if (!entity.Comp.Enabled)
            return;

        if (_timing.CurTime < entity.Comp.MinStartAction)
            return;

        SpawnWithOffset(entity);
    }

    private void SpawnWithOffset(Entity<SpawnOnApproachComponent> entity)
    {
        var comp = entity.Comp;
        if (!_random.Prob(Math.Clamp(comp.Chance, 0f, 1f)))
        {
            if (comp.ShouldTimeoutOnRoll)
            {
                comp.CoolDownTime = _timing.CurTime + TimeSpan.FromSeconds(comp.Cooldown);
                comp.Enabled = false;
            }
            return;
        }

        var xform = Transform(entity);

        var amount = _random.Next(comp.MinAmount, comp.MaxAmount);
        for (var i = 0; i < amount; i++)
        {
            var initialCoords = xform.Coordinates;

            // ST14-EN: Just made this call RandomizeUntilCorrect
            initialCoords = RandomizeUntilCorrect(comp, initialCoords);

            // Randomizing entity
            var proto = _random.Pick(comp.EntProtoIds);
            Spawn(proto, initialCoords);
        }
        if (TryComp<ApproachTriggerComponent>(entity, out var approach))
            approach.Enabled = false;

        comp.CoolDownTime = _timing.CurTime + TimeSpan.FromSeconds(comp.Cooldown);
        comp.Enabled = false;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private EntityCoordinates RandomizeCoords(SpawnOnApproachComponent comp, EntityCoordinates initial)
    {
        var offset = _random.NextFloat(comp.MinOffset, comp.MaxOffset);
        var xOffset = _random.NextFloat(-offset, offset);
        var yOffset = _random.NextFloat(-offset, offset);
        return initial.Offset(new Vector2(xOffset, yOffset));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private EntityCoordinates RandomizeUntilCorrect(SpawnOnApproachComponent comp, EntityCoordinates initial)
    {
        var triesSoFar = 0; // ST14-EN Addition
        var offset = initial;

        // ST14-EN: Made this all `||` instead of `&&`, so you keep retrying if any of these return true
        while (CheckBlocked(offset, comp /* ST14-EN Addition */) || CheckEntities(offset, comp) || CheckPlayerNearby(offset, comp) /* ST14-EN Addition */)
        {
            offset = RandomizeCoords(comp, initial);

            // ST14-EN Addition: infinite-loop check:
            if (++triesSoFar == 15)
                break;
        }

        return offset;
    }

    private bool CheckEntities(EntityCoordinates coords, SpawnOnApproachComponent comp)
    {
        var tile = _turf.GetTileRef(coords);
        if (tile == null)
            return false;

        foreach (var entity in _lookupSystem.GetLocalEntitiesIntersecting(tile.Value, 0f))
        {
            var meta = MetaData(entity);
            if (meta.EntityPrototype == null)
                continue;

            return comp.RestrictedProtos.Contains(meta.EntityPrototype.ID);
        }
        return false;
    }

    // ST14-EN: Addition
    private bool CheckPlayerNearby(in EntityCoordinates coords, SpawnOnApproachComponent comp)
    {
        if (comp.SpawnNearPlayers)
            return false;

        var actorQuery = GetEntityQuery<ActorComponent>();
        foreach (var uid in _lookupSystem.GetEntitiesInRange(coords, comp.MinOffset * 0.67f, flags: LookupFlags.Approximate | LookupFlags.Dynamic))
        {
            if (actorQuery.HasComponent(uid))
                return true;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool CheckBlocked(EntityCoordinates coords, SpawnOnApproachComponent comp /* ST14-EN Addition */)
    {
        if (comp.SpawnInside)
            return false;

        var tile = _turf.GetTileRef(coords);

        return tile != null && _turf.IsTileBlocked(tile.Value, CollisionGroup.Impassable);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<SpawnOnApproachComponent>();
        while (query.MoveNext(out var uid, out var spawner))
        {
            if (spawner.CoolDownTime > _timing.CurTime)
                continue;

            if (TryComp<ApproachTriggerComponent>(uid, out var approach))
                approach.Enabled = true;

            spawner.Enabled = true;
        }
    }
}
