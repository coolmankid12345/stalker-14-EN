using System.Text.RegularExpressions;
using Content.Server.Administration.Logs;
using Content.Server.CartridgeLoader;
using Content.Server.Database;
using Content.Server.Discord;
using Content.Server.GameTicking;
using Content.Server.PDA.Ringer;
using Content.Shared.Access;
using Content.Shared.Access.Systems;
using Content.Shared.CartridgeLoader;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.PDA.Ringer;
using Content.Shared._Stalker_EN.CCVar;
using Content.Shared._Stalker_EN.News;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Stalker_EN.News;

/// <summary>
/// Server system for the Stalker News PDA cartridge program.
/// Manages article publishing, database persistence, Discord webhook, and broadcast updates.
/// </summary>
public sealed class STNewsSystem : EntitySystem
{
    [Dependency] private readonly CartridgeLoaderSystem _cartridgeLoader = default!;
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;
    [Dependency] private readonly DiscordWebhook _discord = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly IServerDbManager _dbManager = default!;
    [Dependency] private readonly RingerSystem _ringer = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly ProtoId<AccessLevelPrototype> JournalistAccess = "Journalist";

    /// <summary>In-memory article cache, newest first.</summary>
    private readonly List<STNewsArticle> _articles = new();

    /// <summary>Loaders (PDAs) with the news cartridge currently active.</summary>
    private readonly HashSet<EntityUid> _activeLoaders = new();

    /// <summary>All known news cartridge loader UIDs, for sending notifications without a global query.</summary>
    private readonly HashSet<EntityUid> _allLoaders = new();

    /// <summary>Cached summary list, invalidated on publish. Avoids re-running regex strip on every UI open.</summary>
    private List<STNewsArticleSummary>? _cachedSummaries;

    private WebhookIdentifier? _webhookId;
    private bool _cacheReady;

