using Robust.Shared.GameObjects;

namespace Content.Shared._Stalker_EN.SoftCrit;

/// <summary>
/// Raised on the source entity before the chat type switch in ChatSystem.
/// If a handler sets <see cref="Override"/> to true, the speech type is forced to whisper
/// and action blocker checks are bypassed.
/// </summary>
[ByRefEvent]
public record struct STSoftCritSpeechEvent(EntityUid Source)
{
    public bool Override = false;
}
