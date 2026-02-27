using Robust.Shared.Serialization;

namespace Content.Shared._Stalker_EN.FactionRelations;

/// <summary>
/// A pending faction relation proposal entry for network serialization.
/// </summary>
[Serializable, NetSerializable]
public sealed class STFactionRelationProposalEntry
{
    /// <summary>
    /// The faction that initiated the proposal.
    /// </summary>
    public string InitiatingFaction { get; }

    /// <summary>
    /// The faction that must accept or reject the proposal.
    /// </summary>
    public string TargetFaction { get; }

    /// <summary>
    /// The proposed relation type.
    /// </summary>
    public STFactionRelationType ProposedRelation { get; }

    /// <summary>
    /// Optional custom message from the initiating faction's leader.
    /// </summary>
    public string? CustomMessage { get; }

    public STFactionRelationProposalEntry(
        string initiatingFaction,
        string targetFaction,
        STFactionRelationType proposedRelation,
        string? customMessage)
    {
        InitiatingFaction = initiatingFaction;
        TargetFaction = targetFaction;
        ProposedRelation = proposedRelation;
        CustomMessage = customMessage;
    }
}
