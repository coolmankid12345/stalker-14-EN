using System;
using System.Collections.Generic;
using Content.Shared._Stalker.PersistentCrafting;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Stalker.PersistentCrafting.UI.Coordinators;

public sealed class PersistentCraftBranchCoordinator
{
    private readonly IDictionary<string, BoxContainer> _branchHosts;
    private readonly Dictionary<string, PersistentCraftBranchState> _branchStateById = new();
    private PersistentCraftBranchRegistry _branchRegistry;

    public PersistentCraftBranchCoordinator(
        PersistentCraftBranchRegistry branchRegistry,
        IDictionary<string, BoxContainer> branchHosts)
    {
        _branchRegistry = branchRegistry;
        _branchHosts = branchHosts;
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

    public string GetCurrentBranch(TabContainer branches)
    {
        return _branchRegistry.TryGetBranchByIndex(branches.CurrentTab, out var branch)
            ? branch
            : (_branchRegistry.FirstBranchId is { Length: > 0 } first ? first : GetAnyBranchId());
    }

    public BoxContainer GetBranchHost(string branch)
    {
        if (_branchHosts.TryGetValue(branch, out var host))
            return host;

        foreach (var existingHost in _branchHosts.Values)
        {
            return existingHost;
        }

        throw new InvalidOperationException("Persistent craft skill branches are not initialized.");
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

    public void SelectPreferredBranchTab(TabContainer branches)
    {
        var preferredBranch = string.Empty;
        var bestPoints = 0;

        for (var i = 0; i < _branchRegistry.OrderedBranchIds.Count; i++)
        {
            var branch = _branchRegistry.OrderedBranchIds[i];
            var points = GetBranchState(branch).AvailablePoints;
            if (points <= bestPoints)
                continue;

            bestPoints = points;
            preferredBranch = branch;
        }

        if (!string.IsNullOrWhiteSpace(preferredBranch) &&
            _branchRegistry.TryGetBranchIndex(preferredBranch, out var branchIndex))
        {
            branches.CurrentTab = branchIndex;
        }
    }

    private string GetAnyBranchId()
    {
        foreach (var key in _branchHosts.Keys)
        {
            return key;
        }

        return string.Empty;
    }
}
