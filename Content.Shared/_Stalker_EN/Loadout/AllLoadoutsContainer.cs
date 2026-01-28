using System.Linq;
using Robust.Shared.Serialization;

namespace Content.Shared._Stalker_EN.Loadout;

/// <summary>
/// Container for all player loadouts, serialized to database.
/// </summary>
[Serializable, NetSerializable]
public sealed class AllLoadoutsContainer
{
    /// <summary>
    /// Format version for future migrations.
    /// </summary>
    public int FormatVersion { get; set; } = 1;

    /// <summary>
    /// All saved loadouts. No max limit.
    /// </summary>
    public List<PlayerLoadout> Loadouts { get; set; } = new();

    /// <summary>
    /// Gets the next available ID for a new loadout.
    /// </summary>
    public int GetNextId()
    {
        if (Loadouts.Count == 0)
            return 1; // 0 is reserved for Quick Save

        return Loadouts.Max(l => l.Id) + 1;
    }

    /// <summary>
    /// Gets the quick save loadout (ID = 0), or null if not set.
    /// </summary>
    public PlayerLoadout? GetQuickSave()
    {
        return Loadouts.FirstOrDefault(l => l.Id == 0);
    }
}
