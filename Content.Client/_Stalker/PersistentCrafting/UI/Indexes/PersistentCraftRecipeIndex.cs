using System;
using System.Collections.Generic;
using Content.Client._Stalker.PersistentCrafting;
using Content.Shared._Stalker.PersistentCrafting;

namespace Content.Client._Stalker.PersistentCrafting.UI.Indexes;

public sealed class PersistentCraftRecipeIndex
{
    private static readonly IReadOnlyList<PersistentCraftRecipePrototype> EmptyRecipes = Array.Empty<PersistentCraftRecipePrototype>();

    private readonly IReadOnlyDictionary<string, IReadOnlyList<PersistentCraftRecipePrototype>> _recipesByBranch;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<PersistentCraftRecipePrototype>> _recipesByRequiredNode;
    private readonly IReadOnlyDictionary<string, PersistentCraftRecipePrototype> _recipesById;

    private PersistentCraftRecipeIndex(
        IReadOnlyDictionary<string, IReadOnlyList<PersistentCraftRecipePrototype>> recipesByBranch,
        IReadOnlyDictionary<string, IReadOnlyList<PersistentCraftRecipePrototype>> recipesByRequiredNode,
        IReadOnlyDictionary<string, PersistentCraftRecipePrototype> recipesById)
    {
        _recipesByBranch = recipesByBranch;
        _recipesByRequiredNode = recipesByRequiredNode;
        _recipesById = recipesById;
    }

    public static PersistentCraftRecipeIndex Create(PersistentCraftClientPrototypeCache prototypeCache)
    {
        var recipesById = new Dictionary<string, PersistentCraftRecipePrototype>(prototypeCache.AllRecipes.Count);
        for (var i = 0; i < prototypeCache.AllRecipes.Count; i++)
        {
            var recipe = prototypeCache.AllRecipes[i];
            recipesById[recipe.ID] = recipe;
        }

        return new PersistentCraftRecipeIndex(
            prototypeCache.RecipesByBranch,
            prototypeCache.RecipesByNode,
            recipesById);
    }

    public IReadOnlyList<PersistentCraftRecipePrototype> GetByBranch(string branch)
    {
        return _recipesByBranch.TryGetValue(branch, out var recipes)
            ? recipes
            : EmptyRecipes;
    }

    public IReadOnlyList<PersistentCraftRecipePrototype> GetByRequiredNode(string nodeId)
    {
        return _recipesByRequiredNode.TryGetValue(nodeId, out var recipes)
            ? recipes
            : EmptyRecipes;
    }

    public bool TryGetById(string recipeId, out PersistentCraftRecipePrototype recipe)
    {
        if (!string.IsNullOrWhiteSpace(recipeId) &&
            _recipesById.TryGetValue(recipeId, out var found))
        {
            recipe = found;
            return true;
        }

        recipe = default!;
        return false;
    }
}
