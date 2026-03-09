using Robust.Shared.Configuration;

namespace Content.Shared._Stalker_EN.CCVar;

public sealed partial class STCCVars
{
    /// <summary>
    ///     Discord webhook URL for Stalker News article notifications.
    /// </summary>
    public static readonly CVarDef<string> NewsWebhook =
        CVarDef.Create("stalkeren.news.discord_webhook", string.Empty,
            CVar.SERVERONLY | CVar.CONFIDENTIAL);

    /// <summary>
    ///     Per-player cooldown in seconds between article publishes. Default 60.
    /// </summary>
    public static readonly CVarDef<int> NewsPublishCooldownSeconds =
        CVarDef.Create("stalkeren.news.publish_cooldown_seconds", 60, CVar.SERVERONLY);

    /// <summary>
    ///     Maximum number of articles kept in the in-memory cache. Default 200.
    /// </summary>
    public static readonly CVarDef<int> NewsMaxCachedArticles =
        CVarDef.Create("stalkeren.news.max_cached_articles", 200, CVar.SERVERONLY);
}
