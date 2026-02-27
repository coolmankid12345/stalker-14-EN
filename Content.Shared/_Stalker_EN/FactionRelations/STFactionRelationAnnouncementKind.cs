using Robust.Shared.Serialization;

namespace Content.Shared._Stalker_EN.FactionRelations;

/// <summary>
/// The kind of faction relation announcement, used to select locale string variants.
/// </summary>
[Serializable, NetSerializable]
public enum STFactionRelationAnnouncementKind : byte
{
    /// <summary>
    /// A direct unilateral relation change (escalation or admin command).
    /// </summary>
    DirectChange,

    /// <summary>
    /// A bilateral proposal was sent and is awaiting confirmation.
    /// </summary>
    ProposalSent,

    /// <summary>
    /// A bilateral proposal was accepted.
    /// </summary>
    ProposalAccepted,

    /// <summary>
    /// A bilateral proposal was rejected.
    /// </summary>
    ProposalRejected,

    /// <summary>
    /// A bilateral proposal expired without a response.
    /// </summary>
    ProposalExpired,
}
