using System.Threading.Tasks;
using Content.Shared._Stalker.Anomaly.Data;
using Robust.Shared.Map.Components;

namespace Content.Server._Stalker.Anomaly.Generation.Jobs;

public sealed partial class STAnomalyGenerationJob
{
    private async Task<EntityUid> TrySpawn(STAnomalyGeneratorAnomalyEntry anomalyEntry, Vector2i coords)
    {
        if (!_tileCoordinates.TryGetValue(coords, out var tileRef) || !await PlaceFree(anomalyEntry, coords))
            return EntityUid.Invalid;

        var gridComp = _entityManager.EnsureComponent<MapGridComponent>(tileRef.GridUid);
        var targetCoords = _map.GridTileToWorld(tileRef.GridUid, gridComp, tileRef.GridIndices);

        return _entityManager.Spawn(anomalyEntry.ProtoId, targetCoords);
    }

    private async Task<bool> PlaceFree(STAnomalyGeneratorAnomalyEntry anomalyEntry, Vector2i coords)
    {
        var tiles = GetAnomalyTiles(anomalyEntry, coords);
        foreach (var tile in tiles)
        {
            await MakeOperation();

            if (_tileCoordinates.ContainsKey(tile))
                continue;

            return false;
        }

        return true;
    }
}
