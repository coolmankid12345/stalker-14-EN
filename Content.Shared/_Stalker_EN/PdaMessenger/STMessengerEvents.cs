using Content.Shared.CartridgeLoader;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Stalker_EN.PdaMessenger;

/// <summary>
/// Send a message to a channel or DM.
/// <see cref="TargetChatId"/> is either a channel prototype ID or a DM chat ID
/// in the format "<see cref="STMessengerChat.DmChatPrefix"/>{messengerId}".
/// </summary>
[Serializable, NetSerializable]
public sealed class STMessengerSendEvent : CartridgeMessageEvent
{
    public readonly string TargetChatId;
    public readonly string Content;
    public readonly uint? ReplyToId;

    /// <summary>
    /// If true, the message will be posted under a per-character pseudonym (channels only).
    /// Server ignores this flag for DMs.
    /// </summary>
    public readonly bool IsAnonymous;

    public STMessengerSendEvent(string targetChatId, string content, uint? replyToId = null, bool isAnonymous = false)
    {
        TargetChatId = targetChatId;
        Content = content;
        ReplyToId = replyToId;
        IsAnonymous = isAnonymous;
    }
}

/// <summary>
/// Add a contact by their unique messenger ID number (e.g. "472-819").
/// </summary>
[Serializable, NetSerializable]
public sealed class STMessengerAddContactEvent : CartridgeMessageEvent
{
    public readonly string MessengerId;

    public STMessengerAddContactEvent(string messengerId)
    {
        MessengerId = messengerId;
    }
}

/// <summary>
/// Remove a contact by their unique messenger ID.
/// </summary>
[Serializable, NetSerializable]
public sealed class STMessengerRemoveContactEvent : CartridgeMessageEvent
{
    public readonly string ContactMessengerId;

    public STMessengerRemoveContactEvent(string contactMessengerId)
    {
        ContactMessengerId = contactMessengerId;
    }
}

/// <summary>
/// Toggle the mute state of a channel (suppresses ringer notifications).
/// </summary>
[Serializable, NetSerializable]
public sealed class STMessengerToggleMuteEvent : CartridgeMessageEvent
{
    public readonly ProtoId<STMessengerChannelPrototype> ChannelId;

    public STMessengerToggleMuteEvent(ProtoId<STMessengerChannelPrototype> channelId)
    {
        ChannelId = channelId;
    }
}

/// <summary>
/// Mark a channel/DM as read up to a specific message ID.
/// </summary>
[Serializable, NetSerializable]
public sealed class STMessengerMarkReadEvent : CartridgeMessageEvent
{
    public readonly string ChatId;
    public readonly uint LastSeenMessageId;

    public STMessengerMarkReadEvent(string chatId, uint lastSeenMessageId)
    {
        ChatId = chatId;
        LastSeenMessageId = lastSeenMessageId;
    }
}

/// <summary>
/// Client tells server which chat is currently being viewed (for lazy message loading).
/// Null means the client is on the main page (no chat open).
/// </summary>
[Serializable, NetSerializable]
public sealed class STMessengerViewChatEvent : CartridgeMessageEvent
{
    public readonly string? ChatId;

    public STMessengerViewChatEvent(string? chatId)
    {
        ChatId = chatId;
    }
}

/// <summary>
/// Client requests navigation to a bulletin board offer from a clickable offer link in chat.
/// Handled by the messenger system, which raises a local event for the bulletin board to pick up.
/// </summary>
[Serializable, NetSerializable]
public sealed class STMessengerNavigateToOfferEvent : CartridgeMessageEvent
{
    public readonly uint OfferId;

    public STMessengerNavigateToOfferEvent(uint offerId)
    {
        OfferId = offerId;
    }
}

/// <summary>
/// Client requests navigation to a news article from a clickable news link in chat.
/// Handled by the messenger system, which raises a local event for the news cartridge to pick up.
/// </summary>
[Serializable, NetSerializable]
public sealed class STMessengerNavigateToNewsEvent : CartridgeMessageEvent
{
    public readonly int ArticleId;

    public STMessengerNavigateToNewsEvent(int articleId)
    {
        ArticleId = articleId;
    }
}
