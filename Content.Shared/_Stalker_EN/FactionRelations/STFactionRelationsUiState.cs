using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared._Stalker_EN.FactionRelations;

/// <summary>
/// UI state sent from server to client for the faction relations PDA program.
/// </summary>
[Serializable, NetSerializable]
public sealed class STFactionRelationsUiState : BoundUserInterfaceState
{
    /// <summary>
    /// Ordered list of faction display IDs for the matrix headers.
    /// </summary>
    public List<string> FactionIds;

    /// <summary>
    /// Flat list of faction pair relations. Pairs not listed default to Neutral.
    /// </summary>
    public List<STFactionRelationEntry> Relations;

    public STFactionRelationsUiState(List<string> factionIds, List<STFactionRelationEntry> relations)
    {
        FactionIds = factionIds;
        Relations = relations;
    }
}

/// <summary>
/// A single faction pair relation entry for network serialization.
/// </summary>
[Serializable, NetSerializable]
public sealed class STFactionRelationEntry
{
    /// <summary>
    /// First faction ID (alphabetically first of the pair).
    /// </summary>
    public string FactionA;

    /// <summary>
    /// Second faction ID (alphabetically second of the pair).
    /// </summary>
    public string FactionB;

    /// <summary>
    /// The relationship type between the two factions.
    /// </summary>
    public STFactionRelationType Relation;

    public STFactionRelationEntry(string factionA, string factionB, STFactionRelationType relation)
    {
        FactionA = factionA;
        FactionB = factionB;
        Relation = relation;
    }
}
