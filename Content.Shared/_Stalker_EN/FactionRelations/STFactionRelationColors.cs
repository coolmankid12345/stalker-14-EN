using Robust.Shared.Maths;

namespace Content.Shared._Stalker_EN.FactionRelations;

/// <summary>
/// Shared color constants for faction relation types.
/// Used by both server announcements and client UI elements.
/// </summary>
public static class STFactionRelationColors
{
    public static readonly Color Alliance = Color.FromHex("#2d7019");
    public static readonly Color Neutral = Color.FromHex("#b8a900");
    public static readonly Color Hostile = Color.FromHex("#c87000");
    public static readonly Color War = Color.FromHex("#a01000");

    /// <summary>
    /// Gets the color for a given relation type. Returns null for unknown types.
    /// </summary>
    public static Color? GetColor(STFactionRelationType type)
    {
        return type switch
        {
            STFactionRelationType.Alliance => Alliance,
            STFactionRelationType.Neutral => Neutral,
            STFactionRelationType.Hostile => Hostile,
            STFactionRelationType.War => War,
            _ => null,
        };
    }
}
