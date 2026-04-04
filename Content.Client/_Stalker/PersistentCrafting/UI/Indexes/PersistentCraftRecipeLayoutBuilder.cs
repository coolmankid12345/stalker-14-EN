using System;
using System.Collections.Generic;
using Content.Shared._Stalker.PersistentCrafting;

namespace Content.Client._Stalker.PersistentCrafting.UI.Indexes;

public static class PersistentCraftRecipeLayoutBuilder
{
    public static List<int> CollectSortedTiers(IReadOnlyList<PersistentCraftRecipePrototype> recipes)
    {
        var tiers = new List<int>();
        var seen = new HashSet<int>();

        for (var i = 0; i < recipes.Count; i++)
        {
            var tier = recipes[i].Tier;
            if (seen.Add(tier))
                tiers.Add(tier);
        }

        tiers.Sort();
        return tiers;
    }

    public static List<PersistentCraftRecipeTierGroup> BuildLayout(
        IReadOnlyList<PersistentCraftRecipePrototype> recipes,
        Func<PersistentCraftRecipePrototype, string> resolveCategoryId,
        Func<string, int> resolveCategoryOrder,
        Func<PersistentCraftRecipePrototype, string> resolveSubCategoryId,
        Func<string, int> resolveSubCategoryOrder)
    {
        var byTier = new Dictionary<int, List<PersistentCraftRecipePrototype>>();
        for (var i = 0; i < recipes.Count; i++)
        {
            var recipe = recipes[i];
            if (!byTier.TryGetValue(recipe.Tier, out var tierRecipes))
            {
                tierRecipes = new List<PersistentCraftRecipePrototype>();
                byTier[recipe.Tier] = tierRecipes;
            }

            tierRecipes.Add(recipe);
        }

        var tiers = new List<int>(byTier.Keys);
        tiers.Sort();

        var result = new List<PersistentCraftRecipeTierGroup>(tiers.Count);
        for (var i = 0; i < tiers.Count; i++)
        {
            var tier = tiers[i];
            var categories = BuildCategoryGroups(
                byTier[tier],
                resolveCategoryId,
                resolveCategoryOrder,
                resolveSubCategoryId,
                resolveSubCategoryOrder);
            result.Add(new PersistentCraftRecipeTierGroup(tier, categories));
        }

        return result;
    }

    private static List<PersistentCraftRecipeCategoryGroup> BuildCategoryGroups(
        IReadOnlyList<PersistentCraftRecipePrototype> recipes,
        Func<PersistentCraftRecipePrototype, string> resolveCategoryId,
        Func<string, int> resolveCategoryOrder,
        Func<PersistentCraftRecipePrototype, string> resolveSubCategoryId,
        Func<string, int> resolveSubCategoryOrder)
    {
        var byCategory = new Dictionary<string, List<PersistentCraftRecipePrototype>>();
        for (var i = 0; i < recipes.Count; i++)
        {
            var recipe = recipes[i];
            var categoryId = resolveCategoryId(recipe);
            if (!byCategory.TryGetValue(categoryId, out var categoryRecipes))
            {
                categoryRecipes = new List<PersistentCraftRecipePrototype>();
                byCategory[categoryId] = categoryRecipes;
            }

            categoryRecipes.Add(recipe);
        }

        var categoryIds = new List<string>(byCategory.Keys);
        categoryIds.Sort((left, right) =>
        {
            var orderComparison = resolveCategoryOrder(left).CompareTo(resolveCategoryOrder(right));
            return orderComparison != 0 ? orderComparison : string.CompareOrdinal(left, right);
        });

        var result = new List<PersistentCraftRecipeCategoryGroup>(categoryIds.Count);
        for (var i = 0; i < categoryIds.Count; i++)
        {
            var categoryId = categoryIds[i];
            var subCategories = BuildSubCategoryGroups(
                byCategory[categoryId],
                resolveSubCategoryId,
                resolveSubCategoryOrder);
            result.Add(new PersistentCraftRecipeCategoryGroup(categoryId, subCategories));
        }

        return result;
    }

    private static List<PersistentCraftRecipeSubCategoryGroup> BuildSubCategoryGroups(
        IReadOnlyList<PersistentCraftRecipePrototype> recipes,
        Func<PersistentCraftRecipePrototype, string> resolveSubCategoryId,
        Func<string, int> resolveSubCategoryOrder)
    {
        var bySubCategory = new Dictionary<string, List<PersistentCraftRecipePrototype>>();
        for (var i = 0; i < recipes.Count; i++)
        {
            var recipe = recipes[i];
            var subCategoryId = resolveSubCategoryId(recipe);
            if (!bySubCategory.TryGetValue(subCategoryId, out var subCategoryRecipes))
            {
                subCategoryRecipes = new List<PersistentCraftRecipePrototype>();
                bySubCategory[subCategoryId] = subCategoryRecipes;
            }

            subCategoryRecipes.Add(recipe);
        }

        var subCategoryIds = new List<string>(bySubCategory.Keys);
        subCategoryIds.Sort((left, right) =>
        {
            var orderComparison = resolveSubCategoryOrder(left).CompareTo(resolveSubCategoryOrder(right));
            return orderComparison != 0 ? orderComparison : string.CompareOrdinal(left, right);
        });

        var result = new List<PersistentCraftRecipeSubCategoryGroup>(subCategoryIds.Count);
        for (var i = 0; i < subCategoryIds.Count; i++)
        {
            var subCategoryId = subCategoryIds[i];
            var subCategoryRecipes = bySubCategory[subCategoryId];
            subCategoryRecipes.Sort(static (left, right) => string.CompareOrdinal(left.ID, right.ID));
            result.Add(new PersistentCraftRecipeSubCategoryGroup(subCategoryId, subCategoryRecipes));
        }

        return result;
    }
}

public readonly record struct PersistentCraftRecipeTierGroup(
    int Tier,
    IReadOnlyList<PersistentCraftRecipeCategoryGroup> Categories);

public readonly record struct PersistentCraftRecipeCategoryGroup(
    string CategoryId,
    IReadOnlyList<PersistentCraftRecipeSubCategoryGroup> SubCategories);

public readonly record struct PersistentCraftRecipeSubCategoryGroup(
    string SubCategoryId,
    IReadOnlyList<PersistentCraftRecipePrototype> Recipes);
