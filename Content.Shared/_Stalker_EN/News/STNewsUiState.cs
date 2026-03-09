using Robust.Shared.Serialization;

namespace Content.Shared._Stalker_EN.News;

/// <summary>
/// UI state for the Stalker News cartridge program.
/// </summary>
[Serializable, NetSerializable]
public sealed class STNewsUiState : BoundUserInterfaceState
{
    /// <summary>Article summaries, newest first.</summary>
    public readonly List<STNewsArticleSummary> Articles;

    /// <summary>Whether the current user has journalist access (can publish).</summary>
    public readonly bool CanWrite;

    /// <summary>Article ID to navigate to (from news link click).</summary>
    public readonly int? OpenArticleId;

    /// <summary>Full article content (when detail view is requested).</summary>
    public readonly STNewsArticle? OpenArticle;

    /// <summary>Article IDs that are new (unread) for this user.</summary>
    public readonly HashSet<int> NewArticleIds;

    public STNewsUiState(
        List<STNewsArticleSummary> articles,
        bool canWrite,
        int? openArticleId = null,
        STNewsArticle? openArticle = null,
        HashSet<int>? newArticleIds = null)
    {
        Articles = articles;
        CanWrite = canWrite;
        OpenArticleId = openArticleId;
        OpenArticle = openArticle;
        NewArticleIds = newArticleIds ?? new HashSet<int>();
    }
}
