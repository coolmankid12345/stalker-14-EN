using System;
using System.Collections.Generic;
using System.Linq;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stalker.PersistentCrafting;

public sealed class PersistentCraftBranchRegistry
{
    private readonly List<PersistentCraftBranchPrototype> _orderedBranches;
    private readonly List<string> _orderedBranchIds;
    private readonly Dictionary<string, PersistentCraftBranchPrototype> _byId;
    private readonly Dictionary<string, int> _indexById;

    public IReadOnlyList<PersistentCraftBranchPrototype> OrderedBranches => _orderedBranches;
    public IReadOnlyList<string> OrderedBranchIds => _orderedBranchIds;
    public IReadOnlyDictionary<string, PersistentCraftBranchPrototype> ById => _byId;
    public IReadOnlyDictionary<string, int> IndexById => _indexById;
    public string FirstBranchId => _orderedBranchIds.Count > 0 ? _orderedBranchIds[0] : string.Empty;

    private PersistentCraftBranchRegistry(
        List<PersistentCraftBranchPrototype> orderedBranches,
        List<string> orderedBranchIds,
        Dictionary<string, PersistentCraftBranchPrototype> byId,
        Dictionary<string, int> indexById)
    {
        _orderedBranches = orderedBranches;
        _orderedBranchIds = orderedBranchIds;
        _byId = byId;
        _indexById = indexById;
    }

    public static PersistentCraftBranchRegistry Create(IPrototypeManager prototype)
    {
        var orderedBranches = prototype.EnumeratePrototypes<PersistentCraftBranchPrototype>()
            .OrderBy(branch => branch.Order)
            .ThenBy(branch => branch.ID)
            .ToList();

        var orderedBranchIds = new List<string>(orderedBranches.Count);
        var byId = new Dictionary<string, PersistentCraftBranchPrototype>(orderedBranches.Count);
        var indexById = new Dictionary<string, int>(orderedBranches.Count);

        for (var i = 0; i < orderedBranches.Count; i++)
        {
            var definition = orderedBranches[i];
            orderedBranchIds.Add(definition.ID);
            byId[definition.ID] = definition;
            indexById[definition.ID] = i;
        }

        return new PersistentCraftBranchRegistry(orderedBranches, orderedBranchIds, byId, indexById);
    }

    public bool TryGetBranchDefinition(string branchId, out PersistentCraftBranchPrototype definition)
    {
        if (!string.IsNullOrWhiteSpace(branchId) &&
            _byId.TryGetValue(branchId, out var found))
        {
            definition = found;
            return true;
        }

        definition = default!;
        return false;
    }

    public bool TryGetBranchIndex(string branchId, out int index)
    {
        if (!string.IsNullOrWhiteSpace(branchId) &&
            _indexById.TryGetValue(branchId, out var found))
        {
            index = found;
            return true;
        }

        index = -1;
        return false;
    }

    public int GetBranchIndex(string branchId)
    {
        if (TryGetBranchIndex(branchId, out var index))
            return index;

        throw new ArgumentOutOfRangeException(nameof(branchId), branchId, "Unknown persistent craft branch.");
    }

    public bool TryGetBranchByIndex(int index, out string branchId)
    {
        if ((uint) index < (uint) _orderedBranchIds.Count)
        {
            branchId = _orderedBranchIds[index];
            return true;
        }

        branchId = string.Empty;
        return false;
    }
}
