using System.Collections.Generic;

namespace Content.Client._Stalker.PersistentCrafting.UI.ViewModels;

public sealed class PersistentCraftSkillTreeViewModel
{
    public Dictionary<string, string> SelectedNodeByBranch { get; } = new();
    public bool SelectPreferredBranchOnNextUpdate { get; set; } = true;

    public bool TryGetSelectedNode(string branch, out string nodeId)
    {
        if (SelectedNodeByBranch.TryGetValue(branch, out var found))
        {
            nodeId = found;
            return true;
        }

        nodeId = string.Empty;
        return false;
    }

    public void SetSelectedNode(string branch, string nodeId)
    {
        SelectedNodeByBranch[branch] = nodeId;
    }

    public void ClearSelectedNode(string branch)
    {
        SelectedNodeByBranch.Remove(branch);
    }
}
