using Content.Server.Administration;
using Content.Shared._RD.Area;
using Content.Shared.Administration;
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Toolshed;

namespace Content.Server._RD.Area.Commands;

[ToolshedCommand, AdminCommand(AdminFlags.Mapping)]
public sealed partial class AresCommand : ToolshedCommand
{
    private MapSystem? _map;

    [CommandImplementation("save")]
    public void Save([CommandInvocationContext] IInvocationContext ctx)
    {
        _map = GetSys<MapSystem>();

        var gridQuery = GetEntityQuery<MapGridComponent>();

        var query = EntityManager.AllEntityQueryEnumerator<RDAreaComponent, MetaDataComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var metaData, out var xform))
        {
            if (xform.GridUid is not { } gridId || !gridQuery.TryComp(gridId, out var grid))
                continue;

            var areaGrid = EnsureComp<RDAreaContainerComponent>(gridId);
            if (metaData.EntityPrototype is not { } prototype)
            {
                ctx.WriteLine($"{EntityManager.ToPrettyString(uid)} did not have a prototype.");
                continue;
            }

            var indices = _map.TileIndicesFor(gridId, grid, xform.Coordinates);
            var areas = areaGrid.Areas;
            areas[indices] = prototype.ID;
            QDel(uid);
        }
    }

    [CommandImplementation("load")]
    private void Load()
    {
        _map ??= GetSys<MapSystem>();

        var query = EntityManager.AllEntityQueryEnumerator<RDAreaContainerComponent, MapGridComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var areas, out var mapGrid, out var xform))
        {
            foreach (var (position, protoId) in areas.Areas)
            {
                var coordinates = _map.ToCoordinates(uid, position, mapGrid);
                Spawn(protoId, coordinates);
            }
        }
    }
}
