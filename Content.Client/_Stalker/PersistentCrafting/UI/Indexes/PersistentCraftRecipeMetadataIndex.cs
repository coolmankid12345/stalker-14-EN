using System;
using System.Collections.Generic;
using Content.Shared._Stalker.PersistentCrafting;

namespace Content.Client._Stalker.PersistentCrafting.UI.Indexes;

public sealed class PersistentCraftRecipeMetadataIndex
{
    public static readonly PersistentCraftRecipeMetadataIndex Empty = new(
        new Dictionary<string, string>(),
        new Dictionary<string, string>(),
        new Dictionary<string, string>(),
        new Dictionary<string, string>(),
        new Dictionary<string, string>(),
        new Dictionary<string, string>());

    private readonly IReadOnlyDictionary<string, string> _nameById;
    private readonly IReadOnlyDictionary<string, string> _descriptionById;
    private readonly IReadOnlyDictionary<string, string> _categoryIdById;
    private readonly IReadOnlyDictionary<string, string> _subCategoryIdById;
    private readonly IReadOnlyDictionary<string, string> _categoryPathById;
    private readonly IReadOnlyDictionary<string, string> _searchTextById;

    private PersistentCraftRecipeMetadataIndex(
        IReadOnlyDictionary<string, string> nameById,
        IReadOnlyDictionary<string, string> descriptionById,
        IReadOnlyDictionary<string, string> categoryIdById,
        IReadOnlyDictionary<string, string> subCategoryIdById,
        IReadOnlyDictionary<string, string> categoryPathById,
        IReadOnlyDictionary<string, string> searchTextById)
    {
        _nameById = nameById;
        _descriptionById = descriptionById;
        _categoryIdById = categoryIdById;
        _subCategoryIdById = subCategoryIdById;
        _categoryPathById = categoryPathById;
        _searchTextById = searchTextById;
    }

    public static PersistentCraftRecipeMetadataIndex Build(
        IReadOnlyList<PersistentCraftRecipePrototype> recipes,
        Func<PersistentCraftRecipePrototype, string> resolveName,
        Func<PersistentCraftRecipePrototype, string> resolveDescription,
        Func<PersistentCraftRecipePrototype, string> resolveCategoryId,
        Func<PersistentCraftRecipePrototype, string> resolveSubCategoryId,
        Func<string, string, string> resolveCategoryPath,
        Func<int, string> resolveTierLabel)
    {
        var nameById = new Dictionary<string, string>(recipes.Count);
        var descriptionById = new Dictionary<string, string>(recipes.Count);
        var categoryIdById = new Dictionary<string, string>(recipes.Count);
        var subCategoryIdById = new Dictionary<string, string>(recipes.Count);
        var categoryPathById = new Dictionary<string, string>(recipes.Count);
        var searchTextById = new Dictionary<string, string>(recipes.Count);

        for (var i = 0; i < recipes.Count; i++)
        {
            var recipe = recipes[i];
            var name = resolveName(recipe);
            var description = resolveDescription(recipe);
            var categoryId = resolveCategoryId(recipe);
            var subCategoryId = resolveSubCategoryId(recipe);
            var categoryPath = resolveCategoryPath(categoryId, subCategoryId);

            nameById[recipe.ID] = name;
            descriptionById[recipe.ID] = description;
            categoryIdById[recipe.ID] = categoryId;
            subCategoryIdById[recipe.ID] = subCategoryId;
            categoryPathById[recipe.ID] = categoryPath;

            var searchSource = $"{name} {categoryPath} {recipe.Tier} {resolveTierLabel(recipe.Tier)} {description}";
            searchTextById[recipe.ID] = searchSource.ToLowerInvariant();
        }

        return new PersistentCraftRecipeMetadataIndex(
            nameById,
            descriptionById,
            categoryIdById,
            subCategoryIdById,
            categoryPathById,
            searchTextById);
    }

    public bool TryGetName(string recipeId, out string name)
    {
        if (_nameById.TryGetValue(recipeId, out var found))
        {
            name = found;
            return true;
        }

        name = string.Empty;
        return false;
    }

    public bool TryGetDescription(string recipeId, out string description)
    {
        if (_descriptionById.TryGetValue(recipeId, out var found))
        {
            description = found;
            return true;
        }

        description = string.Empty;
        return false;
    }

    public bool TryGetCategoryId(string recipeId, out string categoryId)
    {
        if (_categoryIdById.TryGetValue(recipeId, out var found))
        {
            categoryId = found;
            return true;
        }

        categoryId = string.Empty;
        return false;
    }

    public bool TryGetSubCategoryId(string recipeId, out string subCategoryId)
    {
        if (_subCategoryIdById.TryGetValue(recipeId, out var found))
        {
            subCategoryId = found;
            return true;
        }

        subCategoryId = string.Empty;
        return false;
    }

    public bool TryGetCategoryPath(string recipeId, out string categoryPath)
    {
        if (_categoryPathById.TryGetValue(recipeId, out var found))
        {
            categoryPath = found;
            return true;
        }

        categoryPath = string.Empty;
        return false;
    }

    public bool MatchesSearch(string recipeId, string normalizedQuery)
    {
        if (string.IsNullOrWhiteSpace(normalizedQuery))
            return true;

        return _searchTextById.TryGetValue(recipeId, out var searchText) &&
               searchText.Contains(normalizedQuery);
    }
}
