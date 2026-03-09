using Content.Shared.Chat;
using Robust.Shared.Player;

namespace Content.Server._Stalker.Chat;

[ByRefEvent]
public record struct STChatMessageOverrideInVoiceRangeEvent(
    ICommonSession HearingSession,
    ChatChannel Channel,
    EntityUid Source,
    string Message,
    string WrappedMessage,
    bool EntHideChat,
    float Range = -1f,              // stalker-en-changes: distance from source to listener
    bool Observer = false,           // stalker-en-changes: listener is a ghost/observer
    bool? HideChatOverride = null)   // stalker-en-changes: relay recipient (camera/AI)
{
    public bool Cancelled = false; // stalker-en-changes: suppress message delivery
}
