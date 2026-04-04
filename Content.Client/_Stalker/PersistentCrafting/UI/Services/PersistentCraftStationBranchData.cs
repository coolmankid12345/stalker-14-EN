using System.Collections.Generic;
using Content.Shared._Stalker.PersistentCrafting;

namespace Content.Client._Stalker.PersistentCrafting.UI.Services;

public sealed class PersistentCraftStationBranchData
{
    public PersistentCraftStationBranchData(
        IReadOnlyList<PersistentCraftRecipePrototype> branchRecipes,
        IReadOnlyList<PersistentCraftRecipePrototype> unlockedRecipes,
        IReadOnlyList<PersistentCraftRecipePrototype> visibleRecipes,
        IReadOnlyDictionary<string, bool> craftabilityByRecipeId,
        PersistentCraftRecipePrototype? selectedRecipe,
        int selectedTier,
        int craftableCount,
        string searchText,
        bool craftableOnly)
    {
        BranchRecipes = branchRecipes;
        UnlockedRecipes = unlockedRecipes;
        VisibleRecipes = visibleRecipes;
        CraftabilityByRecipeId = craftabilityByRecipeId;
        SelectedRecipe = selectedRecipe;
        SelectedTier = selectedTier;
        CraftableCount = craftableCount;
        SearchText = searchText;
        CraftableOnly = craftableOnly;
    }

    public IReadOnlyList<PersistentCraftRecipePrototype> BranchRecipes { get; }
    public IReadOnlyList<PersistentCraftRecipePrototype> UnlockedRecipes { get; }
    public IReadOnlyList<PersistentCraftRecipePrototype> VisibleRecipes { get; }
    public IReadOnlyDictionary<string, bool> CraftabilityByRecipeId { get; }
    public PersistentCraftRecipePrototype? SelectedRecipe { get; }
    public int SelectedTier { get; }
    public int CraftableCount { get; }
    public string SearchText { get; }
    public bool CraftableOnly { get; }
}
