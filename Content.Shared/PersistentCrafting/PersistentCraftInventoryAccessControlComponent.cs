using System.Collections.Generic;
using Robust.Shared.GameObjects;

namespace Content.Shared._Stalker.PersistentCrafting;

/// <summary>
/// Explicit crafting-access policy for an entity and its internal containers.
/// Use this to exclude hidden/internal storage from persistent crafting without
/// relying on hardcoded container IDs sprinkled through the traversal code.
/// </summary>
[RegisterComponent]
public sealed partial class PersistentCraftInventoryAccessControlComponent : Component
{
    /// <summary>
    /// If true, this entity itself cannot be consumed as a crafting ingredient.
    /// Its contained entities may still remain accessible unless excluded separately.
    /// </summary>
    [DataField("excludeFromCrafting")]
    public bool ExcludeFromCrafting;

    /// <summary>
    /// If true, containers owned by this entity are not traversed for crafting.
    /// </summary>
    [DataField("excludeContainedEntitiesFromCrafting")]
    public bool ExcludeContainedEntitiesFromCrafting;

    /// <summary>
    /// Fine-grained per-entity exclusions for specific container IDs.
    /// </summary>
    [DataField("excludedContainerIds")]
    public HashSet<string> ExcludedContainerIds = new();
}
