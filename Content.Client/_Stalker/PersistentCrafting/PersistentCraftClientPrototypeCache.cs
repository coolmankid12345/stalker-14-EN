using System;
using System.Collections.Generic;
using System.Linq;
using Content.Shared._Stalker.PersistentCrafting;
using Robust.Shared.Prototypes;

namespace Content.Client._Stalker.PersistentCrafting;

public sealed class PersistentCraftClientPrototypeCache
{
    private readonly Dictionary<string, PersistentCraftNodePrototype> _nodeById;

    public PersistentCraftBranchRegistry BranchRegistry { get; }
    public IReadOnlyList<PersistentCraftRecipePrototype> AllRecipes { get; }
    public IReadOnlyList<PersistentCraftNodePrototype> AllNodes { get; }
    public IReadOnlyDictionary<string, IReadOnlyList<PersistentCraftRecipePrototype>> RecipesByBranch { get; }
    public IReadOnlyDictionary<string, IReadOnlyList<PersistentCraftRecipePrototype>> RecipesByNode { get; }
    public IReadOnlyDictionary<string, IReadOnlyList<PersistentCraftNodePrototype>> NodesByBranch { get; }

    private PersistentCraftClientPrototypeCache(
        PersistentCraftBranchRegistry branchRegistry,
        IReadOnlyList<PersistentCraftRecipePrototype> allRecipes,
        IReadOnlyList<PersistentCraftNodePrototype> allNodes,
        IReadOnlyDictionary<string, IReadOnlyList<PersistentCraftRecipePrototype>> recipesByBranch,
        IReadOnlyDictionary<string, IReadOnlyList<PersistentCraftRecipePrototype>> recipesByNode,
        IReadOnlyDictionary<string, IReadOnlyList<PersistentCraftNodePrototype>> nodesByBranch,
        Dictionary<string, PersistentCraftNodePrototype> nodeById)
    {
        BranchRegistry = branchRegistry;
        AllRecipes = allRecipes;
        AllNodes = allNodes;
        RecipesByBranch = recipesByBranch;
        RecipesByNode = recipesByNode;
        NodesByBranch = nodesByBranch;
        _nodeById = nodeById;
    }

    public static PersistentCraftClientPrototypeCache Create(IPrototypeManager prototype)
    {
        var branchRegistry = PersistentCraftBranchRegistry.Create(prototype);

        var allRecipes = prototype.EnumeratePrototypes<PersistentCraftRecipePrototype>()
            .OrderBy(recipe => GetBranchOrder(branchRegistry, recipe.Branch))
            .ThenBy(recipe => recipe.Tier)
            .ThenBy(recipe => recipe.ID)
            .ToList();

        var allNodes = prototype.EnumeratePrototypes<PersistentCraftNodePrototype>()
            .OrderBy(node => GetBranchOrder(branchRegistry, node.Branch))
            .ThenBy(node => node.TreeRow >= 0 ? node.TreeRow : int.MaxValue)
            .ThenBy(node => node.TreeColumn >= 0 ? node.TreeColumn : int.MaxValue)
            .ThenBy(node => node.ID)
            .ToList();

        var nodeById = allNodes.ToDictionary(node => node.ID);
        var recipesByBranch = BuildRecipesByBranch(allRecipes, branchRegistry);
        var recipesByNode = BuildRecipesByNode(allRecipes);
        var nodesByBranch = BuildNodesByBranch(allNodes, branchRegistry);

        return new PersistentCraftClientPrototypeCache(
            branchRegistry,
            allRecipes,
            allNodes,
            recipesByBranch,
            recipesByNode,
            nodesByBranch,
            nodeById);
    }

    public bool TryGetNode(string nodeId, out PersistentCraftNodePrototype node)
    {
        if (!string.IsNullOrWhiteSpace(nodeId) &&
            _nodeById.TryGetValue(nodeId, out var found))
        {
            node = found;
            return true;
        }

        node = default!;
        return false;
    }

    private static int GetBranchOrder(PersistentCraftBranchRegistry registry, string branchId)
    {
        return registry.TryGetBranchIndex(branchId, out var index) ? index : int.MaxValue;
    }

    private static Dictionary<string, IReadOnlyList<PersistentCraftRecipePrototype>> BuildRecipesByBranch(
        List<PersistentCraftRecipePrototype> allRecipes,
        PersistentCraftBranchRegistry registry)
    {
        var grouped = allRecipes
            .GroupBy(recipe => recipe.Branch)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<PersistentCraftRecipePrototype>) group.ToList());

        foreach (var branch in registry.OrderedBranchIds)
        {
            if (!grouped.ContainsKey(branch))
                grouped[branch] = Array.Empty<PersistentCraftRecipePrototype>();
        }

        return grouped;
    }

    private static Dictionary<string, IReadOnlyList<PersistentCraftRecipePrototype>> BuildRecipesByNode(
        List<PersistentCraftRecipePrototype> allRecipes)
    {
        return allRecipes
            .GroupBy(recipe => recipe.RequiredNode)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<PersistentCraftRecipePrototype>) group.ToList());
    }

    private static Dictionary<string, IReadOnlyList<PersistentCraftNodePrototype>> BuildNodesByBranch(
        List<PersistentCraftNodePrototype> allNodes,
        PersistentCraftBranchRegistry registry)
    {
        var grouped = allNodes
            .GroupBy(node => node.Branch)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<PersistentCraftNodePrototype>) group.ToList());

        foreach (var branch in registry.OrderedBranchIds)
        {
            if (!grouped.ContainsKey(branch))
                grouped[branch] = Array.Empty<PersistentCraftNodePrototype>();
        }

        return grouped;
    }
}
