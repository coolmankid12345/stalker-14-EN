using Content.Shared.Hands.EntitySystems;
using Content.Shared.Stacks;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._Stalker_EN.Currency;

/// <summary>
/// Utility system for counting and modifying physical rouble stacks in an entity's containers.
/// Uses recursive container traversal matching ShopSystem's pattern, but avoids intermediate
/// list allocations by operating directly on containers.
/// </summary>
public sealed class STCurrencySystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;

    /// <summary>
    /// Stack type ID for roubles, matching ShopComponent.MoneyId default.
    /// </summary>
    public const string RoublesStackTypeId = "Roubles";

    /// <summary>
    /// Counts the total roubles in an entity's inventory containers.
    /// Traverses containers recursively without allocating intermediate lists.
    /// </summary>
    public int CountRoubles(EntityUid uid, string moneyId = RoublesStackTypeId)
    {
        var total = 0;
        CountRoublesRecursive(uid, moneyId, ref total);
        return total;
    }

    /// <summary>
    /// Attempts to deduct roubles from an entity's inventory.
    /// Collects all rouble stacks in a single container traversal,
    /// merges them, and adjusts the count.
    /// Returns false if the entity has insufficient funds or no rouble stacks.
    /// </summary>
    public bool TryDeductRoubles(EntityUid uid, int amount, string moneyId = RoublesStackTypeId)
    {
        if (amount <= 0)
            return true;

        var stacks = new List<(EntityUid Entity, StackComponent Stack)>();
        CollectRoubleStacks(uid, moneyId, stacks);

        if (stacks.Count == 0)
            return false;

        var target = stacks[0];
        for (var i = 1; i < stacks.Count; i++)
        {
            _stack.TryMergeStacks(stacks[i].Entity, target.Entity, out _);
        }

        if (target.Stack.Count < amount)
            return false;

        var newCount = target.Stack.Count - amount;
        _stack.SetCount(target.Entity, newCount);
        return true;
    }

    /// <summary>
    /// Adds roubles to an entity's inventory by first filling existing stacks,
    /// then spawning new rouble entities for the remainder.
    /// Follows the ShopSystem.IncreaseBalance pattern.
    /// </summary>
    public void AddRoubles(EntityUid uid, int amount, string moneyId = RoublesStackTypeId)
    {
        if (amount <= 0)
            return;

        if (!TryComp<TransformComponent>(uid, out var xform))
            return;

        var maxCount = _stack.GetMaxCount(new ProtoId<StackPrototype>(moneyId));
        var remaining = amount;

        var stacks = new List<(EntityUid Entity, StackComponent Stack)>();
        CollectRoubleStacks(uid, moneyId, stacks);

        foreach (var (entity, stack) in stacks)
        {
            var space = maxCount - stack.Count;
            if (space <= 0)
                continue;

            var toAdd = Math.Min(space, remaining);
            _stack.SetCount(entity, stack.Count + toAdd);
            remaining -= toAdd;

            if (remaining <= 0)
                return;
        }

        while (remaining > 0)
        {
            var toSpawn = Math.Min(maxCount, remaining);
            var money = Spawn(moneyId, xform.Coordinates);
            _stack.SetCount(money, toSpawn);
            _hands.PickupOrDrop(uid, money);
            remaining -= toSpawn;
        }
    }

    /// <summary>
    /// Recursively counts rouble stacks without allocating intermediate lists.
    /// </summary>
    private void CountRoublesRecursive(EntityUid uid, string moneyId, ref int total, ContainerManagerComponent? manager = null)
    {
        if (!Resolve(uid, ref manager, logMissing: false))
            return;

        foreach (var container in manager.Containers.Values)
        {
            foreach (var element in container.ContainedEntities)
            {
                if (TryComp<StackComponent>(element, out var stack) && stack.StackTypeId == moneyId)
                    total += stack.Count;

                if (TryComp<ContainerManagerComponent>(element, out var childManager))
                    CountRoublesRecursive(element, moneyId, ref total, childManager);
            }
        }
    }

    /// <summary>
    /// Recursively collects rouble stack entities into the provided list.
    /// </summary>
    private void CollectRoubleStacks(EntityUid uid, string moneyId, List<(EntityUid, StackComponent)> stacks, ContainerManagerComponent? manager = null)
    {
        if (!Resolve(uid, ref manager, logMissing: false))
            return;

        foreach (var container in manager.Containers.Values)
        {
            foreach (var element in container.ContainedEntities)
            {
                if (TryComp<StackComponent>(element, out var stack) && stack.StackTypeId == moneyId)
                    stacks.Add((element, stack));

                if (TryComp<ContainerManagerComponent>(element, out var childManager))
                    CollectRoubleStacks(element, moneyId, stacks, childManager);
            }
        }
    }
}
