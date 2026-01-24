using Robust.Shared.Serialization;

namespace Content.Shared._Stalker_EN.Loadout;

/// <summary>
/// Represents a missing item from a loadout, used for detailed display.
/// </summary>
[Serializable, NetSerializable]
public sealed class MissingLoadoutItem
{
    /// <summary>
    /// Display name of the missing item.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Location of the item (slot name or "hand").
    /// </summary>
    public string Location { get; set; } = string.Empty;

    public MissingLoadoutItem() { }

    public MissingLoadoutItem(string name, string location)
    {
        Name = name;
        Location = location;
    }
}
