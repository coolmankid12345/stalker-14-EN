using System.Collections.Generic;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.Shared._Stalker.PersistentCrafting;

public static class PersistentCraftInventoryHelper
{
    private static readonly PersistentCraftAccessibleInventoryPolicy DefaultPolicy = PersistentCraftAccessibleInventoryPolicy.Default;

    public static List<EntityUid> CollectAccessibleEntities(
        IEntityManager entityManager,
        EntityUid root,
        PersistentCraftAccessibleInventoryPolicy? policy = null)
    {
        var effectivePolicy = policy ?? DefaultPolicy;
        var result = new List<EntityUid>();
        var seen = new HashSet<EntityUid>();

        if (!effectivePolicy.CanTraverseRoot(entityManager, root, seen))
            return result;

        CollectAccessibleEntitiesRecursive(entityManager, root, result, seen, effectivePolicy);
        return result;
    }

    private static void CollectAccessibleEntitiesRecursive(
        IEntityManager entityManager,
        EntityUid owner,
        List<EntityUid> result,
        HashSet<EntityUid> seen,
        PersistentCraftAccessibleInventoryPolicy policy,
        ContainerManagerComponent? manager = null)
    {
        if (manager == null && !entityManager.TryGetComponent(owner, out manager))
            return;

        foreach (var (containerId, container) in manager.Containers)
        {
            if (!policy.ShouldTraverseContainer(entityManager, owner, containerId, container))
                continue;

            foreach (var contained in container.ContainedEntities)
            {
                var decision = policy.EvaluateContainedEntity(entityManager, contained, seen);
                if (!decision.IncludeEntity && !decision.TraverseContainedEntities)
                    continue;

                if (decision.IncludeEntity)
                    result.Add(contained);

                if (decision.TraverseContainedEntities)
                    CollectAccessibleEntitiesRecursive(entityManager, contained, result, seen, policy);
            }
        }
    }
}
