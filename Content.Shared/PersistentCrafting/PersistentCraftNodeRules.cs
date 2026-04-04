using System;
using System.Collections.Generic;

namespace Content.Shared._Stalker.PersistentCrafting;

public static class PersistentCraftNodeRules
{
    public static bool HasNodeUnlockedOrAutoAvailable(
        string nodeId,
        Func<string, bool> isUnlocked,
        Func<string, PersistentCraftNodePrototype?> resolveNode)
    {
        return HasNodeUnlockedOrAutoAvailable(nodeId, isUnlocked, resolveNode, new HashSet<string>());
    }

    /// <summary>
    /// Overload for hot paths: caller provides a reusable HashSet to avoid per-call allocations.
    /// The HashSet is used as a cycle-detection scratch buffer and is cleared before use.
    /// </summary>
    public static bool HasNodeUnlockedOrAutoAvailable(
        string nodeId,
        Func<string, bool> isUnlocked,
        Func<string, PersistentCraftNodePrototype?> resolveNode,
        HashSet<string> reusablePath)
    {
        reusablePath.Clear();
        return HasNodeUnlockedOrAutoAvailableInternal(nodeId, isUnlocked, resolveNode, reusablePath);
    }

    public static bool ArePrerequisitesMet(
        PersistentCraftNodePrototype node,
        Func<string, bool> isUnlocked,
        Func<string, PersistentCraftNodePrototype?> resolveNode)
    {
        return ArePrerequisitesMet(node, isUnlocked, resolveNode, new HashSet<string>());
    }

    /// <summary>
    /// Overload for hot paths: caller provides a reusable HashSet to avoid per-call allocations.
    /// The HashSet is used as a cycle-detection scratch buffer and is cleared before each prerequisite check.
    /// </summary>
    public static bool ArePrerequisitesMet(
        PersistentCraftNodePrototype node,
        Func<string, bool> isUnlocked,
        Func<string, PersistentCraftNodePrototype?> resolveNode,
        HashSet<string> reusablePath)
    {
        for (var i = 0; i < node.Prerequisites.Count; i++)
        {
            reusablePath.Clear();
            if (!HasNodeUnlockedOrAutoAvailableInternal(node.Prerequisites[i], isUnlocked, resolveNode, reusablePath))
                return false;
        }

        return true;
    }

    private static bool HasNodeUnlockedOrAutoAvailableInternal(
        string nodeId,
        Func<string, bool> isUnlocked,
        Func<string, PersistentCraftNodePrototype?> resolveNode,
        HashSet<string> path)
    {
        var node = resolveNode(nodeId);
        if (node == null)
            return false;

        if (isUnlocked(nodeId))
            return true;

        if (!PersistentCraftingHelper.IsAutoUnlockedNode(node))
            return false;

        if (!path.Add(nodeId))
            return false;

        try
        {
            for (var i = 0; i < node.Prerequisites.Count; i++)
            {
                if (!HasNodeUnlockedOrAutoAvailableInternal(node.Prerequisites[i], isUnlocked, resolveNode, path))
                    return false;
            }

            return true;
        }
        finally
        {
            path.Remove(nodeId);
        }
    }
}
