using System.Collections.Generic;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.Shared._Stalker.PersistentCrafting;

public sealed class PersistentCraftAccessibleInventoryPolicy
{
    public const string ToggleableClothingContainerId = "toggleable-clothing";

    public static readonly PersistentCraftAccessibleInventoryPolicy Default = new();

    private readonly HashSet<string> _globallyExcludedContainerIds;

    public PersistentCraftAccessibleInventoryPolicy(IEnumerable<string>? globallyExcludedContainerIds = null)
    {
        _globallyExcludedContainerIds = globallyExcludedContainerIds != null
            ? new HashSet<string>(globallyExcludedContainerIds)
            : new HashSet<string> { ToggleableClothingContainerId };
    }

    public bool CanTraverseRoot(IEntityManager entityManager, EntityUid root, HashSet<EntityUid> seen)
    {
        return entityManager.EntityExists(root) && seen.Add(root);
    }

    public bool ShouldTraverseContainer(
        IEntityManager entityManager,
        EntityUid owner,
        string containerId,
        BaseContainer container)
    {
        _ = container;

        if (_globallyExcludedContainerIds.Contains(containerId))
            return false;

        if (entityManager.TryGetComponent(owner, out PersistentCraftInventoryAccessControlComponent? access) &&
            access.ExcludedContainerIds.Contains(containerId))
        {
            return false;
        }

        return true;
    }

    public PersistentCraftAccessibleEntityDecision EvaluateContainedEntity(
        IEntityManager entityManager,
        EntityUid entity,
        HashSet<EntityUid> seen)
    {
        if (!entityManager.EntityExists(entity) || !seen.Add(entity))
            return PersistentCraftAccessibleEntityDecision.Skip;

        var includeEntity = true;
        var traverseContainedEntities = true;

        if (entityManager.TryGetComponent(entity, out PersistentCraftInventoryAccessControlComponent? access))
        {
            includeEntity = !access.ExcludeFromCrafting;
            traverseContainedEntities = !access.ExcludeContainedEntitiesFromCrafting;
        }

        return new PersistentCraftAccessibleEntityDecision(includeEntity, traverseContainedEntities);
    }
}

public readonly struct PersistentCraftAccessibleEntityDecision
{
    public static readonly PersistentCraftAccessibleEntityDecision Skip = new(false, false);

    public bool IncludeEntity { get; }
    public bool TraverseContainedEntities { get; }

    public PersistentCraftAccessibleEntityDecision(bool includeEntity, bool traverseContainedEntities)
    {
        IncludeEntity = includeEntity;
        TraverseContainedEntities = traverseContainedEntities;
    }
}
