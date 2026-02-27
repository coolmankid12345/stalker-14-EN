using Content.Server.CartridgeLoader;
using Content.Server.Chat.Systems;
using Content.Server.Database;
using Content.Server.Discord;
using Content.Server.GameTicking;
using Content.Shared._Stalker_EN.CCVar;
using Content.Shared._Stalker_EN.FactionRelations;
using Content.Shared.CartridgeLoader;
using Content.Shared.GameTicking;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Stalker_EN.FactionRelations;

/// <summary>
/// Server system for the faction relations PDA cartridge program.
/// Manages relation overrides, bilateral proposals, cooldowns, and announcements.
/// </summary>
public sealed class STFactionRelationsCartridgeSystem : EntitySystem
{
    [Dependency] private readonly CartridgeLoaderSystem _cartridgeLoaderSystem = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly DiscordWebhook _discord = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IServerDbManager _dbManager = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedSTFactionResolutionSystem _factionResolution = default!;

    private static ProtoId<STFactionRelationDefaultsPrototype> DefaultsProtoId => SharedSTFactionResolutionSystem.DefaultsProtoId;

    /// <summary>
    /// In-memory cache of DB overrides. Key: normalized (factionA, factionB) pair.
    /// </summary>
    private readonly Dictionary<(string, string), STFactionRelationType> _dbOverrides = new();

    /// <summary>
    /// Cached lookup from the defaults prototype for O(1) relation queries.
    /// Rebuilt on initialization and prototype reload.
    /// </summary>
    private Dictionary<(string, string), STFactionRelationType> _defaultsCache = new();

    /// <summary>
    /// Cached ordered list of faction IDs from the defaults prototype.
    /// </summary>
    private List<string>? _cachedFactionIds;

    /// <summary>
    /// Cached display names from the defaults prototype (e.g. "ClearSky" → "Clear Sky").
    /// </summary>
    private Dictionary<string, string>? _cachedDisplayNames;

    /// <summary>
    /// In-memory cache of pending bilateral proposals. Key: (initiatingFaction, targetFaction).
    /// </summary>
    private readonly Dictionary<(string, string), STFactionRelationProposalData> _pendingProposals = new();

    /// <summary>
    /// Per-pair cooldown tracker. Key: normalized pair. Value: absolute expiry time.
    /// Not persisted across restarts.
    /// </summary>
    private readonly Dictionary<(string, string), TimeSpan> _pairCooldowns = new();

    /// <summary>
    /// Maps alias factions to their primary. E.g. "Rookies" -> "Loners".
    /// Alias factions share the primary's relations and are hidden from UIs.
    /// </summary>
    private Dictionary<string, string> _aliasToPrimary = new();

    /// <summary>
    /// Maps primary factions to their alias list. E.g. "Loners" -> ["Rookies", "Neutrals"].
    /// </summary>
    private Dictionary<string, List<string>> _primaryToAliases = new();

    /// <summary>
    /// Factions that cannot be targeted by player-initiated relation changes.
    /// </summary>
    private HashSet<string> _cachedRestrictedFactions = new();

    /// <summary>
    /// Tracks loaders (PDAs) that currently have the faction relations cartridge active.
    /// Only these receive broadcast UI updates.
    /// </summary>
    private readonly HashSet<EntityUid> _activeLoaders = new();

    private bool _cacheReady;
    private WebhookIdentifier? _webhookIdentifier;

    private const int BroadcastVariants = 4;

    /// <summary>
    /// Next time to check for expired proposals. Avoids per-tick iteration.
    /// </summary>
    private TimeSpan _nextExpirationCheck;

