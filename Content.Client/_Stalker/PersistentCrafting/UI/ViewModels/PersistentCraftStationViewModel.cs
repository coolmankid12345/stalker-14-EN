using System.Collections.Generic;
using System.Numerics;

namespace Content.Client._Stalker.PersistentCrafting.UI.ViewModels;

public sealed class PersistentCraftStationViewModel
{
    public Dictionary<string, string> SelectedRecipes { get; } = new();
    public Dictionary<string, int> SelectedTierFilters { get; } = new();
    public Dictionary<string, string> SearchTextByBranch { get; } = new();
    public Dictionary<string, bool> CraftableOnlyByBranch { get; } = new();
    public Dictionary<string, Vector2> ListScrollByBranch { get; } = new();
    public Dictionary<string, Vector2> DetailScrollByBranch { get; } = new();
    public HashSet<string> CollapsedCategoryKeys { get; } = new();
    public HashSet<string> CollapsedSubCategoryKeys { get; } = new();
    public HashSet<string> InitializedCategoryKeys { get; } = new();
    public HashSet<string> InitializedSubCategoryKeys { get; } = new();
    public bool SelectPreferredBranchOnNextUpdate { get; set; } = true;
    public string LastVisibleBranch { get; set; } = string.Empty;

    public bool TryGetSelectedRecipe(string branch, out string recipeId)
    {
        if (SelectedRecipes.TryGetValue(branch, out var found))
        {
            recipeId = found;
            return true;
        }

        recipeId = string.Empty;
        return false;
    }

    public void SetSelectedRecipe(string branch, string recipeId)
    {
        SelectedRecipes[branch] = recipeId;
    }

    public bool TryGetSelectedTierFilter(string branch, out int tier)
    {
        return SelectedTierFilters.TryGetValue(branch, out tier);
    }

    public void SetSelectedTierFilter(string branch, int tier)
    {
        SelectedTierFilters[branch] = tier;
    }

    public string GetSearchText(string branch)
    {
        return SearchTextByBranch.TryGetValue(branch, out var text)
            ? text
            : string.Empty;
    }

    public bool SetSearchText(string branch, string text)
    {
        var normalized = text.Trim();
        if (GetSearchText(branch) == normalized)
            return false;

        SearchTextByBranch[branch] = normalized;
        return true;
    }

    public bool GetCraftableOnly(string branch)
    {
        return CraftableOnlyByBranch.TryGetValue(branch, out var value) && value;
    }

    public void ToggleCraftableOnly(string branch)
    {
        CraftableOnlyByBranch[branch] = !GetCraftableOnly(branch);
    }
}
