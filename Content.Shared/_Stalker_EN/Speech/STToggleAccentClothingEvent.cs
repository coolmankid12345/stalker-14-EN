using Robust.Shared.Serialization;

namespace Content.Shared._Stalker_EN.Speech;

/// <summary>
///     Raised on accent clothing to toggle the accent on/off.
/// </summary>
[Serializable, NetSerializable]
public sealed class STToggleAccentClothingEvent : EntityEventArgs
{
    public EntityUid Performer;
}