    private static readonly TimeSpan ExpirationCheckInterval = TimeSpan.FromSeconds(30);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<STFactionRelationsCartridgeComponent, CartridgeUiReadyEvent>(OnUiReady);
        SubscribeLocalEvent<STFactionRelationsCartridgeComponent, CartridgeActivatedEvent>(OnCartridgeActivated);
        SubscribeLocalEvent<STFactionRelationsCartridgeComponent, CartridgeDeactivatedEvent>(OnCartridgeDeactivated);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);

        RebuildDefaultsCache();
        LoadFromDatabaseAsync();
        LoadProposalsFromDatabaseAsync();

        _config.OnValueChanged(STCCVars.FactionRelationsWebhook, value =>
        {
            if (!string.IsNullOrWhiteSpace(value))
                _discord.GetWebhook(value, data => _webhookIdentifier = data.ToIdentifier());
            else
                _webhookIdentifier = null;
        }, true);
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<STFactionRelationDefaultsPrototype>())
            RebuildDefaultsCache();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_cacheReady)
            return;

        if (_timing.CurTime < _nextExpirationCheck)
            return;

        _nextExpirationCheck = _timing.CurTime + ExpirationCheckInterval;
        CheckExpiredProposals();
    }

    /// <summary>
    /// Checks all pending proposals for time-based expiration and processes them.
    /// </summary>
    private void CheckExpiredProposals()
    {
        var expirationSeconds = _config.GetCVar(STCCVars.FactionRelationsProposalExpirationSeconds);
        if (expirationSeconds <= 0)
            return;

        var expirationSpan = TimeSpan.FromSeconds(expirationSeconds);
        var now = DateTime.UtcNow;
        var expired = new List<(string, string)>();

        foreach (var (key, proposal) in _pendingProposals)
        {
            if (now - proposal.CreatedAt > expirationSpan)
                expired.Add(key);
        }

        if (expired.Count == 0)
            return;

        foreach (var key in expired)
        {
            if (!_pendingProposals.TryGetValue(key, out var proposal))
                continue;

            _pendingProposals.Remove(key);
            DeleteProposalAsync(key.Item1, key.Item2);

            if (proposal.Broadcast)
            {
                BroadcastRelationAnnouncement(
                    proposal.InitiatingFaction,
                    proposal.TargetFaction,
                    proposal.ProposedRelation,
                    STFactionRelationAnnouncementKind.ProposalExpired,
                    proposal.CustomMessage);
            }
        }

        BroadcastUiUpdate();
    }

    /// <summary>
    /// Clears all pending proposals on round restart.
    /// </summary>
    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _pendingProposals.Clear();
        ClearProposalsAsync();
    }

    #region Cache Building

    /// <summary>
    /// Rebuilds the cached defaults dictionary from the YAML prototype.
    /// Also builds alias maps and restricted faction set.
    /// </summary>
    private void RebuildDefaultsCache()
    {
        _defaultsCache = new Dictionary<(string, string), STFactionRelationType>();
        _aliasToPrimary = new Dictionary<string, string>();
        _primaryToAliases = new Dictionary<string, List<string>>();
        _cachedRestrictedFactions = new HashSet<string>();
        _cachedFactionIds = null;

        if (!_protoManager.TryIndex(DefaultsProtoId, out var proto))
            return;

        foreach (var (primary, aliases) in proto.FactionGroups)
        {
            _primaryToAliases[primary] = aliases;
            foreach (var alias in aliases)
            {
                _aliasToPrimary[alias] = primary;
            }
        }

        _cachedRestrictedFactions = new HashSet<string>(proto.RestrictedFactions);

        // Keep full faction list — filtering for specific UIs is done at the caller level
        _cachedFactionIds = proto.Factions;
        _cachedDisplayNames = proto.DisplayNames;

        foreach (var rel in proto.Relations)
        {
            var key = STFactionRelationHelpers.NormalizePair(rel.FactionA, rel.FactionB);
            _defaultsCache[key] = rel.Relation;
        }
    }

    #endregion

    #region Database Loading

    private async void LoadFromDatabaseAsync()
    {
        try
        {
            var relations = await _dbManager.GetAllStalkerFactionRelationsAsync();
            foreach (var rel in relations)
            {
                var key = STFactionRelationHelpers.NormalizePair(rel.FactionA, rel.FactionB);
                _dbOverrides[key] = (STFactionRelationType) rel.RelationType;
            }
        }
        catch (Exception e)
        {
            Log.Error($"Failed to load faction relations from database: {e}");
        }
        finally
        {
            _cacheReady = true;
        }
    }

    private async void LoadProposalsFromDatabaseAsync()
    {
        try
        {
            var proposals = await _dbManager.GetAllStalkerFactionRelationProposalsAsync();
            foreach (var p in proposals)
            {
                var key = (p.InitiatingFaction, p.TargetFaction);
                _pendingProposals[key] = new STFactionRelationProposalData(
                    p.InitiatingFaction,
                    p.TargetFaction,
                    (STFactionRelationType) p.ProposedRelationType,
                    p.CustomMessage,
                    p.CreatedAt,
                    p.Broadcast);
            }
        }
        catch (Exception e)
        {
            Log.Error($"Failed to load faction relation proposals from database: {e}");
        }
    }

    #endregion

    #region UI

    private void OnUiReady(EntityUid uid, STFactionRelationsCartridgeComponent component, CartridgeUiReadyEvent args)
    {
        var state = BuildUiState();
        _cartridgeLoaderSystem.UpdateCartridgeUiState(args.Loader, state);
    }

    private void OnCartridgeActivated(EntityUid uid, STFactionRelationsCartridgeComponent component, CartridgeActivatedEvent args)
    {
        _activeLoaders.Add(args.Loader);
    }

    private void OnCartridgeDeactivated(EntityUid uid, STFactionRelationsCartridgeComponent component, CartridgeDeactivatedEvent args)
    {
        _activeLoaders.Remove(args.Loader);
    }

    /// <summary>
    /// Pushes the current faction relations state to loaders that have the faction relations cartridge active.
    /// Only iterates loaders tracked via activation events rather than querying all entities.
    /// </summary>
    private void BroadcastUiUpdate()
    {
        if (_activeLoaders.Count == 0)
            return;

        var state = BuildUiState();
        foreach (var loaderUid in _activeLoaders)
        {
            if (!TryComp<CartridgeLoaderComponent>(loaderUid, out var loaderComp))
                continue;

            _cartridgeLoaderSystem.UpdateCartridgeUiState(loaderUid, state, loader: loaderComp);
        }
    }

    public STFactionRelationsUiState BuildUiState()
    {
        if (!_protoManager.TryIndex(DefaultsProtoId, out var defaults))
            return new STFactionRelationsUiState(new List<string>(), new List<STFactionRelationEntry>());

        var factions = defaults.Factions;
        var entries = new List<STFactionRelationEntry>();

        for (var i = 0; i < factions.Count; i++)
        {
            for (var j = i + 1; j < factions.Count; j++)
            {
                var relation = GetRelation(factions[i], factions[j]);

                // Only send non-neutral entries to save bandwidth
                if (relation != STFactionRelationType.Neutral)
                {
                    var key = STFactionRelationHelpers.NormalizePair(factions[i], factions[j]);
                    entries.Add(new STFactionRelationEntry(key.Item1, key.Item2, relation));
                }
            }
        }

        return new STFactionRelationsUiState(factions, entries);
    }

    #endregion

    #region Public API

    /// <summary>
    /// Gets the current relation between two factions. Resolves aliases to their primaries.
    /// </summary>
    public STFactionRelationType GetRelation(string factionA, string factionB)
    {
        var resolvedA = ResolvePrimary(factionA);
        var resolvedB = ResolvePrimary(factionB);
        var key = STFactionRelationHelpers.NormalizePair(resolvedA, resolvedB);

        if (_dbOverrides.TryGetValue(key, out var overrideType))
            return overrideType;

        if (_defaultsCache.TryGetValue(key, out var defaultType))
            return defaultType;

        return STFactionRelationType.Neutral;
    }

    /// <summary>
    /// Gets the cached ordered list of faction display IDs from the defaults prototype.
    /// </summary>
    public List<string>? GetFactionIds()
    {
        return _cachedFactionIds;
    }

    /// <summary>
    /// Gets the cached display names dictionary from the defaults prototype.
    /// </summary>
    public Dictionary<string, string>? GetDisplayNames()
    {
        return _cachedDisplayNames;
    }

    /// <summary>
    /// Resolves a band prototype name (e.g. "Dolg") to a faction relation name (e.g. "Duty").
    /// Delegates to <see cref="SharedSTFactionResolutionSystem"/> for O(1) cached lookup.
    /// </summary>
    public string? GetBandFactionName(string bandName)
    {
        return _factionResolution.GetBandFactionName(bandName);
    }

    /// <summary>
    /// Gets pending proposals involving a faction (both incoming and outgoing).
    /// </summary>
    public (List<STFactionRelationProposalData> Incoming, List<STFactionRelationProposalData> Outgoing) GetProposalsForFaction(string faction)
    {
        var incoming = new List<STFactionRelationProposalData>();
        var outgoing = new List<STFactionRelationProposalData>();

        foreach (var proposal in _pendingProposals.Values)
        {
            if (proposal.TargetFaction == faction)
                incoming.Add(proposal);
            else if (proposal.InitiatingFaction == faction)
                outgoing.Add(proposal);
        }

        return (incoming, outgoing);
    }

    /// <summary>
    /// Gets the remaining cooldown in seconds for a faction pair. Returns 0 if not on cooldown.
    /// Resolves aliases to their primaries.
    /// </summary>
    public float GetCooldownRemaining(string factionA, string factionB)
    {
        var resolvedA = ResolvePrimary(factionA);
        var resolvedB = ResolvePrimary(factionB);
        var key = STFactionRelationHelpers.NormalizePair(resolvedA, resolvedB);

        if (_pairCooldowns.TryGetValue(key, out var expiry) && expiry > _timing.CurTime)
            return (float) (expiry - _timing.CurTime).TotalSeconds;

        return 0f;
    }

    /// <summary>
    /// Resolves a faction alias to its primary. Returns the input if not an alias.
    /// </summary>
    public string ResolvePrimary(string faction)
    {
        return _aliasToPrimary.GetValueOrDefault(faction, faction);
    }

    /// <summary>
    /// Returns true if the faction is an alias of another faction.
    /// </summary>
    public bool IsAlias(string faction)
    {
        return _aliasToPrimary.ContainsKey(faction);
    }

    /// <summary>
    /// Returns true if the resolved faction is restricted (admin-only changes).
    /// </summary>
    public bool IsFactionRestricted(string faction)
    {
        var resolved = ResolvePrimary(faction);
        return _cachedRestrictedFactions.Contains(resolved);
    }

    #endregion

    #region Relation Changes

    /// <summary>
    /// Attempts a faction relation change. Handles cooldown checking and determines whether
    /// the change is instant (escalation) or creates a proposal (cooperation).
    /// For bilateral proposals, the <paramref name="broadcast"/> flag controls whether
    /// intermediate states (sent/rejected/expired) are announced to all players.
    /// Unilateral changes and accepted proposals are always announced.
    /// </summary>
    public STFactionRelationChangeResult TryChangeRelation(
        EntityUid initiatorUid,
        string initiatingFaction,
        string targetFaction,
        STFactionRelationType proposedRelation,
        string? customMessage,
        bool broadcast = true)
    {
        initiatingFaction = ResolvePrimary(initiatingFaction);
        targetFaction = ResolvePrimary(targetFaction);

        var factionIds = GetFactionIds();
        if (factionIds == null || !factionIds.Contains(initiatingFaction) || !factionIds.Contains(targetFaction))
            return STFactionRelationChangeResult.InvalidFaction;

        if (initiatingFaction == targetFaction)
            return STFactionRelationChangeResult.InvalidFaction;

        if (IsFactionRestricted(targetFaction))
            return STFactionRelationChangeResult.RestrictedFaction;

        var currentRelation = GetRelation(initiatingFaction, targetFaction);
        if (currentRelation == proposedRelation)
            return STFactionRelationChangeResult.SameRelation;

        var key = STFactionRelationHelpers.NormalizePair(initiatingFaction, targetFaction);
        if (_pairCooldowns.TryGetValue(key, out var expiry) && expiry > _timing.CurTime)
            return STFactionRelationChangeResult.OnCooldown;

        var maxLen = _config.GetCVar(STCCVars.FactionRelationsCustomMessageMaxLength);
        if (customMessage != null && customMessage.Length > maxLen)
            customMessage = customMessage[..maxLen];

        var cooldownSeconds = _config.GetCVar(STCCVars.FactionRelationsCooldownSeconds);
        _pairCooldowns[key] = _timing.CurTime + TimeSpan.FromSeconds(cooldownSeconds);

        if (STFactionRelationHelpers.RequiresConfirmation(currentRelation, proposedRelation))
        {
            var proposalKey = (initiatingFaction, targetFaction);
            var data = new STFactionRelationProposalData(
                initiatingFaction,
                targetFaction,
                proposedRelation,
                customMessage,
                DateTime.UtcNow,
                broadcast);

            _pendingProposals[proposalKey] = data;
            SaveProposalAsync(initiatingFaction, targetFaction, (int) proposedRelation, customMessage, broadcast);

            if (broadcast)
            {
                BroadcastRelationAnnouncement(
                    initiatingFaction,
                    targetFaction,
                    proposedRelation,
                    STFactionRelationAnnouncementKind.ProposalSent,
                    customMessage);
            }

            BroadcastUiUpdate();
            return STFactionRelationChangeResult.ProposalCreated;
        }

        // Unilateral escalation -- apply immediately (always broadcast)
        SetRelation(initiatingFaction, targetFaction, proposedRelation,
            broadcast: true,
            customMessage: customMessage,
            kind: STFactionRelationAnnouncementKind.DirectChange);

        return STFactionRelationChangeResult.Success;
    }

    /// <summary>
    /// Accepts a pending proposal targeting the accepting faction.
    /// Always broadcasts the resulting relation change.
    /// </summary>
    public STFactionRelationChangeResult AcceptProposal(
        EntityUid acceptorUid,
        string acceptingFaction,
        string initiatingFaction)
    {
        var proposalKey = (initiatingFaction, acceptingFaction);
        if (!_pendingProposals.TryGetValue(proposalKey, out var proposal))
            return STFactionRelationChangeResult.ProposalNotFound;

        _pendingProposals.Remove(proposalKey);
        DeleteProposalAsync(initiatingFaction, acceptingFaction);

        SetRelation(initiatingFaction, acceptingFaction, proposal.ProposedRelation,
            broadcast: true,
            customMessage: proposal.CustomMessage,
            kind: STFactionRelationAnnouncementKind.ProposalAccepted);

        return STFactionRelationChangeResult.ProposalAccepted;
    }

    /// <summary>
    /// Rejects a pending proposal targeting the rejecting faction.
    /// Only broadcasts if the original proposal had broadcast enabled.
    /// </summary>
    public STFactionRelationChangeResult RejectProposal(
        string rejectingFaction,
        string initiatingFaction)
    {
        var proposalKey = (initiatingFaction, rejectingFaction);
        if (!_pendingProposals.TryGetValue(proposalKey, out var proposal))
            return STFactionRelationChangeResult.ProposalNotFound;

        _pendingProposals.Remove(proposalKey);
        DeleteProposalAsync(initiatingFaction, rejectingFaction);

        if (proposal.Broadcast)
        {
            BroadcastRelationAnnouncement(
                initiatingFaction,
                rejectingFaction,
                proposal.ProposedRelation,
                STFactionRelationAnnouncementKind.ProposalRejected,
                proposal.CustomMessage);
        }

        BroadcastUiUpdate();
        return STFactionRelationChangeResult.ProposalRejected;
    }

    /// <summary>
    /// Cancels an outgoing proposal.
    /// </summary>
    public void CancelProposal(string initiatingFaction, string targetFaction)
    {
        var proposalKey = (initiatingFaction, targetFaction);
        if (!_pendingProposals.ContainsKey(proposalKey))
            return;

        _pendingProposals.Remove(proposalKey);
        DeleteProposalAsync(initiatingFaction, targetFaction);
        BroadcastUiUpdate();
    }

    /// <summary>
    /// Sets a faction relation directly. Used by admin commands and internally after confirmation.
    /// </summary>
    public void SetRelation(
        string factionA,
        string factionB,
        STFactionRelationType type,
        bool broadcast = true,
        string? customMessage = null,
        STFactionRelationAnnouncementKind kind = STFactionRelationAnnouncementKind.DirectChange)
    {
        // Resolve aliases so DB stores under the primary faction
        factionA = ResolvePrimary(factionA);
        factionB = ResolvePrimary(factionB);

        if (factionA == factionB)
            return;

        var oldRelation = GetRelation(factionA, factionB);

        var key = STFactionRelationHelpers.NormalizePair(factionA, factionB);
        _dbOverrides[key] = type;
        SaveRelationAsync(key.Item1, key.Item2, (int) type);

        if (oldRelation != type)
        {
            if (broadcast)
                BroadcastRelationAnnouncement(factionA, factionB, type, kind, customMessage);

            SendDiscordRelationChange(factionA, factionB, oldRelation, type);
        }

        // Remove any pending proposals between these factions since the relation just changed
        RemoveProposalsBetween(factionA, factionB);

        BroadcastUiUpdate();
    }

    /// <summary>
    /// Clears all DB overrides and reverts to YAML defaults.
    /// Also clears all pending proposals.
    /// </summary>
    public void ResetAllRelations()
    {
        _dbOverrides.Clear();
        _pendingProposals.Clear();
        _pairCooldowns.Clear();
        ClearRelationsAsync();
        ClearProposalsAsync();
        BroadcastUiUpdate();
    }

    #endregion

    #region Database Persistence

    private async void SaveRelationAsync(string factionA, string factionB, int relationType)
    {
        try
        {
            await _dbManager.SetStalkerFactionRelationAsync(factionA, factionB, relationType);
        }
        catch (Exception e)
        {
            Log.Error($"Failed to save faction relation to database: {e}");
        }
    }

    private async void ClearRelationsAsync()
    {
        try
        {
            await _dbManager.ClearAllStalkerFactionRelationsAsync();
        }
        catch (Exception e)
        {
            Log.Error($"Failed to clear faction relations from database: {e}");
        }
    }

    private async void SaveProposalAsync(string initiatingFaction, string targetFaction, int proposedRelationType, string? customMessage, bool broadcast)
    {
        try
        {
            await _dbManager.SetStalkerFactionRelationProposalAsync(initiatingFaction, targetFaction, proposedRelationType, customMessage, broadcast);
        }
        catch (Exception e)
        {
            Log.Error($"Failed to save faction relation proposal to database: {e}");
        }
    }

    private async void DeleteProposalAsync(string initiatingFaction, string targetFaction)
    {
        try
        {
            await _dbManager.DeleteStalkerFactionRelationProposalAsync(initiatingFaction, targetFaction);
        }
        catch (Exception e)
        {
            Log.Error($"Failed to delete faction relation proposal from database: {e}");
        }
    }

    private async void ClearProposalsAsync()
    {
        try
        {
            await _dbManager.ClearAllStalkerFactionRelationProposalsAsync();
        }
        catch (Exception e)
        {
            Log.Error($"Failed to clear faction relation proposals from database: {e}");
        }
    }

    /// <summary>
    /// Removes any pending proposals between two factions (in either direction).
    /// </summary>
    private void RemoveProposalsBetween(string factionA, string factionB)
    {
        var keyAB = (factionA, factionB);
        var keyBA = (factionB, factionA);

        if (_pendingProposals.Remove(keyAB))
            DeleteProposalAsync(factionA, factionB);

        if (_pendingProposals.Remove(keyBA))
            DeleteProposalAsync(factionB, factionA);
    }

    #endregion

    #region Announcements

    /// <summary>
    /// Broadcasts a faction relation announcement to all in-game players.
    /// Picks a random lore-friendly message variant based on the announcement kind.
    /// Appends custom message when provided.
    /// </summary>
    private void BroadcastRelationAnnouncement(
        string factionA,
        string factionB,
        STFactionRelationType relation,
        STFactionRelationAnnouncementKind kind,
        string? customMessage = null)
    {
        var relationKey = GetRelationKey(relation);
        var kindPrefix = kind switch
        {
            STFactionRelationAnnouncementKind.DirectChange => "",
            STFactionRelationAnnouncementKind.ProposalSent => "proposal-",
            STFactionRelationAnnouncementKind.ProposalAccepted => "accepted-",
            STFactionRelationAnnouncementKind.ProposalRejected => "rejected-",
            STFactionRelationAnnouncementKind.ProposalExpired => "expired-",
            _ => "",
        };

        var variant = _random.Next(1, BroadcastVariants + 1);
        var displayA = STFactionRelationHelpers.GetDisplayName(factionA, _cachedDisplayNames);
        var displayB = STFactionRelationHelpers.GetDisplayName(factionB, _cachedDisplayNames);
        var message = Loc.GetString($"st-faction-relations-{kindPrefix}{relationKey}-{variant}",
            ("factionA", displayA),
            ("factionB", displayB));

        if (!string.IsNullOrWhiteSpace(customMessage))
        {
            var maxLen = _config.GetCVar(STCCVars.FactionRelationsCustomMessageMaxLength);
            var trimmed = customMessage.Length > maxLen ? customMessage[..maxLen] : customMessage;
            var suffix = Loc.GetString("st-faction-relations-custom-message", ("message", trimmed));
            message = $"{message} {suffix}";
        }

        var color = GetRelationColor(relation);
        var filter = Filter.Empty().AddWhere(_gameTicker.UserHasJoinedGame);

        _chatSystem.DispatchFilteredAnnouncement(
            filter,
            message,
            sender: "S.T.A.L.K.E.R Network",
            playSound: false,
            colorOverride: color);
    }

    /// <summary>
    /// Sends a faction relation change notification to the configured Discord webhook.
    /// </summary>
    private async void SendDiscordRelationChange(
        string factionA,
        string factionB,
        STFactionRelationType oldRelation,
        STFactionRelationType newRelation)
    {
        if (_webhookIdentifier is null)
            return;

        try
        {
            var payload = new WebhookPayload
            {
                Embeds = new List<WebhookEmbed>
                {
                    new()
                    {
                        Title = "Faction Relations Changed",
                        Description = $"**{STFactionRelationHelpers.GetDisplayName(factionA, _cachedDisplayNames)}** ↔ **{STFactionRelationHelpers.GetDisplayName(factionB, _cachedDisplayNames)}**",
                        Color = GetRelationDiscordColor(newRelation),
                        Fields = new List<WebhookEmbedField>
                        {
                            new()
                            {
                                Name = "Old Relation",
                                Value = GetRelationDisplayString(oldRelation),
                                Inline = true,
                            },
                            new()
                            {
                                Name = "New Relation",
                                Value = GetRelationDisplayString(newRelation),
                                Inline = true,
                            },
                        },
                    },
                },
            };

            await _discord.CreateMessage(_webhookIdentifier.Value, payload);
        }
        catch (Exception e)
        {
            Log.Error($"Error sending faction relation change to Discord:\n{e}");
        }
    }

    #endregion

    #region Helpers

    private static Color GetRelationColor(STFactionRelationType type)
    {
        return STFactionRelationColors.GetColor(type) ?? Color.White;
    }

    /// <summary>
    /// Maps a relation type to its lowercase locale key fragment.
    /// </summary>
    private static string GetRelationKey(STFactionRelationType type) => type switch
    {
        STFactionRelationType.Alliance => "alliance",
        STFactionRelationType.Neutral => "neutral",
        STFactionRelationType.Hostile => "hostile",
        STFactionRelationType.War => "war",
        _ => "neutral",
    };

    /// <summary>
    /// Maps a relation type to a display string for external integrations (e.g. Discord).
    /// </summary>
    private static string GetRelationDisplayString(STFactionRelationType type) => type switch
    {
        STFactionRelationType.Alliance => "Alliance",
        STFactionRelationType.Neutral => "Neutral",
        STFactionRelationType.Hostile => "Hostile",
        STFactionRelationType.War => "War",
        _ => "Neutral",
    };

    private static int GetRelationDiscordColor(STFactionRelationType type)
    {
        return type switch
        {
            STFactionRelationType.Alliance => 0x2d7019,
            STFactionRelationType.Neutral => 0xb8a900,
            STFactionRelationType.Hostile => 0xc87000,
            STFactionRelationType.War => 0xa01000,
            _ => 0xFFFFFF,
        };
    }

    #endregion
}

/// <summary>
/// Internal data record for a pending faction relation proposal.
/// </summary>
public sealed record STFactionRelationProposalData(
    string InitiatingFaction,
    string TargetFaction,
    STFactionRelationType ProposedRelation,
    string? CustomMessage,
    DateTime CreatedAt,
    bool Broadcast);
