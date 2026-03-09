using System.Collections.Frozen;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._RD.Area;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RDAreaSingletonComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public Dictionary<EntProtoId<RDAreaComponent>, EntityUid> Instancies = new();
}

public sealed class RDAreaSystem : RDEntitySystemSingleton<RDAreaSingletonComponent>
{
    [Dependency] private readonly INetManager _net = null!;

    [Dependency] private readonly SharedTransformSystem _transform = null!;
    [Dependency] private readonly SharedMapSystem _map = null!;

    private EntityQuery<RDAreaContainerComponent> _areaGridQuery;
    private EntityQuery<MapGridComponent> _mapGridQuery;

    public override void Initialize()
    {
        _areaGridQuery = GetEntityQuery<RDAreaContainerComponent>();
        _mapGridQuery = GetEntityQuery<MapGridComponent>();
    }

    public bool TryGetArea(
        EntityCoordinates coordinates,
        out Entity<RDAreaComponent> area)
    {
        area = default;

        if (!TryGetArea(coordinates, out EntProtoId<RDAreaComponent> areaId))
            return false;

        if (Inst.Comp.Instancies.TryGetValue(areaId, out var instance))
        {
            area = (instance, Comp<RDAreaComponent>(instance));
            return true;
        }

        if (_net.IsServer)
        {
            var newInstance = Spawn(areaId, MapCoordinates.Nullspace);

            Inst.Comp.Instancies[areaId] = newInstance;
            Dirty();

            area = (newInstance, Comp<RDAreaComponent>(newInstance));
            return true;
        }

        return false;
    }

    public bool TryGetArea(Entity<RDAreaContainerComponent?> entity, Vector2i position,
        out Entity<RDAreaComponent> area)
    {
        area = default;

        if (!TryGetArea(entity, position, out EntProtoId<RDAreaComponent> areaId))
            return false;

        if (Inst.Comp.Instancies.TryGetValue(areaId, out var instance))
        {
            area = (instance, Comp<RDAreaComponent>(instance));
            return true;
        }

        if (_net.IsServer)
        {
            var newInstance = Spawn(areaId, MapCoordinates.Nullspace);

            Inst.Comp.Instancies[areaId] = newInstance;
            Dirty();

            area = (newInstance, Comp<RDAreaComponent>(newInstance));
            return true;
        }

        return false;
    }

    public bool TryGetArea(
        EntityCoordinates coordinates,
        out EntProtoId<RDAreaComponent> area)
    {
        area = default;

        if (_transform.GetGrid(coordinates) is not { } gridId)
            return false;

        if ( !_mapGridQuery.TryComp(gridId, out var grid))
            return false;

        if (!_areaGridQuery.TryComp(gridId, out var containerComponent))
            return false;

        var indices = _map.CoordinatesToTile(gridId, grid, coordinates);
        return TryGetArea((gridId, containerComponent), indices, out area);
    }

    public bool TryGetArea(Entity<RDAreaContainerComponent?> entity, Vector2i position,
        out EntProtoId<RDAreaComponent> area)
    {
        area = default;

        if (!Resolve(entity.Owner, ref entity.Comp, false))
            return false;

        if (entity.Comp.CacheValid && !entity.Comp.CachedAreas.TryGetValue(position, out area))
            return false;

        return entity.Comp.Areas.TryGetValue(position, out area);
    }

    public void AddArea(Entity<RDAreaContainerComponent?> entity, Vector2i position, EntProtoId<RDAreaComponent> area)
    {
        if (!Resolve(entity.Owner, ref entity.Comp, false))
            return;

        entity.Comp.CacheValid = false;
        entity.Comp.Areas.Add(position, area);
        DirtyField(entity.Owner, entity.Comp, nameof(RDAreaContainerComponent.Areas));
    }

    public void RemoveArea(Entity<RDAreaContainerComponent?> entity, Vector2i position)
    {
        if (!Resolve(entity.Owner, ref entity.Comp, false))
            return;

        entity.Comp.CacheValid = false;
        entity.Comp.Areas.Remove(position);
        DirtyField(entity.Owner, entity.Comp, nameof(RDAreaContainerComponent.Areas));
    }

    private void Cache(Entity<RDAreaContainerComponent?> entity)
    {
        if (!Resolve(entity.Owner, ref entity.Comp, false))
            return;

        if (entity.Comp.CacheValid)
        {
            Log.Debug("Skip caching, cache valid");
            return;
        }

        entity.Comp.CacheValid = true;
        entity.Comp.CachedAreas = entity.Comp.Areas.ToFrozenDictionary();
    }
}
