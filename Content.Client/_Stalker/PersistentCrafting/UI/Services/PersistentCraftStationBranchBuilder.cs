using System;
using System.Collections.Generic;
using Content.Client._Stalker.PersistentCrafting.UI.ViewModels;
using Content.Shared._Stalker.PersistentCrafting;

namespace Content.Client._Stalker.PersistentCrafting.UI.Services;

public static class PersistentCraftStationBranchBuilder
{
    public static PersistentCraftStationBranchData Build(
        string branch,
        IReadOnlyList<PersistentCraftRecipePrototype> branchRecipes,
        PersistentCraftState state,
        PersistentCraftStationViewModel viewModel,
        Func<PersistentCraftRecipePrototype, bool> hasRequirement,
        Func<PersistentCraftRecipePrototype, bool> hasLocalMaterials,
        Func<PersistentCraftRecipePrototype, string, bool> matchesSearch)
    {
        var unlockedRecipes = state.Loaded
            ? FilterUnlockedRecipes(branchRecipes, hasRequirement)
            : (IReadOnlyList<PersistentCraftRecipePrototype>) Array.Empty<PersistentCraftRecipePrototype>();

        var selectedTier = GetSelectedTierFilter(branch, unlockedRecipes, viewModel);
        var searchText = viewModel.GetSearchText(branch);
        var craftableOnly = viewModel.GetCraftableOnly(branch);

        // Каждый фильтр возвращает исходный список если фильтрация не нужна — без копирования.
        var filteredRecipes = selectedTier > 0
            ? FilterRecipesByTier(unlockedRecipes, selectedTier)
            : unlockedRecipes;

        filteredRecipes = ApplyRecipeSearch(filteredRecipes, searchText, matchesSearch);
        var craftabilityByRecipeId = IndexRecipeCraftability(filteredRecipes, hasLocalMaterials, out var craftableCount);
        filteredRecipes = ApplyCraftableFilter(filteredRecipes, craftableOnly, craftabilityByRecipeId);

        var selectedRecipe = ResolveSelectedRecipe(branch, filteredRecipes, viewModel);
        return new PersistentCraftStationBranchData(
            branchRecipes,
            unlockedRecipes,
            filteredRecipes,
            craftabilityByRecipeId,
            selectedRecipe,
            selectedTier,
            craftableCount,
            searchText,
            craftableOnly);
    }

    private static IReadOnlyList<PersistentCraftRecipePrototype> FilterUnlockedRecipes(
        IReadOnlyList<PersistentCraftRecipePrototype> recipes,
        Func<PersistentCraftRecipePrototype, bool> hasRequirement)
    {
        var unlocked = new List<PersistentCraftRecipePrototype>(recipes.Count);
        for (var i = 0; i < recipes.Count; i++)
        {
            var recipe = recipes[i];
            if (hasRequirement(recipe))
                unlocked.Add(recipe);
        }

        return unlocked;
    }

    private static IReadOnlyList<PersistentCraftRecipePrototype> FilterRecipesByTier(
        IReadOnlyList<PersistentCraftRecipePrototype> recipes,
        int tier)
    {
        var filtered = new List<PersistentCraftRecipePrototype>(recipes.Count);
        for (var i = 0; i < recipes.Count; i++)
        {
            var recipe = recipes[i];
            if (recipe.Tier == tier)
                filtered.Add(recipe);
        }

        return filtered;
    }

    private static IReadOnlyList<PersistentCraftRecipePrototype> ApplyRecipeSearch(
        IReadOnlyList<PersistentCraftRecipePrototype> recipes,
        string searchText,
        Func<PersistentCraftRecipePrototype, string, bool> matchesSearch)
    {
        var query = searchText.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(query))
            return recipes; // нет запроса — нет копии

        var filtered = new List<PersistentCraftRecipePrototype>(recipes.Count);
        for (var i = 0; i < recipes.Count; i++)
        {
            var recipe = recipes[i];
            if (matchesSearch(recipe, query))
                filtered.Add(recipe);
        }

        return filtered;
    }

    private static Dictionary<string, bool> IndexRecipeCraftability(
        IReadOnlyList<PersistentCraftRecipePrototype> recipes,
        Func<PersistentCraftRecipePrototype, bool> hasLocalMaterials,
        out int craftableCount)
    {
        craftableCount = 0;
        var craftabilityByRecipeId = new Dictionary<string, bool>(recipes.Count);
        for (var i = 0; i < recipes.Count; i++)
        {
            var recipe = recipes[i];
            var hasMaterials = hasLocalMaterials(recipe);
            craftabilityByRecipeId[recipe.ID] = hasMaterials;

            if (hasMaterials)
                craftableCount++;
        }

        return craftabilityByRecipeId;
    }

    private static IReadOnlyList<PersistentCraftRecipePrototype> ApplyCraftableFilter(
        IReadOnlyList<PersistentCraftRecipePrototype> recipes,
        bool craftableOnly,
        IReadOnlyDictionary<string, bool> craftabilityByRecipeId)
    {
        if (!craftableOnly)
            return recipes; // фильтр выключен — нет копии

        var filtered = new List<PersistentCraftRecipePrototype>(recipes.Count);
        for (var i = 0; i < recipes.Count; i++)
        {
            var recipe = recipes[i];
            if (craftabilityByRecipeId.TryGetValue(recipe.ID, out var hasMaterials) &&
                hasMaterials)
            {
                filtered.Add(recipe);
            }
        }

        return filtered;
    }

    private static PersistentCraftRecipePrototype? ResolveSelectedRecipe(
        string branch,
        IReadOnlyList<PersistentCraftRecipePrototype> recipes,
        PersistentCraftStationViewModel viewModel)
    {
        if (recipes.Count == 0)
            return null;

        if (viewModel.TryGetSelectedRecipe(branch, out var selectedId))
        {
            for (var i = 0; i < recipes.Count; i++)
            {
                var recipe = recipes[i];
                if (recipe.ID == selectedId)
                    return recipe;
            }
        }

        var fallback = recipes[0];
        viewModel.SetSelectedRecipe(branch, fallback.ID);
        return fallback;
    }

    private static int GetSelectedTierFilter(
        string branch,
        IReadOnlyList<PersistentCraftRecipePrototype> recipes,
        PersistentCraftStationViewModel viewModel)
    {
        if (viewModel.TryGetSelectedTierFilter(branch, out var selectedTier))
        {
            if (selectedTier == 0)
                return selectedTier;

            for (var i = 0; i < recipes.Count; i++)
            {
                if (recipes[i].Tier == selectedTier)
                    return selectedTier;
            }
        }

        var preferredTier = 0;
        var maxTier = int.MinValue;
        for (var i = 0; i < recipes.Count; i++)
        {
            if (recipes[i].Tier > maxTier)
                maxTier = recipes[i].Tier;
        }

        if (maxTier > int.MinValue)
            preferredTier = maxTier;

        viewModel.SetSelectedTierFilter(branch, preferredTier);
        return preferredTier;
    }
}
