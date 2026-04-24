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

    /// <summary>
    /// Character portrait prototype ID for the sender's selected portrait.
    /// If set, takes priority over BandIcon for notification display.
    /// </summary>
    public readonly string? PortraitId;

    /// <summary>
    /// Whether the sender is disguised (e.g., Clear Sky disguised as Loners).
    /// Used to display correct faction icon when PNG icons are disabled.
    /// </summary>
    public readonly bool IsDisguised;

    public PdaDirectMessageEvent(string sender, string content, string? bandIcon = null, string? portraitId = null, bool isDisguised = false)
    {
        Sender = sender;
        Content = content;
        BandIcon = bandIcon;
        PortraitId = portraitId;
        IsDisguised = isDisguised;
    }
}
