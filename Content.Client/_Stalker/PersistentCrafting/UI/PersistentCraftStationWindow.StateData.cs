using System.Collections.Generic;
using System.Numerics;
using Content.Shared._Stalker.PersistentCrafting;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Stalker.PersistentCrafting.UI;

public sealed partial class PersistentCraftStationWindow
{
    private void SelectRecipe(string branch, string recipeId)
    {
        RememberListScroll(branch);
        _viewModel.DetailScrollByBranch[branch] = Vector2.Zero;

        _viewModel.TryGetSelectedRecipe(branch, out var previousRecipeId);
        _viewModel.SetSelectedRecipe(branch, recipeId);

        if (branch == GetCurrentBranch() &&
            _recipeEntryControlsByBranch.TryGetValue(branch, out var entries) &&
            _detailContentHostsByBranch.ContainsKey(branch) &&
            _visibleBranchStatesByBranch.ContainsKey(branch))
        {
            if (!string.IsNullOrWhiteSpace(previousRecipeId) &&
                entries.TryGetValue(previousRecipeId, out var previousControls) &&
                TryGetRecipeById(previousRecipeId, out var previousRecipe))
            {
                ApplyRecipeEntryVisuals(previousControls.Button, previousControls.IconPanel, previousRecipe, false);
            }

            if (entries.TryGetValue(recipeId, out var selectedControls) &&
                TryGetRecipeById(recipeId, out var selectedRecipe))
            {
                ApplyRecipeEntryVisuals(selectedControls.Button, selectedControls.IconPanel, selectedRecipe, true);
                UpdateRecipeDetails(branch, selectedRecipe);
                return;
            }
        }

        PopulateBranch(GetBranchContainer(branch), branch);
    }

    private void SelectTierFilter(string branch, int tier)
    {
        RememberListScroll(branch);
        _viewModel.DetailScrollByBranch[branch] = Vector2.Zero;
        _viewModel.SetSelectedTierFilter(branch, tier);
        PopulateBranch(GetBranchContainer(branch), branch);
    }

    private void UpdateSearch(string branch, LineEdit.LineEditEventArgs args)
    {
        if (!_viewModel.SetSearchText(branch, args.Text))
            return;

        _viewModel.ListScrollByBranch[branch] = Vector2.Zero;
        _viewModel.DetailScrollByBranch[branch] = Vector2.Zero;
        PopulateBranch(GetBranchContainer(branch), branch);
    }

    private void ToggleCraftableOnly(string branch)
    {
        _viewModel.ToggleCraftableOnly(branch);
        _viewModel.ListScrollByBranch[branch] = Vector2.Zero;
        _viewModel.DetailScrollByBranch[branch] = Vector2.Zero;
        PopulateBranch(GetBranchContainer(branch), branch);
    }

    private void ToggleCategoryCollapse(string categoryKey)
    {
        var branch = GetCurrentBranch();
        RememberListScroll(branch);

        if (!_viewModel.CollapsedCategoryKeys.Add(categoryKey))
            _viewModel.CollapsedCategoryKeys.Remove(categoryKey);

        PopulateBranch(GetBranchContainer(branch), branch);
    }

    private void ToggleSubCategoryCollapse(string subCategoryKey)
    {
        var branch = GetCurrentBranch();
        RememberListScroll(branch);

        if (!_viewModel.CollapsedSubCategoryKeys.Add(subCategoryKey))
            _viewModel.CollapsedSubCategoryKeys.Remove(subCategoryKey);

        PopulateBranch(GetBranchContainer(branch), branch);
    }

    private void EnsureCategoryCollapsedByDefault(string categoryKey)
    {
        if (_viewModel.InitializedCategoryKeys.Add(categoryKey))
            _viewModel.CollapsedCategoryKeys.Add(categoryKey);
    }

    private void EnsureSubCategoryCollapsedByDefault(string subCategoryKey)
    {
        if (_viewModel.InitializedSubCategoryKeys.Add(subCategoryKey))
            _viewModel.CollapsedSubCategoryKeys.Add(subCategoryKey);
    }

    private void RememberBranchScroll(string branch)
    {
        RememberListScroll(branch);

        if (_activeDetailScrollByBranch.TryGetValue(branch, out var detailScroll))
            _viewModel.DetailScrollByBranch[branch] = detailScroll.GetScrollValue(true);
    }

    private void RememberListScroll(string branch)
    {
        if (_activeListScrollByBranch.TryGetValue(branch, out var listScroll))
            _viewModel.ListScrollByBranch[branch] = listScroll.GetScrollValue(true);
    }

    private void RestoreBranchScroll(string branch, ScrollContainer listScroll, ScrollContainer detailScroll)
    {
        if (_viewModel.ListScrollByBranch.TryGetValue(branch, out var listScrollValue))
            listScroll.SetScrollValue(listScrollValue);

        if (_viewModel.DetailScrollByBranch.TryGetValue(branch, out var detailScrollValue))
            detailScroll.SetScrollValue(detailScrollValue);

        _pendingScrollRestoreBranches.Add(branch);
    }

    private void UpdateRecipeDetails(string branch, PersistentCraftRecipePrototype recipe)
    {
        if (!_detailContentHostsByBranch.TryGetValue(branch, out var detailHost) ||
            !_visibleBranchStatesByBranch.TryGetValue(branch, out var branchState))
        {
            PopulateBranch(GetBranchContainer(branch), branch);
            return;
        }

        detailHost.RemoveAllChildren();
        detailHost.AddChild(CreateRecipeDetailsPanel(recipe, branchState));

        if (_activeDetailScrollByBranch.TryGetValue(branch, out var detailScroll))
            detailScroll.SetScrollValue(Vector2.Zero);
    }

    private static string BuildCategoryGroupKey(string branch, int tier, string categoryId)
    {
        return $"{branch}|{tier}|{categoryId}";
    }

    private static string BuildSubCategoryGroupKey(string branch, int tier, string categoryId, string subCategoryId)
    {
        return $"{branch}|{tier}|{categoryId}|{subCategoryId}";
    }

    private BoxContainer GetBranchContainer(string branch)
    {
        return _branchCoordinator.GetBranchContainer(branch);
    }

    private string GetCurrentBranch()
    {
        return _branchCoordinator.GetCurrentBranch(Branches);
    }
}
