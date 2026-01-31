using Content.Shared.Chat.Prototypes;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Tools;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Stalker_EN.RemovableMarkings;

/// <summary>
///     Allows you to forcefully remove markings from someone
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class RemovableMarkingsComponent : Component
{
    /// <summary>
    ///     Tool quality needed to remove marking.
    /// </summary>
    [DataField(required: true), ViewVariables]
    public ProtoId<ToolQualityPrototype> RequiredToolQuality;

    /// <summary>
    ///     Damage done to victim after finishing removal.
    /// </summary>
    [DataField(required: true), ViewVariables]
    public DamageSpecifier RemovalDamage;

    /// <summary>
    ///     Amount of time to stun this entity for, after marking removal.
    /// </summary>
    [DataField, ViewVariables]
    public TimeSpan? RemovalStunDuration = null;

    /// <summary>
    ///     Emote forced on the victim (if their mobstate is <see cref="Mobs.MobState.Alive"/>)
    ///         after removing the marking.
    /// </summary>
    [DataField, ViewVariables]
    public ProtoId<EmotePrototype>? RemovalEmoteId = null;

    [DataField, ViewVariables]
    public TimeSpan RemovalDoAfterLength = TimeSpan.FromSeconds(15);

    /// <summary>
    ///     Specifies what kinds of markings can be removed.
    /// </summary>
    [DataField, ViewVariables]
    public RemovableMarkingFlags CompatibleMarkingFlags = RemovableMarkingFlags.None;

    /// <summary>
    ///     Because i'm lazy. TODO: make this better
    /// </summary>
    [DataField, ViewVariables]
    public bool RemoveComponentAfterRemoval = true;
}

[Serializable, NetSerializable]
public sealed partial class MarkingRemovalDoAfterEvent : SimpleDoAfterEvent;

/// <summary>
///     Flags for what markings can be removed with <see cref="RemovableMarkingsComponent"/>.
/// </summary>
[Flags]
public enum RemovableMarkingFlags : byte
{
    None = 0,

    /// <summary>
    ///     Cat-ears/tails.
    /// </summary>
    Cat = 1 << 0
}
