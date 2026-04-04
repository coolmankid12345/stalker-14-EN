using System.Collections.Generic;
using Content.Shared._Stalker.PersistentCrafting;
using Content.Client._Stalker.PersistentCrafting.UI.ViewModels;

namespace Content.Client._Stalker.PersistentCrafting.UI.Coordinators;

public sealed class PersistentCraftNodeSelectionCoordinator
{
    private readonly PersistentCraftSkillTreeViewModel _viewModel;

    public PersistentCraftNodeSelectionCoordinator(PersistentCraftSkillTreeViewModel viewModel)
    {
        _viewModel = viewModel;
    }

    public PersistentCraftNodePrototype? ResolveSelectedNode(
        string branch,
        IReadOnlyList<PersistentCraftNodePrototype> nodes)
    {
        if (_viewModel.TryGetSelectedNode(branch, out var selectedId))
        {
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node.ID == selectedId)
                    return node;
            }

            _viewModel.ClearSelectedNode(branch);
        }

        return null;
    }

    public PersistentCraftNodeSelectionDecision OnNodePressed(
        string branch,
        string nodeId,
        bool isDetailsWindowOpen)
    {
        if (_viewModel.TryGetSelectedNode(branch, out var selectedId) &&
            selectedId == nodeId &&
            isDetailsWindowOpen)
        {
            _viewModel.ClearSelectedNode(branch);
            return new PersistentCraftNodeSelectionDecision(
                ShouldCloseDetails: true,
                SelectedNodeId: null);
        }

        _viewModel.SetSelectedNode(branch, nodeId);
        return new PersistentCraftNodeSelectionDecision(
            ShouldCloseDetails: false,
            SelectedNodeId: nodeId);
    }
}

public readonly record struct PersistentCraftNodeSelectionDecision(
    bool ShouldCloseDetails,
    string? SelectedNodeId);
