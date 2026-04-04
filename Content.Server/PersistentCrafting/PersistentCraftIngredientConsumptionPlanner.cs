using System;
using System.Collections.Generic;
using Content.Shared._Stalker.PersistentCrafting;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Robust.Shared.GameObjects;

namespace Content.Server._Stalker.PersistentCrafting;

public sealed class PersistentCraftIngredientConsumptionPlanner
{
    private readonly IEntityManager _entityManager;
    private readonly PersistentCraftIngredientMatcher _matcher;
    private readonly TagSystem _tagSystem;
    private readonly PersistentCraftAccessibleInventoryPolicy _inventoryPolicy;

    public PersistentCraftIngredientConsumptionPlanner(
        IEntityManager entityManager,
        TagSystem tagSystem,
        PersistentCraftAccessibleInventoryPolicy? inventoryPolicy = null)
    {
        _entityManager = entityManager;
        _tagSystem = tagSystem;
        _inventoryPolicy = inventoryPolicy ?? PersistentCraftAccessibleInventoryPolicy.Default;
        _matcher = new PersistentCraftIngredientMatcher(entityManager);
    }

    public bool TryPlan(
        EntityUid user,
        PersistentCraftRecipePrototype recipe,
        Func<PersistentCraftIngredient, int> getRequiredAmount,
        out Dictionary<EntityUid, int> plan)
    {
        plan = new Dictionary<EntityUid, int>();
        if (!_entityManager.EntityExists(user))
            return false;

        var availableEntities = PersistentCraftInventoryHelper.CollectAccessibleEntities(_entityManager, user, _inventoryPolicy);

        for (var ingredientIndex = 0; ingredientIndex < recipe.Ingredients.Count; ingredientIndex++)
        {
            var ingredient = recipe.Ingredients[ingredientIndex];
            var remaining = Math.Max(0, getRequiredAmount(ingredient));
            if (remaining <= 0)
                continue;

            var candidates = BuildCandidates(availableEntities, ingredient, plan);
            for (var candidateIndex = 0; candidateIndex < candidates.Count && remaining > 0; candidateIndex++)
            {
                var candidate = candidates[candidateIndex];
                var taken = Math.Min(candidate.AvailableAmount, remaining);
                if (taken <= 0)
                    continue;

                if (plan.TryGetValue(candidate.Entity, out var reserved))
                    plan[candidate.Entity] = reserved + taken;
                else
                    plan[candidate.Entity] = taken;

                remaining -= taken;
            }

            if (remaining > 0)
            {
                plan.Clear();
                return false;
            }
        }

        return true;
    }

    private List<IngredientCandidate> BuildCandidates(
        List<EntityUid> availableEntities,
        PersistentCraftIngredient ingredient,
        Dictionary<EntityUid, int> plan)
    {
        var candidates = new List<IngredientCandidate>();

        for (var entityIndex = 0; entityIndex < availableEntities.Count; entityIndex++)
        {
            var entity = availableEntities[entityIndex];
            if (!_entityManager.EntityExists(entity))
                continue;

            var matchKind = ClassifyMatch(entity, ingredient);
            if (matchKind == IngredientMatchKind.None)
                continue;

            var usableAmount = _matcher.GetUsableAmount(entity);
            if (plan.TryGetValue(entity, out var reservedAmount))
                usableAmount -= reservedAmount;

            if (usableAmount <= 0)
                continue;

            candidates.Add(new IngredientCandidate(entity, matchKind, usableAmount));
        }

        candidates.Sort(static (left, right) =>
        {
            var matchComparison = left.MatchKind.CompareTo(right.MatchKind);
            if (matchComparison != 0)
                return matchComparison;

            var amountComparison = left.AvailableAmount.CompareTo(right.AvailableAmount);
            if (amountComparison != 0)
                return amountComparison;

            return left.Entity.Id.CompareTo(right.Entity.Id);
        });

        return candidates;
    }

    private IngredientMatchKind ClassifyMatch(
        EntityUid entity,
        PersistentCraftIngredient ingredient)
    {
        switch (ingredient.GetSelectorKind())
        {
            case PersistentCraftIngredientSelectorKind.Proto:
                return _entityManager.TryGetComponent(entity, out MetaDataComponent? metadata) &&
                       metadata.EntityPrototype?.ID == ingredient.Proto
                    ? IngredientMatchKind.ExactPrototype
                    : IngredientMatchKind.None;

            case PersistentCraftIngredientSelectorKind.StackType:
                var stackType = ingredient.StackType;
                return _entityManager.TryGetComponent(entity, out StackComponent? stack) &&
                       !string.IsNullOrWhiteSpace(stackType) &&
                       string.Equals(stack.StackTypeId, stackType, StringComparison.Ordinal)
                    ? IngredientMatchKind.StackType
                    : IngredientMatchKind.None;

            case PersistentCraftIngredientSelectorKind.Tag:
                return _tagSystem.HasTag(entity, ingredient.Tag!) ? IngredientMatchKind.Tag : IngredientMatchKind.None;

            default:
                return IngredientMatchKind.None;
        }
    }

    private readonly record struct IngredientCandidate(
        EntityUid Entity,
        IngredientMatchKind MatchKind,
        int AvailableAmount);

    private enum IngredientMatchKind
    {
        ExactPrototype = 0,
        StackType = 1,
        Tag = 2,
        None = 3,
    }
}
