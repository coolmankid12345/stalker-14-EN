using Robust.Shared.Serialization;

namespace Content.Shared._Stalker_EN.FactionRelations;

/// <summary>
/// Represents the relationship type between two factions in the Zone.
/// </summary>
[Serializable, NetSerializable]
public enum STFactionRelationType : byte
{
    /// <summary>
    /// Default neutral standing.
    /// </summary>
    Neutral = 0,

    /// <summary>
    /// Allied factions.
    /// </summary>
    Alliance = 1,

    /// <summary>
    /// Friendly, non-hostile but not full alliance.
    /// </summary>
    Friendly = 2,

    /// <summary>
    /// Can rob under faction rules.
    /// </summary>
    Hostile = 3,

    /// <summary>
    /// Kill on sight.
    /// </summary>
    War = 4,
}
