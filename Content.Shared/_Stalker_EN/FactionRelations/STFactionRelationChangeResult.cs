using Robust.Shared.Serialization;

namespace Content.Shared._Stalker_EN.FactionRelations;

/// <summary>
/// Result of a faction relation change attempt.
/// </summary>
[Serializable, NetSerializable]
public enum STFactionRelationChangeResult : byte
{
    /// <summary>
    /// The relation was changed successfully (unilateral escalation).
    /// </summary>
    Success,

    /// <summary>
    /// A bilateral proposal was created and is awaiting confirmation.
    /// </summary>
    ProposalCreated,

    /// <summary>
    /// A pending proposal was accepted and the relation was changed.
    /// </summary>
    ProposalAccepted,

    /// <summary>
    /// A pending proposal was rejected.
    /// </summary>
    ProposalRejected,

    /// <summary>
    /// The faction pair is currently on cooldown.
    /// </summary>
    OnCooldown,

    /// <summary>
    /// One or both faction names are invalid.
    /// </summary>
    InvalidFaction,

    /// <summary>
    /// The proposed relation is the same as the current relation.
    /// </summary>
    SameRelation,

    /// <summary>
    /// No pending proposal was found to accept/reject/cancel.
    /// </summary>
    ProposalNotFound,

    /// <summary>
    /// The target faction is restricted and can only be changed by admins.
    /// </summary>
    RestrictedFaction,
}
