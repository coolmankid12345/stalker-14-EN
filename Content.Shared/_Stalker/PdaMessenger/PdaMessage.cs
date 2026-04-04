using Robust.Shared.Serialization;

namespace Content.Shared._Stalker.PdaMessenger;

[Serializable, NetSerializable]
public sealed class PdaMessage
{
    public string Title;
    public string Content;
    public string Receiver;
    public string? BandId; // Band ID for faction icon

    public PdaMessage(string title, string content, string receiver, string? bandId = null)
    {
        Title = title;
        Content = content;
        Receiver = receiver;
        BandId = bandId;
    }
}
