using Robust.Shared.Serialization;

namespace Content.Shared._Stalker_EN.Loadout;

/// <summary>
/// Represents a missing item from a loadout, used for detailed display.
/// Supports nested children for hierarchical display of container contents.
/// </summary>
[Serializable, NetSerializable]
public sealed class MissingLoadoutItem
{
    /// <summary>
    /// Display name of the missing item.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Location of the item (slot name or container name).
    /// </summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>
    /// Number of this item that are missing.
    /// </summary>
    public int Count { get; set; } = 1;

    /// <summary>
    /// Nested children (items that were inside this container).
    /// </summary>
    public List<MissingLoadoutItem> Children { get; set; } = new();

    public MissingLoadoutItem() { }

    public MissingLoadoutItem(string name, string location)
    {
        Name = name;
        Location = location;
        Count = 1;
    }
}
