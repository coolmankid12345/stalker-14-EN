using System;
using System.Collections.Generic;
using Content.Shared._Stalker.PersistentCrafting;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Stalker.PersistentCrafting.UI.Coordinators;

public sealed class PersistentCraftStationBranchCoordinator
{
    private readonly IDictionary<string, BoxContainer> _branchContainers;
    private readonly Dictionary<string, PersistentCraftBranchState> _branchStateById = new();
    private PersistentCraftBranchRegistry _branchRegistry;

    public PersistentCraftStationBranchCoordinator(
        PersistentCraftBranchRegistry branchRegistry,
        IDictionary<string, BoxContainer> branchContainers)
    {
        _branchRegistry = branchRegistry;
        _branchContainers = branchContainers;
    }

    public void SetBranchRegistry(PersistentCraftBranchRegistry branchRegistry)
    {
        _branchRegistry = branchRegistry;
    }

    public void RebuildBranchStateIndex(PersistentCraftState state)
    {
        _branchStateById.Clear();
        for (var i = 0; i < state.BranchStates.Count; i++)
        {
            var branchState = state.BranchStates[i];
            _branchStateById[branchState.Branch] = branchState;
        }
    }

    public PersistentCraftBranchState GetBranchState(string branch)
    {
        if (_branchStateById.TryGetValue(branch, out var branchState))
            return branchState;

        return new PersistentCraftBranchState(
            branch,
            0,
            0);
    }

    public string GetCurrentBranch(TabContainer branches)
    {
        return _branchRegistry.TryGetBranchByIndex(branches.CurrentTab, out var branch)
            ? branch
            : (_branchRegistry.FirstBranchId is { Length: > 0 } first ? first : GetAnyBranchId());
    }

    public BoxContainer GetBranchContainer(string branch)
    {
        if (_branchContainers.TryGetValue(branch, out var container))
            return container;

        foreach (var existing in _branchContainers.Values)
        {
            return existing;
        }

        throw new InvalidOperationException("Persistent craft station branches are not initialized.");
    }

    public void SelectPreferredBranchTab(
        TabContainer branches,
        Func<string, int> getAvailableRecipeCount)
    {
        var preferredBranch = string.Empty;
        var bestRecipeCount = -1;
        var bestPointCount = -1;

        for (var i = 0; i < _branchRegistry.OrderedBranchIds.Count; i++)
        {
            var branch = _branchRegistry.OrderedBranchIds[i];
            var availableRecipeCount = getAvailableRecipeCount(branch);
            var points = GetBranchState(branch).AvailablePoints;
            if (availableRecipeCount <= 0 && points <= 0)
                continue;

            if (availableRecipeCount > bestRecipeCount ||
                (availableRecipeCount == bestRecipeCount && points > bestPointCount))
            {
                preferredBranch = branch;
                bestRecipeCount = availableRecipeCount;
                bestPointCount = points;
            }
        }

        if (!string.IsNullOrWhiteSpace(preferredBranch) &&
            _branchRegistry.TryGetBranchIndex(preferredBranch, out var branchIndex))
        {
            branches.CurrentTab = branchIndex;
        }
    }

    private string GetAnyBranchId()
    {
        foreach (var key in _branchContainers.Keys)
        {
            return key;
        }

        return string.Empty;
    }
}
