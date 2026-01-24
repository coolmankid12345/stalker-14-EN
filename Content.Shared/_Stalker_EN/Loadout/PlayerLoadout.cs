using Robust.Shared.Serialization;

namespace Content.Shared._Stalker_EN.Loadout;

/// <summary>
/// Represents a complete saved loadout with all equipped items.
/// </summary>
[Serializable, NetSerializable]
public sealed class PlayerLoadout
{
    /// <summary>
    /// Unique ID for this loadout. ID 0 is reserved for the "Quick Save" slot.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Display name for this loadout.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// All items equipped in inventory slots.
    /// </summary>
    public List<LoadoutSlotItem> SlotItems { get; set; } = new();

    /// <summary>
    /// Items held in hands at time of save.
    /// </summary>
    public List<LoadoutNestedItem> HandItems { get; set; } = new();

    /// <summary>
    /// When the loadout was created/last saved.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Number of items from this loadout that are missing from stash.
    /// Populated by server when sending state to client.
    /// </summary>
    public int MissingCount { get; set; }

    /// <summary>
    /// Gets the total count of items in this loadout (slots + hands + nested).
    /// </summary>
    public int GetTotalItemCount()
    {
        var count = SlotItems.Count + HandItems.Count;
        foreach (var slot in SlotItems)
        {
            count += CountNestedItems(slot.NestedItems);
        }
        foreach (var hand in HandItems)
        {
            count += CountNestedItems(hand.NestedItems);
        }
        return count;
    }

    private static int CountNestedItems(List<LoadoutNestedItem> items)
    {
        var count = items.Count;
        foreach (var item in items)
        {
            count += CountNestedItems(item.NestedItems);
        }
        return count;
    }
}
