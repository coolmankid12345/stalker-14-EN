using Content.Shared.Storage;
using Robust.Shared.Serialization;

namespace Content.Shared._Stalker_EN.Loadout;

/// <summary>
/// Represents a nested item inside a container (e.g., magazine in vest, medkit in backpack).
/// Supports recursive nesting for containers within containers.
/// </summary>
[Serializable, NetSerializable]
public sealed class LoadoutNestedItem
{
    /// <summary>
    /// The container ID this item is stored in (e.g., "storagebase").
    /// </summary>
    public string ContainerName { get; set; } = string.Empty;

    /// <summary>
    /// The prototype ID of the item.
    /// </summary>
    public string PrototypeId { get; set; } = string.Empty;

    /// <summary>
    /// Serialized item state (IItemStalkerStorage data).
    /// Not sent over network - only used server-side and in database.
    /// </summary>
    [field: NonSerialized]
    public object? StorageData { get; set; }

    /// <summary>
    /// Unique identifier for matching items in stash.
    /// </summary>
    public string Identifier { get; set; } = string.Empty;

    /// <summary>
    /// Position and rotation within grid-based storage (if applicable).
    /// Null for non-storage containers like ItemSlots.
    /// </summary>
    public ItemStorageLocation? StorageLocation { get; set; }

    /// <summary>
    /// Items nested inside this item's container (recursive).
    /// </summary>
    public List<LoadoutNestedItem> NestedItems { get; set; } = new();
}
