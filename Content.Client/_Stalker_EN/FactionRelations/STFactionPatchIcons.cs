using Robust.Shared.Utility;

namespace Content.Client._Stalker_EN.FactionRelations;

/// <summary>
/// Shared RSI paths and state names for faction patch icons used in multiple UIs.
/// </summary>
public static class STFactionPatchIcons
{
    private static readonly ResPath PatchRsiPath = new("/Textures/_Stalker/Icons/Patches/band.rsi");
    private static readonly ResPath PatchRsiPathEN = new("/Textures/_Stalker_EN/Icons/Patches/band.rsi");

    private const string Loners = "Loners";
    private const string Freedom = "Freedom";
    private const string Bandits = "Bandits";
    private const string Duty = "Duty";
    private const string Ecologist = "Ecologist";
    private const string Neutrals = "Neutrals";
    private const string Mercenaries = "Mercenaries";
    private const string Military = "Military";
    private const string Monolith = "Monolith";
    private const string ClearSky = "ClearSky";
    private const string Renegades = "Renegades";
    private const string Rookies = "Rookies";

    /// <summary>
    /// Maps faction relation names to their patch RSI paths and state names.
    /// </summary>
    public static readonly Dictionary<string, (ResPath Rsi, string State)> PatchStates = new()
    {
        [Loners] = (PatchRsiPath, "stalker"),
        [Freedom] = (PatchRsiPath, "freedom"),
        [Bandits] = (PatchRsiPath, "band"),
        [Duty] = (PatchRsiPathEN, "dolg1"),
        [Ecologist] = (PatchRsiPath, "sci"),
        [Neutrals] = (PatchRsiPath, "ne"),
        [Mercenaries] = (PatchRsiPath, "merc"),
        [Military] = (PatchRsiPathEN, "voen1"),
        [Monolith] = (PatchRsiPath, "monolith"),
        [ClearSky] = (PatchRsiPath, "cn"),
        [Renegades] = (PatchRsiPath, "rene"),
        [Rookies] = (PatchRsiPathEN, "rookie"),
    };
}
