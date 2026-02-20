using Robust.Shared.Serialization;

namespace Content.Shared._Stalker_EN.PvpZone;

/// <summary>
/// PvP zone types as defined by the Stalker-14 game rules (Rule C6).
/// Each zone type determines the PvP engagement rules for the area.
/// </summary>
[Serializable, NetSerializable]
public enum STPvpZoneType : byte
{
    /// <summary>Safe zone — PvP is strictly prohibited.</summary>
    Green = 0,

    /// <summary>Limited PvP — roleplay exchange required before engagement.</summary>
    Gray = 1,

    /// <summary>Standard PvP — faction rules and relations apply. Default for unspecified areas.</summary>
    Yellow = 2,

    /// <summary>High danger — on-sight PvP with neutral or hostile factions.</summary>
    Red = 3,

    /// <summary>Free-for-all — on-sight PvP regardless of faction alignment.</summary>
    Black = 4,

    /// <summary>Faction base area — base owner's rules apply.</summary>
    Faction = 5,
}