    private static readonly Regex MarkupTagRegex = new(@"\[/?[^\]]+\]", RegexOptions.Compiled);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STNewsCartridgeComponent, CartridgeUiReadyEvent>(OnUiReady);
        SubscribeLocalEvent<STNewsCartridgeComponent, CartridgeActivatedEvent>(OnCartridgeActivated);
        SubscribeLocalEvent<STNewsCartridgeComponent, CartridgeDeactivatedEvent>(OnCartridgeDeactivated);
        SubscribeLocalEvent<STNewsCartridgeComponent, CartridgeMessageEvent>(OnMessage);
        SubscribeLocalEvent<STNewsCartridgeComponent, CartridgeAddedEvent>(OnCartridgeAdded);
        SubscribeLocalEvent<STNewsCartridgeComponent, STOpenNewsArticleEvent>(OnOpenArticle);
        SubscribeLocalEvent<STNewsCartridgeComponent, CartridgeRemovedEvent>(OnCartridgeRemoved);
        SubscribeLocalEvent<STNewsCartridgeComponent, EntityTerminatingEvent>(OnCartridgeTerminating);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);

        LoadFromDatabaseAsync();

        _config.OnValueChanged(STCCVars.NewsWebhook, OnWebhookChanged, true);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _config.UnsubValueChanged(STCCVars.NewsWebhook, OnWebhookChanged);
    }

    private void OnWebhookChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            _discord.GetWebhook(value, data => _webhookId = data.ToIdentifier());
        else
            _webhookId = null;
    }

    #region Database Loading

    private async void LoadFromDatabaseAsync()
    {
        try
        {
            var maxCached = _config.GetCVar(STCCVars.NewsMaxCachedArticles);
            var dbArticles = await _dbManager.GetRecentStalkerNewsArticlesAsync(maxCached);

            foreach (var dbArticle in dbArticles)
            {
                _articles.Add(new STNewsArticle(
                    dbArticle.Id,
                    dbArticle.Title,
                    dbArticle.Content,
                    dbArticle.Author,
                    dbArticle.RoundId,
                    TimeSpan.FromTicks(dbArticle.PublishTimeTicks),
                    dbArticle.EmbedColor));
            }

            _cachedSummaries = null;
        }
        catch (Exception e)
        {
            Log.Error($"Failed to load news articles from database: {e}");
        }
        finally
        {
            _cacheReady = true;
        }
    }

    #endregion

    #region Cartridge Events

    private void OnUiReady(EntityUid uid, STNewsCartridgeComponent comp, CartridgeUiReadyEvent args)
    {
        if (!_cacheReady)
            return;

        // Compute new article IDs BEFORE updating LastSeenArticleId so the first view shows them
        var newIds = GetNewArticleIds(comp);

        // Update LastSeenArticleId to newest article
        if (_articles.Count > 0)
            comp.LastSeenArticleId = _articles[0].Id;

        // Clear notification badge
        if (TryComp<CartridgeComponent>(uid, out var cartComp) && cartComp.HasNotification)
        {
            cartComp.HasNotification = false;
            Dirty(uid, cartComp);
        }

        int? openArticleId = null;
        STNewsArticle? openArticle = null;

        // Consume pending article navigation
        if (comp.PendingArticleId is { } pendingId)
        {
            comp.PendingArticleId = null;
            openArticleId = pendingId;
            openArticle = FindArticleById(pendingId);
        }

        var canWrite = HasJournalistAccessForLoader(args.Loader);
        var state = new STNewsUiState(
            GetCachedSummaries(), canWrite, openArticleId, openArticle, newIds);
        _cartridgeLoader.UpdateCartridgeUiState(args.Loader, state);
    }

    private void OnCartridgeAdded(EntityUid uid, STNewsCartridgeComponent comp, CartridgeAddedEvent args)
    {
        _allLoaders.Add(args.Loader);
    }

    private void OnCartridgeActivated(EntityUid uid, STNewsCartridgeComponent comp, CartridgeActivatedEvent args)
    {
        _activeLoaders.Add(args.Loader);
    }

    private void OnCartridgeDeactivated(EntityUid uid, STNewsCartridgeComponent comp, CartridgeDeactivatedEvent args)
    {
        // Engine bug workaround: DeactivateProgram passes programUid as args.Loader
        if (TryComp<CartridgeComponent>(uid, out var cartridge) && cartridge.LoaderUid is { } loaderUid)
            _activeLoaders.Remove(loaderUid);
        else
            _activeLoaders.Remove(args.Loader);
    }

    private void OnCartridgeRemoved(Entity<STNewsCartridgeComponent> ent, ref CartridgeRemovedEvent args)
    {
        _activeLoaders.Remove(args.Loader);
        _allLoaders.Remove(args.Loader);
    }

    private void OnCartridgeTerminating(EntityUid uid, STNewsCartridgeComponent comp, ref EntityTerminatingEvent args)
    {
        if (TryComp<CartridgeComponent>(uid, out var cartridge) && cartridge.LoaderUid is { } loaderUid)
        {
            _activeLoaders.Remove(loaderUid);
            _allLoaders.Remove(loaderUid);
        }
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        // _articles intentionally NOT cleared — articles persist across rounds (loaded once from DB)
        _activeLoaders.Clear();
        _allLoaders.Clear();
        _cachedSummaries = null;
    }

    #endregion

    #region Message Dispatch

    private void OnMessage(EntityUid uid, STNewsCartridgeComponent comp, CartridgeMessageEvent args)
    {
        switch (args)
        {
            case STNewsPublishEvent publish:
                OnPublish(uid, comp, publish, args);
                break;
            case STNewsRequestArticleEvent request:
                OnRequestArticle(comp, request, args);
                break;
        }
    }

    private void OnPublish(
        EntityUid uid,
        STNewsCartridgeComponent comp,
        STNewsPublishEvent publish,
        CartridgeMessageEvent args)
    {
        if (!HasJournalistAccess(args.Actor))
            return;

        if (_timing.CurTime < comp.NextPublishTime)
            return;

        comp.NextPublishTime = _timing.CurTime + TimeSpan.FromSeconds(
            _config.GetCVar(STCCVars.NewsPublishCooldownSeconds));

        var title = publish.Title.Trim();
        var content = publish.Content.Trim();

        if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(content))
            return;

        if (title.Length > STNewsConstants.MaxTitleLength)
            title = title[..STNewsConstants.MaxTitleLength];

        if (content.Length > STNewsConstants.MaxContentLength)
            content = content[..STNewsConstants.MaxContentLength];

        var author = MetaData(args.Actor).EntityName;

        _adminLogger.Add(
            LogType.Action,
            LogImpact.Low,
            $"{ToPrettyString(args.Actor):player} published news article: \"{title}\"");

        PublishArticleAsync(title, content, author, publish.EmbedColor);
    }

    private void OnRequestArticle(
        STNewsCartridgeComponent comp,
        STNewsRequestArticleEvent request,
        CartridgeMessageEvent args)
    {
        var article = FindArticleById(request.ArticleId);
        var canWrite = HasJournalistAccess(args.Actor);
        var loaderUid = GetEntity(args.LoaderUid);
        var newIds = GetNewArticleIds(comp);

        var state = new STNewsUiState(
            GetCachedSummaries(), canWrite, request.ArticleId, article, newIds);
        _cartridgeLoader.UpdateCartridgeUiState(loaderUid, state);
    }

    #endregion

    #region Cross-Cartridge Navigation

    private void OnOpenArticle(EntityUid uid, STNewsCartridgeComponent comp, ref STOpenNewsArticleEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        comp.PendingArticleId = args.ArticleId;
        _cartridgeLoader.ActivateProgram(args.LoaderUid, uid);
    }

    #endregion

    #region Broadcast

    private void BroadcastUiUpdate()
    {
        if (_activeLoaders.Count == 0)
            return;

        var summaries = GetCachedSummaries();
        var query = EntityQueryEnumerator<STNewsCartridgeComponent, CartridgeComponent>();
        while (query.MoveNext(out _, out var newsComp, out var cartridge))
        {
            if (cartridge.LoaderUid is not { } loaderUid)
                continue;

            if (!_activeLoaders.Contains(loaderUid))
                continue;

            if (!Exists(loaderUid))
                continue;

            var canWrite = HasJournalistAccessForLoader(loaderUid);
            var newIds = GetNewArticleIds(newsComp);
            var state = new STNewsUiState(summaries, canWrite, newArticleIds: newIds);
            _cartridgeLoader.UpdateCartridgeUiState(loaderUid, state);
        }
    }

    private void SendNotifications(STNewsArticle article)
    {
        foreach (var loaderUid in _allLoaders)
        {
            if (!_cartridgeLoader.TryGetProgram<STNewsCartridgeComponent>(loaderUid, out var progUid, out _))
                continue;

            if (!TryComp<CartridgeComponent>(progUid, out var cartridge))
                continue;

            cartridge.HasNotification = true;
            Dirty(progUid.Value, cartridge);

            if (TryComp<RingerComponent>(loaderUid, out var ringer))
                _ringer.RingerPlayRingtone((loaderUid, ringer));
        }
    }

    #endregion

    #region Database Persistence

    /// <summary>
    /// Saves the article to DB, then inserts into cache and broadcasts.
    /// Awaiting the DB save ensures clients receive the correct DB-assigned ID.
    /// </summary>
    private async void PublishArticleAsync(
        string title,
        string content,
        string author,
        int embedColor)
    {
        try
        {
            var dbArticle = new StalkerNewsArticle
            {
                Title = title,
                Content = content,
                Author = author,
                RoundId = _gameTicker.RoundId,
                PublishTimeTicks = _gameTicker.RoundDuration().Ticks,
                EmbedColor = embedColor,
                CreatedAt = DateTime.UtcNow,
            };

            var dbId = await _dbManager.AddStalkerNewsArticleAsync(dbArticle);

            var article = new STNewsArticle(
                dbId,
                title,
                content,
                author,
                dbArticle.RoundId,
                TimeSpan.FromTicks(dbArticle.PublishTimeTicks),
                embedColor);

            var maxCached = _config.GetCVar(STCCVars.NewsMaxCachedArticles);
            _articles.Insert(0, article);
            if (_articles.Count > maxCached)
                _articles.RemoveAt(_articles.Count - 1);

            _cachedSummaries = null;

            SendDiscordArticle(article);
            BroadcastUiUpdate();
            SendNotifications(article);
        }
        catch (Exception e)
        {
            Log.Error($"Failed to publish news article: {e}");
        }
    }

    #endregion

    #region Discord

    private async void SendDiscordArticle(STNewsArticle article)
    {
        if (_webhookId is null)
            return;

        try
        {
            var embed = new WebhookEmbed
            {
                Title = article.Title,
                Description = FormattedMessage.RemoveMarkupPermissive(article.Content),
                Color = article.EmbedColor & 0xFFFFFF,
                Footer = new WebhookEmbedFooter
                {
                    Text = Loc.GetString("st-news-discord-footer",
                        ("author", article.Author),
                        ("round", article.RoundId),
                        ("time", article.PublishTime.ToString(@"hh\:mm\:ss"))),
                },
            };

            var payload = new WebhookPayload { Embeds = new List<WebhookEmbed> { embed } };
            await _discord.CreateMessage(_webhookId.Value, payload);
        }
        catch (Exception e)
        {
            Log.Error($"Error sending news article to Discord: {e}");
        }
    }

    #endregion

    #region Helpers

    private bool HasJournalistAccess(EntityUid actor)
    {
        var tags = _accessReader.FindAccessTags(actor);
        return tags.Contains(JournalistAccess);
    }

    private bool HasJournalistAccessForLoader(EntityUid loaderUid)
    {
        if (!TryComp<TransformComponent>(loaderUid, out var xform))
            return false;

        var holder = xform.ParentUid;
        if (!holder.IsValid())
            return false;

        return HasJournalistAccess(holder);
    }

    /// <summary>
    /// Returns the cached summary list, rebuilding it only when invalidated (on publish).
    /// </summary>
    private List<STNewsArticleSummary> GetCachedSummaries()
    {
        if (_cachedSummaries != null)
            return _cachedSummaries;

        var summaries = new List<STNewsArticleSummary>(_articles.Count);
        foreach (var article in _articles)
        {
            summaries.Add(new STNewsArticleSummary(
                article.Id,
                article.Title,
                StripAndTruncate(article.Content, STNewsConstants.PreviewLength),
                article.Author,
                article.RoundId,
                article.PublishTime,
                article.EmbedColor));
        }

        _cachedSummaries = summaries;
        return summaries;
    }

    private STNewsArticle? FindArticleById(int id)
    {
        foreach (var article in _articles)
        {
            if (article.Id == id)
                return article;
        }

        return null;
    }

    private HashSet<int> GetNewArticleIds(STNewsCartridgeComponent comp)
    {
        var ids = new HashSet<int>();
        foreach (var article in _articles)
        {
            if (article.Id > comp.LastSeenArticleId)
                ids.Add(article.Id);
            else
                break; // articles are newest-first, so we can stop early
        }

        return ids;
    }

    private static string StripAndTruncate(string content, int maxLength)
    {
        var stripped = MarkupTagRegex.Replace(content, string.Empty);
        if (stripped.Length > maxLength)
            stripped = stripped[..maxLength] + "...";
        return stripped;
    }

    #endregion
}
