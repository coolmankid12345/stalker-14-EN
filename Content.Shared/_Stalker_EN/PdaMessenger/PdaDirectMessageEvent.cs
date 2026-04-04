using Robust.Shared.Serialization;

namespace Content.Shared._Stalker_EN.PdaMessenger;

/// <summary>
/// Event sent from server to client when a DM message is received.
/// Used for displaying pop-up notifications for direct messages.
/// </summary>
[Serializable, NetSerializable]
public sealed class PdaDirectMessageEvent : EntityEventArgs
{
    /// <summary>
    /// Sender character name.
    /// </summary>
    public readonly string Sender;

    /// <summary>
    /// Message content.
    /// </summary>
    public readonly string Content;

    /// <summary>
    /// Band icon name (e.g. "stalker", "freedom", "Dolg").
    /// </summary>
    public readonly string? BandIcon;

    public PdaDirectMessageEvent(string sender, string content, string? bandIcon = null)
    {
        Sender = sender;
        Content = content;
        BandIcon = bandIcon;
    }
}
