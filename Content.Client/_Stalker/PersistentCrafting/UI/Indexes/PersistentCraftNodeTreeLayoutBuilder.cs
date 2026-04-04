using System;
using System.Collections.Generic;
using Content.Shared._Stalker.PersistentCrafting;
using Robust.Shared.Maths;

namespace Content.Client._Stalker.PersistentCrafting.UI.Indexes;

public static class PersistentCraftNodeTreeLayoutBuilder
{
    public static PersistentCraftNodeTreeLayout Build(IReadOnlyList<PersistentCraftNodePrototype> nodes)
    {
        var positions = new Dictionary<string, Vector2i>(nodes.Count);
        var nodeIds = new HashSet<string>(nodes.Count);
        var maxColumn = 0;
        var maxRow = 0;

        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            var position = GetNodeTreePosition(node);
            positions[node.ID] = position;
            nodeIds.Add(node.ID);

            if (position.X > maxColumn)
                maxColumn = position.X;

            if (position.Y > maxRow)
                maxRow = position.Y;
        }

        return new PersistentCraftNodeTreeLayout(positions, nodeIds, maxColumn, maxRow);
    }

    private static Vector2i GetNodeTreePosition(PersistentCraftNodePrototype node)
    {
        if (node.TreeColumn >= 0 && node.TreeRow >= 0)
            return new Vector2i(node.TreeColumn, node.TreeRow);

        // Fallback placement for legacy nodes without explicit tree coordinates.
        var hashSeed = 0;
        for (var i = 0; i < node.ID.Length; i++)
        {
            hashSeed += node.ID[i];
        }

        var localColumn = Math.Abs(hashSeed % 8);
        var localRow = Math.Abs((hashSeed / 3) % 3);
        return new Vector2i(localColumn, localRow);
    }
}

public sealed class PersistentCraftNodeTreeLayout
{
    private readonly HashSet<string> _nodeIds;

    public IReadOnlyDictionary<string, Vector2i> Positions { get; }
    public int MaxColumn { get; }
    public int MaxRow { get; }

    internal PersistentCraftNodeTreeLayout(
        IReadOnlyDictionary<string, Vector2i> positions,
        HashSet<string> nodeIds,
        int maxColumn,
        int maxRow)
    {
        Positions = positions;
        _nodeIds = nodeIds;
        MaxColumn = maxColumn;
        MaxRow = maxRow;
    }

    public bool ContainsNode(string nodeId)
    {
        return _nodeIds.Contains(nodeId);
    }
}
