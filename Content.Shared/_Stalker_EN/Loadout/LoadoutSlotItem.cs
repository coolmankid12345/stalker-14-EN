using Robust.Shared.Serialization;

namespace Content.Shared._Stalker_EN.Loadout;

/// <summary>
/// Represents an item equipped in a specific inventory slot.
/// </summary>
[Serializable, NetSerializable]
public sealed class LoadoutSlotItem
{
    /// <summary>
    /// The inventory slot name (e.g., "head", "back", "belt", "jumpsuit").
    /// </summary>
    public string SlotName { get; set; } = string.Empty;

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
    /// Items nested inside this item's container (e.g., magazines in vest).
    /// </summary>
    public List<LoadoutNestedItem> NestedItems { get; set; } = new();
}
