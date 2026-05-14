using Content.Shared.CartridgeLoader;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._Stalker_EN.Leaderboard;

/// <summary>
/// Unique key for a player character in the leaderboard.
/// Allows one user to have multiple characters listed.
/// </summary>
public readonly record struct StalkerKey(NetUserId UserId, string CharacterName)
{
    public override string ToString() => $"{CharacterName}({UserId})";
}

/// <summary>
/// PDA cartridge component that provides the Stalker Leaderboard feature.
/// When inserted into a PDA, it shows a ranked list of all known stalkers with their faction and rank.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class STLeaderboardCartridgeComponent : Component
{
}

/// <summary>
/// Faction relation type for coloring leaderboard entries.
/// Distinguishes mutual hostility (War/Red) from one-way hostility (Hostile/Orange).
/// </summary>
public enum STLeaderboardFactionRelation : byte
{
    Same = 0,     // white — same faction
    Alliance = 1, // green — allied
    Neutral = 2,  // yellow — neutral
    Hostile = 3,  // orange — one-way hostile
    War = 4,      // red — mutual hostility
}

/// <summary>
/// One entry in the stalker leaderboard.
/// </summary>
[Serializable, NetSerializable]
public record struct STLeaderboardEntry(
    string CharacterName,
    string? BandName,
    string? BandIcon,
    int RankIndex,
    string? RankName,
    STLeaderboardFactionRelation BandRelation,
    bool IsMe,
    TimeSpan AccumulatedTime,
    string? PortraitPath,
    bool UsePatchInsteadOfPortrait,
    string? DisplayBandName,
    bool Hidden); // Hidden from leaderboard by user choice

/// <summary>
/// UI state sent from server to client with the full leaderboard.
/// </summary>
[Serializable, NetSerializable]
public sealed class STLeaderboardUiState : BoundUserInterfaceState
{
    public readonly List<STLeaderboardEntry> Entries;

    public STLeaderboardUiState(List<STLeaderboardEntry> entries)
    {
        Entries = entries;
    }
}

/// <summary>
/// Client-to-server UI message.
/// </summary>
[Serializable, NetSerializable]
public sealed class STLeaderboardUiMessage : CartridgeMessageEvent
{
}

/// <summary>
/// Message to toggle hidden state for the current user's leaderboard entry.
/// </summary>
[Serializable, NetSerializable]
public sealed class STLeaderboardToggleHiddenMessage : CartridgeMessageEvent
{
}
