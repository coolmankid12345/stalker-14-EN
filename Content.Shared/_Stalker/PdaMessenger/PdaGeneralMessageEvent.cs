using Robust.Shared.Serialization;

namespace Content.Shared._Stalker.PdaMessenger;

/// <summary>
/// Event sent from server to all clients when a message is sent to the General PDA channel.
/// Used for displaying toast notifications in the bottom-left corner.
/// </summary>
[Serializable, NetSerializable]
public sealed class PdaGeneralMessageEvent : EntityEventArgs
{
    public readonly string Title;
    public readonly string Content;
    public readonly string Sender;

    /// <summary>
    /// Band icon name (e.g. "stalker", "freedom", "Dolg", "band").
    /// Used to determine which faction PNG texture to display.
    /// </summary>
    public readonly string? BandIcon;

    public PdaGeneralMessageEvent(string title, string content, string sender, string? bandIcon = null)
    {
        Title = title;
        Content = content;
        Sender = sender;
        BandIcon = bandIcon;
    }
}
