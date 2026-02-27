using Robust.Shared.Configuration;

namespace Content.Shared._Stalker_EN.CCVar;

public sealed partial class STCCVars
{
    /// <summary>
    ///     Discord webhook URL for faction relation change notifications.
    /// </summary>
    public static readonly CVarDef<string> FactionRelationsWebhook =
        CVarDef.Create("stalkeren.faction_relations.discord_webhook", string.Empty,
            CVar.SERVERONLY | CVar.CONFIDENTIAL);

    /// <summary>
    ///     Per-pair cooldown in seconds between faction relation changes. Default 300 (5 minutes).
    /// </summary>
    public static readonly CVarDef<int> FactionRelationsCooldownSeconds =
        CVarDef.Create("stalkeren.faction_relations.cooldown_seconds", 300, CVar.SERVERONLY);

    /// <summary>
    ///     Maximum character length for custom proposal/announcement messages.
    /// </summary>
    public static readonly CVarDef<int> FactionRelationsCustomMessageMaxLength =
        CVarDef.Create("stalkeren.faction_relations.custom_message_max_length", 250,
            CVar.SERVERONLY | CVar.REPLICATED);

    /// <summary>
    ///     Time in seconds before a pending proposal expires. 0 disables time-based expiration.
    ///     All proposals also expire on round restart regardless of this value.
    /// </summary>
    public static readonly CVarDef<int> FactionRelationsProposalExpirationSeconds =
        CVarDef.Create("stalkeren.faction_relations.proposal_expiration_seconds", 3600, CVar.SERVERONLY);
}
