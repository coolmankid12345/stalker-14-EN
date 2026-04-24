using Content.Shared._Stalker.Bands;
using Content.Shared._Stalker_EN.PdaMessenger;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.ViewVariables;

namespace Content.Server._Stalker_EN.PdaMessenger;

/// <summary>
/// Server-side messenger state for a PDA cartridge.
/// Not networked — client receives data via <see cref="STMessengerUiState"/> through the BUI system.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(STMessengerSystem))]
public sealed partial class STMessengerServerComponent : Component
{
    /// <summary>
    /// This PDA's unique messenger ID (e.g. "472-819"). Loaded from DB on spawn.
    /// </summary>
    [ViewVariables]
    public string MessengerId = string.Empty;

    /// <summary>
    /// The player's account user ID (from NetUserId). Used as part of the composite identity key.
    /// </summary>
    [ViewVariables]
    public Guid OwnerUserId;

    /// <summary>
    /// Character name of the PDA's original owner.
    /// Stored as string so it survives entity deletion (e.g. body cleanup after death).
    /// Used for sender identity on all outgoing messages.
    /// </summary>
    [ViewVariables]
    public string OwnerCharacterName = string.Empty;

    /// <summary>
    /// Contacts loaded from DB for this character.
    /// Key = contact's messenger ID ("XXX-XXX"), Value = contact metadata.
    /// </summary>
    [ViewVariables]
    public Dictionary<string, STContactEntry> Contacts = new();

    /// <summary>
    /// Channels the player has muted (suppresses ringer notification).
    /// </summary>
    [DataField]
    public HashSet<ProtoId<STMessengerChannelPrototype>> MutedChannels = new();

    /// <summary>
    /// Per-channel last-seen message ID for unread tracking.
    /// Key = chat ID (channel proto ID or "dm:{messengerId}"), Value = last seen message ID.
    /// </summary>
    [ViewVariables]
    public Dictionary<string, uint> LastSeenMessageId = new();

    /// <summary>
    /// Minimum time between messages for this PDA.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan SendCooldown = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Next allowed send time (absolute simulation time). Paused when entity is paused.
    /// </summary>
    [AutoPausedField]
    [ViewVariables]
    public TimeSpan NextSendTime;

    /// <summary>
    /// Rate-limits contact add/remove operations to prevent DB flooding.
    /// </summary>
    [AutoPausedField]
    [ViewVariables]
    public TimeSpan NextInteractionTime;

    /// <summary>
    /// One-shot deep-link target set by external systems (e.g. merc board Contact).
    /// Consumed and cleared by the next <see cref="STMessengerSystem.UpdateUiState"/> call.
    /// </summary>
    [ViewVariables]
    public string? PendingNavigateToChatId;

    /// <summary>
    /// One-shot draft message to pre-fill in the compose page (e.g. merc board offer reference).
    /// Consumed and cleared alongside <see cref="PendingNavigateToChatId"/>.
    /// </summary>
    [ViewVariables]
    public string? PendingDraftMessage;

    /// <summary>
    /// The PDA owner's band prototype ID (e.g. "STDolgBand", "STFreedomBand").
    /// Set once at initialization from the owner's BandsComponent.
    /// Used to determine which band-restricted channels this PDA can access.
    /// </summary>
    [ViewVariables]
    public ProtoId<STBandPrototype>? OwnerBand;

    /// <summary>
    /// If true, the player's name will be randomized in messages when not disguised.
    /// Only applies to players who can disguise (have AltBand and CanChange).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool RandomNameWhenNotDisguised;
}

/// <summary>
/// Metadata about a contact stored in <see cref="STMessengerServerComponent.Contacts"/>.
/// </summary>
public sealed class STContactEntry
{
    public Guid UserId;
    public string CharacterName;
    public string? FactionName;

    public STContactEntry(Guid userId, string characterName, string? factionName)
    {
        UserId = userId;
        CharacterName = characterName;
        FactionName = factionName;
    }
}
