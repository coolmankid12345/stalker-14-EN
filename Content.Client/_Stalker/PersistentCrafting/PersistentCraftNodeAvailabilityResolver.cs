using System;
using System.Collections.Generic;
using Content.Shared._Stalker.PersistentCrafting;

namespace Content.Client._Stalker.PersistentCrafting;

public static class PersistentCraftNodeAvailabilityResolver
{
    public static bool HasNodeUnlockedOrAutoAvailable(
        PersistentCraftState state,
        string nodeId,
        Func<string, PersistentCraftNodePrototype?> resolveNode)
    {
        return PersistentCraftNodeRules.HasNodeUnlockedOrAutoAvailable(
            nodeId,
            state.UnlockedNodes.Contains,
            resolveNode);
    }

    public static bool HasNodeUnlockedOrAutoAvailable(
        PersistentCraftState state,
        string nodeId,
        Func<string, PersistentCraftNodePrototype?> resolveNode,
        HashSet<string> reusablePath)
    {
        return PersistentCraftNodeRules.HasNodeUnlockedOrAutoAvailable(
            nodeId,
            state.UnlockedNodes.Contains,
            resolveNode,
            reusablePath);
    }

    public static bool ArePrerequisitesMet(
        PersistentCraftState state,
        PersistentCraftNodePrototype node,
        Func<string, PersistentCraftNodePrototype?> resolveNode)
    {
        return PersistentCraftNodeRules.ArePrerequisitesMet(
            node,
            state.UnlockedNodes.Contains,
            resolveNode);
    }

    public static bool ArePrerequisitesMet(
        PersistentCraftState state,
        PersistentCraftNodePrototype node,
        Func<string, PersistentCraftNodePrototype?> resolveNode,
        HashSet<string> reusablePath)
    {
        return PersistentCraftNodeRules.ArePrerequisitesMet(
            node,
            state.UnlockedNodes.Contains,
            resolveNode,
            reusablePath);
    }
}
