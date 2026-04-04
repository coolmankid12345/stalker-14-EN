using System;
using System.Collections.Generic;
using Content.Client._Stalker.PersistentCrafting;
using Content.Shared._Stalker.PersistentCrafting;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Client._Stalker.PersistentCrafting.UI;

public sealed partial class PersistentCraftingWindow
{
    private IReadOnlyList<PersistentCraftRecipePrototype> FindRecipesForNode(PersistentCraftNodePrototype node)
    {
        if (_recipeIndex != null)
        {
            return _recipeIndex.GetByRequiredNode(node.ID);
        }

        if (_prototypeCache != null &&
            _prototypeCache.RecipesByNode.TryGetValue(node.ID, out var cacheRecipes))
        {
            return cacheRecipes;
        }

        var recipes = new List<PersistentCraftRecipePrototype>();
        for (var i = 0; i < _recipes.Count; i++)
        {
            var recipe = _recipes[i];
            if (recipe.RequiredNode == node.ID)
                recipes.Add(recipe);
        }

        return recipes;
    }

    private bool TryGetNodeTexture(PersistentCraftNodePrototype node, out Texture? texture)
    {
        texture = null;

        var displayProto = node.DisplayProto;
        if (string.IsNullOrWhiteSpace(displayProto))
        {
            var recipes = FindRecipesForNode(node);
            if (recipes.Count > 0)
                displayProto = PersistentCraftingHelper.GetDisplayPrototypeId(recipes[0]);
        }

        if (string.IsNullOrWhiteSpace(displayProto))
            return false;

        try
        {
            var spriteSystem = _entityManager.EntitySysManager.GetEntitySystem<SpriteSystem>();
            texture = spriteSystem.GetPrototypeIcon(displayProto).Default;
            return texture != null;
        }
        catch (Exception ex)
        {
            Sawmill.Warning($"Failed to resolve node texture for node '{node.ID}' using proto '{displayProto}': {ex}");
            texture = null;
            return false;
        }
    }

    private string ResolveRecipeName(PersistentCraftRecipePrototype recipe)
    {
        return _textResolver.ResolveRecipeName(recipe);
    }

    private string ResolveNodeName(PersistentCraftNodePrototype node)
    {
        return _textResolver.ResolveNodeName(node);
    }

    private string ResolveNodeCardCaption(PersistentCraftNodePrototype node)
    {
        return _textResolver.ResolveNodeCardCaption(node);
    }

    private bool HasNodeUnlockedOrAutoAvailable(string nodeId)
    {
        if (_state == null)
            return false;

        return PersistentCraftNodeAvailabilityResolver.HasNodeUnlockedOrAutoAvailable(
            _state,
            nodeId,
            ResolveNodePrototypeOrNull,
            _reusablePath);
    }

    private Color GetBranchAccent(string branch)
    {
        return _branchRegistry.TryGetBranchDefinition(branch, out var definition)
            ? definition.AccentColor
            : Color.White;
    }

    private string ResolveBranchTitle(string branchId)
    {
        return _textResolver.ResolveBranchTitle(branchId);
    }

    private IReadOnlyList<PersistentCraftNodePrototype> GetNodesForBranch(string branch)
    {
        if (_prototypeCache != null &&
            _prototypeCache.NodesByBranch.TryGetValue(branch, out var nodes))
        {
            return nodes;
        }

        var filtered = new List<PersistentCraftNodePrototype>();
        for (var i = 0; i < _nodes.Count; i++)
        {
            var node = _nodes[i];
            if (node.Branch == branch)
                filtered.Add(node);
        }

        return filtered;
    }

    private bool TryGetNodePrototype(string nodeId, out PersistentCraftNodePrototype node)
    {
        if (_prototypeCache != null &&
            _prototypeCache.TryGetNode(nodeId, out node))
        {
            return true;
        }

        if (_prototype.TryIndex<PersistentCraftNodePrototype>(nodeId, out var resolvedNode) &&
            resolvedNode != null)
        {
            node = resolvedNode;
            return true;
        }

        node = default!;
        return false;
    }

    private PersistentCraftNodePrototype? ResolveNodePrototypeOrNull(string nodeId)
    {
        return TryGetNodePrototype(nodeId, out var node)
            ? node
            : null;
    }


}
