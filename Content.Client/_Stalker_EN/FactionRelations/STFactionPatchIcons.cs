using Content.Shared._Stalker_EN.News;
using Robust.Shared.Utility;

namespace Content.Client._Stalker_EN.FactionRelations;

/// <summary>
/// Shared RSI paths and state names for faction patch icons used in multiple UIs.
/// </summary>
public static class STFactionPatchIcons
{
    private static readonly ResPath PatchRsiPath = new("/Textures/_Stalker/Icons/Patches/band.rsi");
    private static readonly ResPath PatchRsiPathEN = new("/Textures/_Stalker_EN/Icons/Patches/band.rsi");

    /// <summary>
    /// Maps faction relation names to their patch RSI paths and state names.
    /// Keys use constants from <see cref="STReactionDefinitions"/>.
    /// </summary>
    public static readonly Dictionary<string, (ResPath Rsi, string State)> PatchStates = new()
    {
        [STReactionDefinitions.Loners] = (PatchRsiPath, "stalker"),
        [STReactionDefinitions.Freedom] = (PatchRsiPath, "freedom"),
        [STReactionDefinitions.Bandits] = (PatchRsiPath, "band"),
        [STReactionDefinitions.Duty] = (PatchRsiPathEN, "dolg1"),
        [STReactionDefinitions.Ecologist] = (PatchRsiPath, "sci"),
        [STReactionDefinitions.Neutrals] = (PatchRsiPath, "ne"),
        [STReactionDefinitions.Mercenaries] = (PatchRsiPath, "merc"),
        [STReactionDefinitions.Military] = (PatchRsiPathEN, "voen1"),
        [STReactionDefinitions.Monolith] = (PatchRsiPath, "monolith"),
        [STReactionDefinitions.ClearSky] = (PatchRsiPath, "cn"),
        [STReactionDefinitions.Renegades] = (PatchRsiPath, "rene"),
        [STReactionDefinitions.Rookies] = (PatchRsiPathEN, "rookie"),
        [STReactionDefinitions.Journalists] = (PatchRsiPath, "journalist"),
        [STReactionDefinitions.UN] = (PatchRsiPath, "un")
    };
}
