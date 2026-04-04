using System;
using Content.Client._Stalker.PersistentCrafting.UI.Indexes;
using Content.Shared._Stalker.PersistentCrafting;
using Robust.Shared.Prototypes;

namespace Content.Client._Stalker.PersistentCrafting.UI.Services;

public sealed class PersistentCraftTextResolver
{
    private readonly IPrototypeManager _prototype;
    private readonly PersistentCraftBranchRegistry _branchRegistry;

    public PersistentCraftRecipeMetadataIndex MetadataIndex { get; set; }

    public PersistentCraftTextResolver(
        IPrototypeManager prototype,
        PersistentCraftBranchRegistry branchRegistry,
        PersistentCraftRecipeMetadataIndex metadataIndex)
    {
        _prototype = prototype;
        _branchRegistry = branchRegistry;
        MetadataIndex = metadataIndex;
    }

    public string ResolveEntityName(string prototypeId)
    {
        return _prototype.TryIndex<EntityPrototype>(prototypeId, out var prototype)
            ? prototype.Name
            : prototypeId;
    }

    public string ResolveRecipeName(PersistentCraftRecipePrototype recipe)
    {
        if (MetadataIndex.TryGetName(recipe.ID, out var cached))
            return cached;

        var displayProto = PersistentCraftingHelper.GetDisplayPrototypeId(recipe);
        if (!string.IsNullOrWhiteSpace(displayProto) &&
            _prototype.TryIndex<EntityPrototype>(displayProto, out var prototype) &&
            !string.IsNullOrWhiteSpace(prototype.Name))
        {
            return prototype.Name;
        }

        return TryLoc(recipe.Name) ?? recipe.Name;
    }

    public string ResolveRecipeDescription(PersistentCraftRecipePrototype recipe)
    {
        if (MetadataIndex.TryGetDescription(recipe.ID, out var cached))
            return cached;

        var displayProto = PersistentCraftingHelper.GetDisplayPrototypeId(recipe);
        if (!string.IsNullOrWhiteSpace(displayProto) &&
            _prototype.TryIndex<EntityPrototype>(displayProto, out var prototype) &&
            !string.IsNullOrWhiteSpace(prototype.Description))
        {
            return prototype.Description;
        }

        return TryLoc(recipe.Description) ?? recipe.Description;
    }

    public string ResolveNodeName(PersistentCraftNodePrototype node)
    {
        if (!string.IsNullOrWhiteSpace(node.Name))
            return TryLoc(node.Name) ?? node.Name;

        if (!string.IsNullOrWhiteSpace(node.DisplayProto) &&
            _prototype.TryIndex<EntityPrototype>(node.DisplayProto, out var prototype) &&
            !string.IsNullOrWhiteSpace(prototype.Name))
        {
            return prototype.Name;
        }

        return TryLoc("persistent-craft-node-sub-recipe-name") ?? node.ID;
    }

    public string ResolveNodeCardCaption(PersistentCraftNodePrototype node)
    {
        var resolved = ResolveNodeName(node).Trim();
        if (!string.IsNullOrWhiteSpace(resolved))
            return resolved;

        if (!string.IsNullOrWhiteSpace(node.DisplayProto))
            return node.DisplayProto;

        return node.ID;
    }

    public string ResolveBranchTitle(string branchId)
    {
        return _branchRegistry.TryGetBranchDefinition(branchId, out var definition)
            ? ResolveBranchTitle(definition)
            : branchId;
    }

    public string ResolveBranchTitle(PersistentCraftBranchPrototype definition)
    {
        return TryLoc(definition.Name) ?? definition.Name;
    }

    public string GetRecipeCategoryId(PersistentCraftRecipePrototype recipe)
    {
        return MetadataIndex.TryGetCategoryId(recipe.ID, out var cached)
            ? cached
            : ComputeRecipeCategoryId(recipe);
    }

    public string GetRecipeSubCategoryId(PersistentCraftRecipePrototype recipe)
    {
        return MetadataIndex.TryGetSubCategoryId(recipe.ID, out var cached)
            ? cached
            : ComputeRecipeSubCategoryId(recipe);
    }

    public string ComputeRecipeCategoryId(PersistentCraftRecipePrototype recipe)
    {
        if (!string.IsNullOrWhiteSpace(recipe.Category))
            return recipe.Category;

        return _branchRegistry.TryGetBranchDefinition(recipe.Branch, out var definition)
            ? definition.DefaultCategory
            : string.Empty;
    }

    public string ComputeRecipeSubCategoryId(PersistentCraftRecipePrototype recipe)
    {
        if (!string.IsNullOrWhiteSpace(recipe.SubCategory))
            return recipe.SubCategory;

        return _branchRegistry.TryGetBranchDefinition(recipe.Branch, out var definition) &&
               definition.UseTierSubcategories
            ? $"tier-{recipe.Tier}"
            : string.Empty;
    }

    public string ResolveCategoryPath(string categoryId, string subCategoryId)
    {
        var categoryName = GetCategoryName(categoryId);
        var subCategoryName = GetSubCategoryName(subCategoryId);

        return string.IsNullOrWhiteSpace(subCategoryName)
            ? categoryName
            : $"{categoryName} / {subCategoryName}";
    }

    public string GetCategoryName(string categoryId)
    {
        var category = GetCategoryPrototype(categoryId);
        if (category != null)
            return TryLoc(category.Name) ?? category.Name;

        var locKey = $"persistent-craft-category-{categoryId}";
        return TryLoc(locKey) ?? HumanizeIdentifier(categoryId);
    }

