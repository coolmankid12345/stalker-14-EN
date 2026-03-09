using Robust.Shared.Serialization;

namespace Content.Shared._Stalker_EN.News;

/// <summary>
/// Full article DTO sent to client for article detail view.
/// </summary>
[Serializable, NetSerializable]
public sealed class STNewsArticle
{
    /// <summary>Database-assigned article ID.</summary>
    public readonly int Id;

    /// <summary>Article headline.</summary>
    public readonly string Title;

    /// <summary>Full article body (may contain markup).</summary>
    public readonly string Content;

    /// <summary>In-game name of the journalist who published.</summary>
    public readonly string Author;

    /// <summary>Round number in which the article was published.</summary>
    public readonly int RoundId;

    /// <summary>Round-relative time the article was published.</summary>
    public readonly TimeSpan PublishTime;

    /// <summary>Embed accent color (RGB integer).</summary>
    public readonly int EmbedColor;

    public STNewsArticle(
        int id,
        string title,
        string content,
        string author,
        int roundId,
        TimeSpan publishTime,
        int embedColor)
    {
        Id = id;
        Title = title;
        Content = content;
        Author = author;
        RoundId = roundId;
        PublishTime = publishTime;
        EmbedColor = embedColor;
    }
}

/// <summary>
/// Lightweight summary DTO for article list view. Avoids sending full content.
/// </summary>
[Serializable, NetSerializable]
public sealed class STNewsArticleSummary
{
    /// <summary>Database-assigned article ID.</summary>
    public readonly int Id;

    /// <summary>Article headline.</summary>
    public readonly string Title;

    /// <summary>First ~150 chars of content with markup stripped.</summary>
    public readonly string Preview;

    /// <summary>In-game name of the journalist who published.</summary>
    public readonly string Author;

    /// <summary>Round number in which the article was published.</summary>
    public readonly int RoundId;

    /// <summary>Round-relative time the article was published.</summary>
    public readonly TimeSpan PublishTime;

    /// <summary>Embed accent color (RGB integer).</summary>
    public readonly int EmbedColor;

    public STNewsArticleSummary(
        int id,
        string title,
        string preview,
        string author,
        int roundId,
        TimeSpan publishTime,
        int embedColor)
    {
        Id = id;
        Title = title;
        Preview = preview;
        Author = author;
        RoundId = roundId;
        PublishTime = publishTime;
        EmbedColor = embedColor;
    }
}

/// <summary>
/// Constants for article validation.
/// </summary>
public static class STNewsConstants
{
    public const int MaxTitleLength = 50;
    public const int MaxContentLength = 2048;
    public const int PreviewLength = 150;
}
