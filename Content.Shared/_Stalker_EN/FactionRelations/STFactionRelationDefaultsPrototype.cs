using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared._Stalker_EN.FactionRelations;

/// <summary>
/// Prototype defining the default faction relations matrix.
/// Provides the list of factions and their default pairwise relations.
/// DB overrides take precedence over these defaults.
/// </summary>
[Prototype("stFactionRelationDefaults")]
public sealed class STFactionRelationDefaultsPrototype : IPrototype
{
    [ViewVariables]
    [IdDataField]
    public string ID { get; } = string.Empty;

    /// <summary>
    /// Ordered list of faction display IDs for the matrix.
    /// </summary>
    [DataField(required: true)]
    public List<string> Factions { get; } = new();

    /// <summary>
    /// Default pairwise relations. Omitted pairs default to Neutral.
    /// </summary>
    [DataField(required: true)]
    public List<STFactionRelationDefault> Relations { get; } = new();

    /// <summary>
    /// Maps STBandPrototype names to faction relation names.
    /// E.g. "Dolg" -> "Duty", "Stalker" -> "Loners".
    /// Used to resolve a player's band into their faction relation identity.
    /// </summary>
    [DataField]
    public Dictionary<string, string> BandMapping { get; } = new();

    /// <summary>
    /// Maps a primary faction to its aliases.
    /// Alias factions share the primary's relations and are hidden from UIs.
    /// E.g. "Loners" -> ["Rookies", "Neutrals"].
    /// </summary>
    [DataField]
    public Dictionary<string, List<string>> FactionGroups { get; } = new();

    /// <summary>
    /// Factions that cannot be targeted by player-initiated relation changes.
    /// Only admins can change relations involving these factions.
    /// Players in restricted factions do not see the Relations tab.
    /// </summary>
    [DataField]
    public List<string> RestrictedFactions { get; } = new();

    /// <summary>
    /// Factions hidden from all relation UIs (PDA app grid, Igor Relations tab).
    /// Unlike aliases, hidden factions are independent — they just don't appear in UI.
    /// </summary>
    [DataField]
    public List<string> HiddenFactions { get; } = new();

    /// <summary>
    /// Maps faction IDs to human-readable display names.
    /// Only factions that differ from their ID need an entry (e.g. "ClearSky" → "Clear Sky").
    /// Factions without an entry use their ID as-is.
    /// </summary>
    [DataField]
    public Dictionary<string, string> DisplayNames { get; } = new();

    /// <summary>
    /// Pairwise relation restrictions. Blocks players from setting the listed relations
    /// between the given pair. Pair order does not matter. Admin commands bypass these.
    /// </summary>
    [DataField]
    public List<STFactionRelationRestriction> RelationRestrictions { get; } = new();
}

/// <summary>
/// A single default faction relation entry defined in YAML.
/// </summary>
[DataDefinition]
public sealed partial class STFactionRelationDefault
{
    [DataField(required: true)]
    public string FactionA { get; set; } = string.Empty;

    [DataField(required: true)]
    public string FactionB { get; set; } = string.Empty;

    [DataField(required: true)]
    public STFactionRelationType Relation { get; set; }
}

/// <summary>
/// A pair-specific set of relation transitions that players cannot initiate.
/// </summary>
[DataDefinition]
public sealed partial class STFactionRelationRestriction
{
    [DataField(required: true)]
    public string FactionA { get; set; } = string.Empty;

    [DataField(required: true)]
    public string FactionB { get; set; } = string.Empty;

    /// <summary>Relation states that cannot be set between this pair.</summary>
    [DataField(required: true)]
    public List<STFactionRelationType> Forbidden { get; set; } = new();
}