    public string GetSubCategoryName(string subCategoryId)
    {
        if (string.IsNullOrWhiteSpace(subCategoryId))
            return string.Empty;

        var subCategory = GetSubCategoryPrototype(subCategoryId);
        if (subCategory != null)
            return TryLoc(subCategory.Name) ?? subCategory.Name;

        if (subCategoryId.StartsWith("tier-") &&
            subCategoryId.Length > "tier-".Length &&
            int.TryParse(subCategoryId.Substring("tier-".Length), out var tier))
        {
            return $"{Loc.GetString("persistent-craft-level-label")} {PersistentCraftingHelper.GetTierDisplayLabel(tier)}";
        }

        var locKey = $"persistent-craft-subcategory-{subCategoryId}";
        return TryLoc(locKey) ?? HumanizeIdentifier(subCategoryId);
    }

    public int GetCategoryOrder(string categoryId)
    {
        return GetCategoryPrototype(categoryId)?.Order ?? 99;
    }

    public int GetSubCategoryOrder(string subCategoryId)
    {
        return GetSubCategoryPrototype(subCategoryId)?.Order
               ?? (subCategoryId.StartsWith("tier-") &&
                   subCategoryId.Length > "tier-".Length &&
                   int.TryParse(subCategoryId.Substring("tier-".Length), out var tier)
                   ? tier
                   : 99);
    }

    public string GetRecipeCategoryPath(PersistentCraftRecipePrototype recipe)
    {
        return MetadataIndex.TryGetCategoryPath(recipe.ID, out var cached)
            ? cached
            : ResolveCategoryPath(GetRecipeCategoryId(recipe), GetRecipeSubCategoryId(recipe));
    }

    public string BuildRecipeSecondaryLine(PersistentCraftRecipePrototype recipe)
    {
        var category = GetSubCategoryName(GetRecipeSubCategoryId(recipe));
        if (string.IsNullOrWhiteSpace(category))
            category = GetCategoryName(GetRecipeCategoryId(recipe));

        return $"{PersistentCraftingHelper.GetTierDisplayLabel(recipe.Tier)} | {category}";
    }

    public bool MatchesRecipeSearch(PersistentCraftRecipePrototype recipe, string normalizedQuery)
    {
        if (string.IsNullOrWhiteSpace(normalizedQuery))
            return true;

        if (MetadataIndex.MatchesSearch(recipe.ID, normalizedQuery))
            return true;

        var fallback = $"{ResolveRecipeName(recipe)} {GetRecipeCategoryPath(recipe)} {recipe.Tier} {PersistentCraftingHelper.GetTierDisplayLabel(recipe.Tier)} {ResolveRecipeDescription(recipe)}"
            .ToLowerInvariant();
        return fallback.Contains(normalizedQuery);
    }

    public string FormatIngredientName(PersistentCraftIngredient ingredient)
    {
        var name = ingredient.GetSelectorKind() switch
        {
            PersistentCraftIngredientSelectorKind.Proto => ResolveEntityName(ingredient.Proto ?? string.Empty),
            PersistentCraftIngredientSelectorKind.StackType => ingredient.StackType ?? string.Empty,
            PersistentCraftIngredientSelectorKind.Tag => FormatTagIngredientName(ingredient.Tag ?? string.Empty),
            _ => "?",
        };

        return $"{name} x{ingredient.Amount}";
    }

    public string FormatResult(PersistentCraftResult result)
    {
        return $"{ResolveEntityName(result.Proto)} x{result.Amount}";
    }

    private PersistentCraftCategoryPrototype? GetCategoryPrototype(string categoryId)
    {
        return !string.IsNullOrWhiteSpace(categoryId) &&
               _prototype.TryIndex<PersistentCraftCategoryPrototype>(categoryId, out var category)
            ? category
            : null;
    }

    private PersistentCraftSubCategoryPrototype? GetSubCategoryPrototype(string subCategoryId)
    {
        return !string.IsNullOrWhiteSpace(subCategoryId) &&
               _prototype.TryIndex<PersistentCraftSubCategoryPrototype>(subCategoryId, out var subCategory)
            ? subCategory
            : null;
    }

    private static string? TryLoc(string locKey)
    {
        if (string.IsNullOrWhiteSpace(locKey))
            return null;

        try
        {
            return Loc.GetString(locKey);
        }
        catch (Exception ex)
        {
            Logger.Warning($"[PersistentCraft] Missing loc key '{locKey}': {ex.Message}");
            return null;
        }
    }

    private static string HumanizeIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value.Replace('-', ' ').Replace('_', ' ').Trim();
        if (normalized.Length == 0)
            return string.Empty;

        var first = normalized.Substring(0, 1).ToUpperInvariant();
        if (normalized.Length == 1)
            return first;

        return first + normalized.Substring(1);
    }

    private static string FormatTagIngredientName(string tag)
    {
        // Пробуем найти локализацию по ключу persistent-craft-tag-{tag}.
        // Для добавления красивого имени к любому тегу достаточно добавить ключ в .ftl файл.
        var tagLoc = TryLoc($"persistent-craft-tag-{tag}");
        if (!string.IsNullOrWhiteSpace(tagLoc))
            return tagLoc;

        return HumanizeIdentifier(tag);
    }
}
