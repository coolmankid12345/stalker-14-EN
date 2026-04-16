using Content.Server.Administration.Logs;
using Content.Server.CartridgeLoader;
using Content.Server.Database;
using Content.Server.Discord;
using Content.Server.Mind;
using Content.Server.PDA;
using Content.Server.PDA.Ringer;
using Content.Shared._Stalker.Bands;
using Content.Shared._Stalker_EN.CCVar;
using Content.Shared._Stalker_EN.CharacterRank;
using Content.Shared._Stalker_EN.FactionRelations;
using Content.Shared._Stalker_EN.BulletinBoard;
using Content.Shared._Stalker_EN.News;
using Content.Shared._Stalker_EN.PdaMessenger;
using Content.Shared._Stalker.PdaMessenger;
using Content.Shared.CartridgeLoader;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.Hands;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.PDA;
using Content.Shared.PDA.Ringer;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Stalker_EN.PdaMessenger;

/// <summary>
/// Server system for the stalker messenger PDA cartridge.
/// Handles message routing, contacts, muting, unread tracking, DB persistence,
/// Discord webhook notifications, and round lifecycle cleanup.
/// </summary>
public sealed partial class STMessengerSystem : EntitySystem
{
    [Dependency] private readonly CartridgeLoaderSystem _cartridgeLoader = default!;
    [Dependency] private readonly DiscordWebhook _discord = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly PdaSystem _pda = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly RingerSystem _ringer = default!;
    [Dependency] private readonly SharedSTFactionResolutionSystem _factionResolution = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly MindSystem _mind = default!;

    private const int MaxChannelMessages = 200;
    private const int MaxDmMessages = 100;
    private const int MaxContacts = 50;
    private const int MaxRetryCollision = 10;
    private const int MaxPseudonymSuffix = 999;
    private static readonly TimeSpan InteractionCooldown = TimeSpan.FromSeconds(0.5);
    private static readonly ProtoId<STBandPrototype> ClearSkyBandId = "STClearSkyBand";

    /// <summary>
    /// Maps (userId, charName) → anonymous pseudonym for the current round.
    /// Cleared on round restart so each round gets fresh pseudonyms.
    /// </summary>
    private readonly Dictionary<(Guid, string), string> _anonymousPseudonyms = new();

    /// <summary>
    /// Global set of all pseudonyms in use this round to prevent collisions.
    /// </summary>
    private readonly HashSet<string> _usedPseudonyms = new();

    /// <summary>
    /// Fixed anonymous display name for channel messages (EN-fork only).
    /// </summary>
    private const string AnonymousName = "Stalker";

    /// <summary>
    /// PDAs with messenger cartridge currently active (UI open). Receive broadcast updates.
    /// </summary>
    private readonly HashSet<EntityUid> _activeLoaders = new();

    /// <summary>
    /// Per-loader currently viewed chat ID for lazy message loading.
    /// Null = main page (no chat open).
    /// </summary>
    private readonly Dictionary<EntityUid, string?> _viewedChat = new();

    /// <summary>
    /// Server-side channel message storage. Key = channel prototype ID.
    /// </summary>
    private readonly Dictionary<string, List<STMessengerMessage>> _channelChats = new();

    /// <summary>
    /// Server-side DM message storage. Key = normalized "idA:idB" (alphabetical messenger IDs).
    /// </summary>
    private readonly Dictionary<string, List<STMessengerMessage>> _dmChats = new();

    /// <summary>
    /// Per-chat auto-increment message ID counter. Key = chat ID.
    /// </summary>
    private readonly Dictionary<string, uint> _nextMessageId = new();

    /// <summary>
    /// Maps messenger ID ("XXX-XXX") → (UserId, CharacterName). Bulk-loaded at system init.
    /// </summary>
    private readonly Dictionary<string, (Guid UserId, string CharName)> _messengerIdCache = new();

    /// <summary>
    /// Maps (UserId, CharacterName) → their PDA's cartridge EntityUid (for O(1) DM recipient lookup).
    /// Updated on <see cref="PlayerSpawnCompleteEvent"/>, cleaned up on entity deletion.
    /// </summary>
    private readonly Dictionary<(Guid, string), EntityUid> _characterToPda = new();

    /// <summary>
    /// Reverse lookup: (UserId, CharacterName) → messenger ID ("XXX-XXX").
    /// Populated alongside <see cref="_messengerIdCache"/> and persists across rounds.
    /// </summary>
    private readonly Dictionary<(Guid, string), string> _characterToMessengerId = new();

    /// <summary>
    /// Cached set of all PDAs that have a messenger cartridge.
    /// Avoids full entity query in <see cref="NotifyChannelRecipients"/>.
    /// Stores (CartridgeUid, PdaUid) — resolve components via TryComp to avoid stale references.
    /// </summary>
    private readonly Dictionary<EntityUid, (EntityUid Cartridge, EntityUid Pda)>
        _messengerPdas = new();

    /// <summary>
    /// Channel prototypes sorted by <see cref="STMessengerChannelPrototype.SortOrder"/>.
    /// Cached to avoid sorting + prototype lookups on every <see cref="BuildUiState"/> call.
    /// </summary>
    private List<STMessengerChannelPrototype> _sortedChannels = new();

    private WebhookIdentifier? _webhookIdentifier;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STMessengerComponent, CartridgeUiReadyEvent>(OnUiReady);
        SubscribeLocalEvent<STMessengerComponent, CartridgeActivatedEvent>(OnCartridgeActivated);
        SubscribeLocalEvent<STMessengerComponent, CartridgeDeactivatedEvent>(OnCartridgeDeactivated);
        SubscribeLocalEvent<STMessengerComponent, CartridgeMessageEvent>(OnMessage);
        SubscribeLocalEvent<STMessengerServerComponent, EntityTerminatingEvent>(OnMessengerTerminating);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<PdaComponent, GotEquippedEvent>(OnPdaEquipped);
        SubscribeLocalEvent<PdaComponent, GotEquippedHandEvent>(OnPdaPickedUp);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);

        CacheSortedChannels();

        foreach (var proto in _sortedChannels)
        {
            _channelChats.TryAdd(proto.ID, new List<STMessengerMessage>());
        }

        LoadMessengerIdCacheAsync();

        _config.OnValueChanged(STCCVars.MessengerDiscordWebhook, OnWebhookChanged, true);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _config.UnsubValueChanged(STCCVars.MessengerDiscordWebhook, OnWebhookChanged);
    }

    private void OnWebhookChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            _discord.GetWebhook(value, data => _webhookIdentifier = data.ToIdentifier());
        else
            _webhookIdentifier = null;
    }

    #region Cartridge Events

    private void OnUiReady(Entity<STMessengerComponent> ent, ref CartridgeUiReadyEvent args)
    {
        if (!TryComp<STMessengerServerComponent>(ent, out var server))
            return;

        UpdateUiState(ent, args.Loader, server);
    }

    private void OnCartridgeActivated(Entity<STMessengerComponent> ent, ref CartridgeActivatedEvent args)
    {
        _activeLoaders.Add(args.Loader);
    }

    private void OnCartridgeDeactivated(Entity<STMessengerComponent> ent, ref CartridgeDeactivatedEvent args)
    {
        _activeLoaders.Remove(args.Loader);
        _viewedChat.Remove(args.Loader);
    }

    private void OnMessengerTerminating(Entity<STMessengerServerComponent> ent, ref EntityTerminatingEvent args)
    {
        // Guard against race: only remove if this entity is still the registered PDA for this character
        if (!string.IsNullOrEmpty(ent.Comp.OwnerCharacterName))
        {
            var key = (ent.Comp.OwnerUserId, ent.Comp.OwnerCharacterName);

            if (_characterToPda.TryGetValue(key, out var existing) && existing == ent.Owner)
                _characterToPda.Remove(key);
        }

        // The loader is the PDA entity that owns this cartridge
        if (TryComp<TransformComponent>(ent, out var xform) && xform.ParentUid.IsValid())
        {
            _activeLoaders.Remove(xform.ParentUid);
            _viewedChat.Remove(xform.ParentUid);
            _messengerPdas.Remove(xform.ParentUid);
        }
    }

    private void OnMessage(Entity<STMessengerComponent> ent, ref CartridgeMessageEvent args)
    {
        if (!TryComp<STMessengerServerComponent>(ent, out var server))
            return;

        switch (args)
        {
            case STMessengerSendEvent send:
                OnSendMessage(ent, server, send, args);
                break;
            case STMessengerAddContactEvent add:
                OnAddContact(ent, server, add, args);
                break;
            case STMessengerRemoveContactEvent remove:
                OnRemoveContact(ent, server, remove, args);
                break;
            case STMessengerToggleMuteEvent mute:
                OnToggleMute(ent, server, mute, args);
                break;
            case STMessengerMarkReadEvent markRead:
                OnMarkRead(server, markRead);
                break;
            case STMessengerViewChatEvent viewChat:
                OnViewChat(args.LoaderUid, viewChat);
                break;
            case STMessengerNavigateToOfferEvent navigateToOffer:
                OnNavigateToOffer(args.LoaderUid, navigateToOffer);
                break;
            case STMessengerNavigateToNewsEvent navigateToNews:
                OnNavigateToNews(args.LoaderUid, navigateToNews);
                break;
        }
    }

    #endregion

    #region Message Handling

    private void OnSendMessage(
        Entity<STMessengerComponent> ent,
        STMessengerServerComponent server,
        STMessengerSendEvent send,
        CartridgeMessageEvent args)
    {
        if (server.NextSendTime > _timing.CurTime)
            return;

        server.NextSendTime = _timing.CurTime + server.SendCooldown;

        var content = send.Content.Trim();
        if (string.IsNullOrEmpty(content))
            return;

        var maxLen = _config.GetCVar(STCCVars.MessengerMaxMessageLength);
        if (content.Length > maxLen)
            content = content[..maxLen];

        // Resolve sender name from stored owner name (survives entity deletion)
        if (string.IsNullOrEmpty(server.OwnerCharacterName))
            return;

        ProcessSendMessage(ent, server, send.TargetChatId, content, send.IsAnonymous,
            send.ReplyToId, args.Actor, GetEntity(args.LoaderUid));
    }

    /// <summary>
    /// Core message processing: routing, creation, eviction, logging, notifications, broadcast.
    /// </summary>
    private void ProcessSendMessage(
        Entity<STMessengerComponent> ent,
        STMessengerServerComponent server,
        string chatId,
        string content,
        bool isAnonymous,
        uint? replyToId,
        EntityUid actor,
        EntityUid loaderUid)
    {
        var senderName = server.OwnerCharacterName;
        var senderKey = (server.OwnerUserId, senderName);
        var isDm = chatId.StartsWith(STMessengerChat.DmChatPrefix, StringComparison.Ordinal);

        // Effective anonymous flag: only allowed for non-DM channels that explicitly permit it
        isAnonymous = isAnonymous && !isDm;

        // Determine display name: anonymous pseudonym for channels, real name for DMs
        var displayName = isAnonymous
            ? GetOrCreatePseudonym(senderKey)
            : senderName;

        string? replySnippet = null;
        if (replyToId is { } replyId)
        {
            replySnippet = FindReplySnippet(chatId, isDm, server, replyId);
        }

        List<STMessengerMessage> chatMessages;
        int maxMessages;
        string storageKey;
        STMessengerChannelPrototype? channelProto = null;

        if (isDm)
        {
            var contactMessengerId = chatId[STMessengerChat.DmChatPrefix.Length..];

            // Only allow DMs to contacts (prevents unbounded DM chat creation)
            if (!server.Contacts.TryGetValue(contactMessengerId, out var contactEntry))
                return;

            // Check if contact's faction changed (only update with non-null — preserve last-known on resolution failure)
            var contactKey = (contactEntry.UserId, contactEntry.CharacterName);
            var currentFaction = ResolveContactFaction(contactKey);
            if (currentFaction is not null && currentFaction != contactEntry.FactionName)
            {
                contactEntry.FactionName = currentFaction;
                UpdateContactFactionAsync(server.OwnerUserId, server.OwnerCharacterName,
                    contactEntry.UserId, contactEntry.CharacterName, currentFaction);
            }

            storageKey = NormalizeDmKey(server.MessengerId, contactMessengerId);
            chatMessages = _dmChats.GetOrNew(storageKey);
            maxMessages = MaxDmMessages;
        }
        else
        {
            // Validate that the channel prototype exists to prevent clients from polluting storage
            if (!_protoManager.TryIndex(chatId, out channelProto))
                return;

            // Re-resolve the holder's band before access check (band may have changed)
            if (TryComp<TransformComponent>(loaderUid, out var pdaXform))
            {
                var holder = pdaXform.ParentUid;
                if (holder.IsValid())
                    server.OwnerBand = ResolveMobBand(holder);
            }

            if (!HasChannelAccess(channelProto, server))
                return;

            // Enforce per-channel anonymous setting
            if (isAnonymous && !channelProto.AllowAnonymous)
            {
                isAnonymous = false;
                displayName = senderName;
            }

            storageKey = chatId;
            chatMessages = _channelChats.GetOrNew(storageKey);
            maxMessages = MaxChannelMessages;
        }

        _nextMessageId.TryAdd(storageKey, 0);
        var msgId = ++_nextMessageId[storageKey];

        // Resolve faction and rank for non-anonymous messages; null hides them on anonymous messages
        string? senderFaction = !isAnonymous
            ? ResolveContactFaction(senderKey)
            : null;
        string? senderRankIcon = !isAnonymous
            ? ResolveContactRankIcon(senderKey)
            : null;

        var message = new STMessengerMessage(
            msgId,
            displayName,
            content,
            _timing.CurTime,
            replyToId,
            replySnippet,
            senderFaction,
            senderRankIcon);

        chatMessages.Add(message);

        // Mark sender's own message as read
        server.LastSeenMessageId[chatId] = msgId;

        if (chatMessages.Count > maxMessages)
        {
            var evictCount = chatMessages.Count - maxMessages;
            chatMessages.RemoveRange(0, evictCount);
        }

        // Admin log — include anonymous pseudonym so admins can trace abuse
        var replyInfo = replyToId is { } rid
            ? $" (reply to #{rid}: \"{replySnippet}\")"
            : "";

        if (isAnonymous)
        {
            _adminLogger.Add(LogType.STMessenger, LogImpact.Medium,
                $"{ToPrettyString(actor):player} sent anonymous message " +
                $"(as \"{displayName}\") to {chatId}{replyInfo}: {content}");
        }
        else
        {
            _adminLogger.Add(LogType.STMessenger, LogImpact.Medium,
                $"{ToPrettyString(actor):player} sent message to {chatId}{replyInfo}: {content}");
        }

        if (isDm)
        {
            var contactMessengerId = chatId[STMessengerChat.DmChatPrefix.Length..];
            if (!server.Contacts.TryGetValue(contactMessengerId, out var contactEntry))
                return;

            var contactKey = (contactEntry.UserId, contactEntry.CharacterName);

            // DM: auto-add sender to recipient's contacts so they can reply
            if (_characterToPda.TryGetValue(contactKey, out var recipientPdaUid)
                && _cartridgeLoader.TryGetProgram<STMessengerServerComponent>(
                    recipientPdaUid, out _, out var recipientServer))
            {
                if (!recipientServer.Contacts.ContainsKey(server.MessengerId))
                {
                    var dmSenderFaction = ResolveContactFaction(senderKey);
                    recipientServer.Contacts[server.MessengerId] = new STContactEntry(
                        server.OwnerUserId, senderName, dmSenderFaction);
                    AddContactAsync(recipientServer.OwnerUserId, recipientServer.OwnerCharacterName,
                        server.OwnerUserId, senderName, dmSenderFaction);
                }
            }

            // Send pop-up notification to DM recipient
            var bandIcon = GetBandIcon(server);
            var dmEvent = new PdaDirectMessageEvent(senderName, content, bandIcon);
            if (_playerManager.TryGetSessionById(new NetUserId(contactKey.UserId), out var recipientSession) &&
            recipientSession.AttachedEntity is { } currentMob &&
            MetaData(currentMob).EntityName == contactEntry.CharacterName)
            {
                RaiseNetworkEvent(dmEvent, recipientSession);
            }

            NotifyDmRecipient(contactKey, server);
        }
        else
        {
            if (channelProto!.BroadcastToDiscord)
            {
                SendDiscordWebhook(chatId, displayName, content);
            }

            NotifyChannelRecipients(channelProto, server);

            // Send pop-up notification for General channel
            if (channelProto.ID == "STGeneral")
            {
                var bandIcon = GetBandIcon(server);
                var generalEvent = new PdaGeneralMessageEvent(displayName, content, displayName, bandIcon);

                foreach (var (pdaUid, (cartridgeUid, _)) in _messengerPdas)
                {

                    // 1. Check for PDA and its owner (mob)
                    if (!TryComp(pdaUid, out PdaComponent? pdaComp) || !pdaComp.PdaOwner.HasValue)
                        continue;

                    var mobUid = pdaComp.PdaOwner.Value;

                    // 2. Find the mob's Mind to get the player's UserId
                    if (_mind.TryGetMind(mobUid, out var _, out var mindComp))
                    {
                        // 4. Find the session by UserId
                        if (_playerManager.TryGetSessionById(mindComp.UserId, out var session))
                        {
                            RaiseNetworkEvent(generalEvent, session!);
                        }
                    }
                }
            }
        }

        BroadcastUiUpdate(chatId);
    }

    /// <summary>
    /// Gets the band icon name for a player based on their band/faction.
    /// Uses the mob holding the PDA (not the PDA entity itself).
    /// Falls back to OwnerBand if mob is not available.
    /// Clear Sky is disguised as Stalker for lore consistency.
    /// </summary>
    private string? GetBandIcon(STMessengerServerComponent server)
    {
        // Try 1: Get bandIcon from the mob holding the PDA
        if (TryComp<TransformComponent>(server.Owner, out var xform))
        {
            var holder = xform.ParentUid;
            if (holder.IsValid() && TryComp<BandsComponent>(holder, out var bands))
            {
                // Clear Sky is disguised as Loners on PDA (lore consistency)
                if (bands.BandProto == ClearSkyBandId)
                    return "stalker";

                if (!string.IsNullOrEmpty(bands.BandStatusIcon))
                    return bands.BandStatusIcon;
            }
        }

        // Try 2: Fallback to OwnerBand and map to bandIcon
        if (server.OwnerBand.HasValue)
        {
            // Clear Sky disguise
            if (server.OwnerBand.Value == ClearSkyBandId)
                return "stalker";

            return GetBandIconForBandProto(server.OwnerBand.Value);
        }

        return null;
    }

    /// <summary>
    /// Maps band prototype ID to bandIcon name.
    /// </summary>
    private static string? GetBandIconForBandProto(ProtoId<STBandPrototype> bandProtoId)
    {
        return bandProtoId.Id switch
        {
            "STFreedomBand" => "freedom",
            "STDolgBand" => "Dolg",
            "STBanditsBand" => "band",
            "STRenegatsBand" => "rene",
            "STMonolithBand" => "monolith",
            "STClearSkyBand" => "cn",
            "STStalkerBand" => "stalker",
            "STMercenariesBand" => "merc",
            "STMilitaryBand" => "army",
            "STSciBand" => "sci",
            "STMilitiaBand" => "militia",
            "STAnomalistsBand" => "ecologists",
            "STSeraphimsBand" => "seraphim",
            "STCovenantBand" => "zavet",
            "STGrehBand" => "greh",
            "STSsuBand" => "sbu",
            "STUNBand" => "un",
            "STProjectBand" => "project-1",
            "STToadsBand" => "jaba",
            _ => "stalker" // Default
        };
    }

    private string? FindReplySnippet(string chatId, bool isDm, STMessengerServerComponent server, uint replyId)
    {
        List<STMessengerMessage>? messages = null;

        if (isDm)
        {
            var contactMessengerId = chatId[STMessengerChat.DmChatPrefix.Length..];
            var key = NormalizeDmKey(server.MessengerId, contactMessengerId);
            _dmChats.TryGetValue(key, out messages);
        }
        else
        {
            _channelChats.TryGetValue(chatId, out messages);
        }

        if (messages is null)
            return null;

        foreach (var msg in messages)
        {
            if (msg.Id != replyId)
                continue;

            return msg.Content.Length > STMessengerChat.MaxReplySnippetLength
                ? msg.Content[..STMessengerChat.MaxReplySnippetLength] + "..."
                : msg.Content;
        }

        return null;
    }

    private void NotifyDmRecipient((Guid UserId, string CharName) contactKey, STMessengerServerComponent senderServer)
    {
        if (!_characterToPda.TryGetValue(contactKey, out var recipientPdaUid))
            return;

        if (!TryComp<CartridgeLoaderComponent>(recipientPdaUid, out _))
            return;

        // Play ringer if not muted (DMs are never muted via channel mute, so always ring)
        if (TryComp<RingerComponent>(recipientPdaUid, out var ringer))
            _ringer.RingerPlayRingtone((recipientPdaUid, ringer));
    }

    private void NotifyChannelRecipients(STMessengerChannelPrototype channelProto, STMessengerServerComponent senderServer)
    {
        // Use cached messenger PDAs instead of full entity query
        foreach (var (pdaUid, (cartridgeUid, _)) in _messengerPdas)
        {
            if (!TryComp<STMessengerServerComponent>(cartridgeUid, out var server))
                continue;

            // Re-resolve the holder's band before access check (band may have changed)
            if (TryComp<TransformComponent>(pdaUid, out var pdaXform))
            {
                var holder = pdaXform.ParentUid;
                if (holder.IsValid())
                    server.OwnerBand = ResolveMobBand(holder);
            }

            if (!HasChannelAccess(channelProto, server))
                continue;

            if (server.MutedChannels.Contains(channelProto.ID))
                continue;

            if (TryComp<RingerComponent>(pdaUid, out var ringer))
                _ringer.RingerPlayRingtone((pdaUid, ringer));
        }
    }

    #endregion

    #region Contacts

    private void OnAddContact(
        Entity<STMessengerComponent> ent,
        STMessengerServerComponent server,
        STMessengerAddContactEvent add,
        CartridgeMessageEvent args)
    {
        if (string.IsNullOrWhiteSpace(add.MessengerId))
            return;

        if (server.NextInteractionTime > _timing.CurTime)
            return;

        server.NextInteractionTime = _timing.CurTime + InteractionCooldown;

        if (!_messengerIdCache.TryGetValue(add.MessengerId, out var contactIdentity))
            return;

        // Can't add yourself
        if (contactIdentity.UserId == server.OwnerUserId
            && contactIdentity.CharName == server.OwnerCharacterName)
            return;

        if (server.Contacts.Count >= MaxContacts)
            return;

        // Already a contact (keyed by messenger ID)
        if (server.Contacts.ContainsKey(add.MessengerId))
            return;

        var factionName = ResolveContactFaction(contactIdentity);
        server.Contacts[add.MessengerId] = new STContactEntry(
            contactIdentity.UserId, contactIdentity.CharName, factionName);

        AddContactAsync(server.OwnerUserId, server.OwnerCharacterName,
            contactIdentity.UserId, contactIdentity.CharName, factionName);

        _adminLogger.Add(LogType.STMessenger, LogImpact.Low,
            $"{ToPrettyString(args.Actor):player} added messenger contact " +
            $"{contactIdentity.CharName} (ID: {add.MessengerId})");

        BroadcastUiUpdate();
    }

    private void OnRemoveContact(
        Entity<STMessengerComponent> ent,
        STMessengerServerComponent server,
        STMessengerRemoveContactEvent remove,
        CartridgeMessageEvent args)
    {
        if (server.NextInteractionTime > _timing.CurTime)
            return;

        server.NextInteractionTime = _timing.CurTime + InteractionCooldown;

        if (!server.Contacts.TryGetValue(remove.ContactMessengerId, out var contactEntry))
            return;

        server.Contacts.Remove(remove.ContactMessengerId);

        _adminLogger.Add(LogType.STMessenger, LogImpact.Low,
            $"{ToPrettyString(args.Actor):player} removed messenger contact " +
            $"{contactEntry.CharacterName} (ID: {remove.ContactMessengerId})");

        RemoveContactAsync(server.OwnerUserId, server.OwnerCharacterName,
            contactEntry.UserId, contactEntry.CharacterName);

        var loaderUid = GetEntity(args.LoaderUid);
        UpdateUiState(ent, loaderUid, server);
    }

    #endregion

    #region Mute / Mark Read / View Chat

    private void OnToggleMute(
        Entity<STMessengerComponent> ent,
        STMessengerServerComponent server,
        STMessengerToggleMuteEvent mute,
        CartridgeMessageEvent args)
    {
        if (!server.MutedChannels.Add(mute.ChannelId))
            server.MutedChannels.Remove(mute.ChannelId);

        var loaderUid = GetEntity(args.LoaderUid);
        UpdateUiState(ent, loaderUid, server);
    }

    private void OnMarkRead(STMessengerServerComponent server, STMessengerMarkReadEvent markRead)
    {
        server.LastSeenMessageId[markRead.ChatId] = markRead.LastSeenMessageId;
    }

    private void OnViewChat(NetEntity loaderNetUid, STMessengerViewChatEvent viewChat)
    {
        var loaderUid = GetEntity(loaderNetUid);
        _viewedChat[loaderUid] = viewChat.ChatId;

        if (_cartridgeLoader.TryGetProgram<STMessengerComponent>(loaderUid, out var progUid, out _)
            && TryComp<STMessengerServerComponent>(progUid.Value, out var server))
        {
            if (viewChat.ChatId is not null)
                MarkChatAsRead(viewChat.ChatId, server);

            UpdateUiState((progUid.Value, Comp<STMessengerComponent>(progUid.Value)), loaderUid, server);
        }
    }

    private void OnNavigateToOffer(NetEntity loaderNetUid, STMessengerNavigateToOfferEvent navigateToOffer)
    {
        var loaderUid = GetEntity(loaderNetUid);

        // Raise the event on all bulletin board cartridges on this PDA.
        // Each board checks if it owns the offer via the global index and only the correct one activates.
        // If the offer was withdrawn (not in index), the first board activates with search pre-fill as fallback.
        var ev = new STOpenBulletinOfferEvent(loaderUid, navigateToOffer.OfferId);
        var installed = _cartridgeLoader.GetInstalled(loaderUid);
        foreach (var progUid in installed)
        {
            if (!HasComp<STBulletinBoardComponent>(progUid))
                continue;

            RaiseLocalEvent(progUid, ref ev);
            if (ev.Handled)
                return;
        }
    }

    private void OnNavigateToNews(NetEntity loaderNetUid, STMessengerNavigateToNewsEvent navigateToNews)
    {
        var loaderUid = GetEntity(loaderNetUid);

        var ev = new STOpenNewsArticleEvent(loaderUid, navigateToNews.ArticleId);
        var installed = _cartridgeLoader.GetInstalled(loaderUid);
        foreach (var progUid in installed)
        {
            if (!HasComp<STNewsCartridgeComponent>(progUid))
                continue;

            RaiseLocalEvent(progUid, ref ev);
            if (ev.Handled)
                return;
        }
    }

    private void MarkChatAsRead(string chatId, STMessengerServerComponent server)
    {
        var isDm = chatId.StartsWith(STMessengerChat.DmChatPrefix, StringComparison.Ordinal);
        List<STMessengerMessage>? messages = null;

        if (isDm)
        {
            var contactMessengerId = chatId[STMessengerChat.DmChatPrefix.Length..];
            var dmKey = NormalizeDmKey(server.MessengerId, contactMessengerId);
            _dmChats.TryGetValue(dmKey, out messages);
        }
        else
        {
            _channelChats.TryGetValue(chatId, out messages);
        }

        if (messages is { Count: > 0 })
            server.LastSeenMessageId[chatId] = messages[^1].Id;
    }

    #endregion

    #region UI State

    private void UpdateUiState(Entity<STMessengerComponent> ent, EntityUid loaderUid, STMessengerServerComponent server)
    {
        // Consume one-shot deep-link from external systems (e.g. merc board Contact)
        string? navigateTo = server.PendingNavigateToChatId;
        server.PendingNavigateToChatId = null;

        string? draftMessage = server.PendingDraftMessage;
        server.PendingDraftMessage = null;

        var state = BuildUiState(loaderUid, server, navigateToChatId: navigateTo, draftMessage: draftMessage);
        _cartridgeLoader.UpdateCartridgeUiState(loaderUid, state);
    }

    private STMessengerUiState BuildUiState(
        EntityUid loaderUid,
        STMessengerServerComponent server,
        string? navigateToChatId = null,
        string? draftMessage = null)
    {
        _viewedChat.TryGetValue(loaderUid, out var viewedChatId);

        // Use pre-sorted channel cache to avoid per-call prototype lookups and sorting
        var channels = new List<STMessengerChat>(_sortedChannels.Count);
        foreach (var proto in _sortedChannels)
        {
            if (!HasChannelAccess(proto, server))
                continue;

            List<STMessengerMessage>? messages = null;
            if (viewedChatId == proto.ID && _channelChats.TryGetValue(proto.ID, out var channelMessages))
                messages = new List<STMessengerMessage>(channelMessages);

            var unread = CountUnread(proto.ID, channelMessages: _channelChats.GetValueOrDefault(proto.ID), server);
            var isMuted = server.MutedChannels.Contains(proto.ID);

            channels.Add(new STMessengerChat(
                proto.ID,
                Loc.GetString(proto.Name),
                isDirect: false,
                unread,
                isMuted,
                messages));
        }

        var directMessages = new List<STMessengerChat>(server.Contacts.Count);
        foreach (var (contactMessengerId, contactEntry) in server.Contacts)
        {
            var dmKey = NormalizeDmKey(server.MessengerId, contactMessengerId);
            var dmChatId = STMessengerChat.DmChatPrefix + contactMessengerId;

            List<STMessengerMessage>? messages = null;
            if (viewedChatId == dmChatId && _dmChats.TryGetValue(dmKey, out var dmMessages))
                messages = new List<STMessengerMessage>(dmMessages);

            var unread = CountUnread(dmChatId, channelMessages: _dmChats.GetValueOrDefault(dmKey), server);

            directMessages.Add(new STMessengerChat(
                dmChatId,
                contactEntry.CharacterName,
                isDirect: true,
                unread,
                isMuted: false,
                messages));
        }

        var contactInfos = new List<STMessengerContactInfo>();
        foreach (var (contactMessengerId, contactEntry) in server.Contacts)
        {
            // Fresh-resolve faction for online contacts; fall back to cached for offline
            var contactKey = (contactEntry.UserId, contactEntry.CharacterName);
            var currentFaction = ResolveContactFaction(contactKey);
            if (currentFaction is not null && currentFaction != contactEntry.FactionName)
            {
                contactEntry.FactionName = currentFaction;
                UpdateContactFactionAsync(server.OwnerUserId, server.OwnerCharacterName,
                    contactEntry.UserId, contactEntry.CharacterName, currentFaction);
            }

            var rankIcon = ResolveContactRankIcon(contactKey);

            contactInfos.Add(new STMessengerContactInfo(
                contactEntry.CharacterName,
                contactMessengerId,
                contactEntry.FactionName,
                rankIcon));
        }

        return new STMessengerUiState(
            server.MessengerId,
            channels,
            directMessages,
            contactInfos,
            navigateToChatId,
            draftMessage);
    }

    private int CountUnread(string chatId, List<STMessengerMessage>? channelMessages, STMessengerServerComponent server)
    {
        if (channelMessages is null || channelMessages.Count == 0)
            return 0;

        if (!server.LastSeenMessageId.TryGetValue(chatId, out var lastSeen))
            return channelMessages.Count;

        // Iterate from end — messages are ordered by ID, so we can stop early
        var count = 0;
        for (var i = channelMessages.Count - 1; i >= 0; i--)
        {
            if (channelMessages[i].Id <= lastSeen)
                break;

            count++;
        }

        return count;
    }

    /// <summary>
    /// Broadcasts UI updates to active loaders. When <paramref name="changedChatId"/> is set,
    /// only loaders viewing the main page or the relevant chat are updated.
    /// </summary>
    private void BroadcastUiUpdate(string? changedChatId = null)
    {
        foreach (var loaderUid in _activeLoaders)
        {
            if (!TryComp<CartridgeLoaderComponent>(loaderUid, out _))
                continue;

            if (!_cartridgeLoader.TryGetProgram<STMessengerComponent>(
                    loaderUid, out var progUid, out var messengerComp))
                continue;

            if (!TryComp<STMessengerServerComponent>(progUid.Value, out var server))
                continue;

            // Skip viewers looking at a different chat — they'll update when they navigate
            if (changedChatId is not null
                && _viewedChat.TryGetValue(loaderUid, out var viewedChat)
                && viewedChat != changedChatId)
                continue;

            UpdateUiState((progUid.Value, messengerComp), loaderUid, server);
        }
    }

    #endregion

    #region Player Spawn & Data Loading

    /// <summary>
    /// Handles PDA being equipped in the ID slot — reloads messenger data from DB
    /// when a fresh PDA is equipped (e.g. after personal stash store/retrieve).
    /// </summary>
    private void OnPdaEquipped(Entity<PdaComponent> ent, ref GotEquippedEvent args)
    {
        if (!args.SlotFlags.HasFlag(SlotFlags.IDCARD))
            return;

        TryInitializeMessenger(ent.Owner, args.Equipee);
    }

    /// <summary>
    /// Handles PDA being picked up into a hand — initializes the messenger for
    /// admin-spawned PDAs that bypass the normal equip/spawn flow.
    /// </summary>
    private void OnPdaPickedUp(Entity<PdaComponent> ent, ref GotEquippedHandEvent args)
    {
        TryInitializeMessenger(ent.Owner, args.User);
    }

    /// <summary>
    /// Attempts to initialize the messenger on a PDA for the given holder entity.
    /// No-ops if the messenger is already claimed or the holder is not player-controlled.
    /// </summary>
    private void TryInitializeMessenger(EntityUid pdaUid, EntityUid holder)
    {
        if (!_cartridgeLoader.TryGetProgram<STMessengerComponent>(pdaUid, out var progUid, out _))
            return;

        if (!TryComp<STMessengerServerComponent>(progUid.Value, out var server))
            return;

        // Already claimed — don't overwrite (e.g. looted PDA with someone else's data)
        if (!string.IsNullOrEmpty(server.OwnerCharacterName))
        {
            // If the rightful owner is re-equipping their own PDA (e.g. after death/respawn),
            // update the cache so faction/rank resolution points to the correct (held) PDA.
            if (TryComp<ActorComponent>(holder, out var ownerActor))
            {
                var holderId = ownerActor.PlayerSession.UserId.UserId;
                var holderName = MetaData(holder).EntityName;

                if (holderId == server.OwnerUserId
                    && holderName == server.OwnerCharacterName)
                {
                    _characterToPda[(holderId, holderName)] = pdaUid;
                    _messengerPdas[pdaUid] = (progUid.Value, pdaUid);
                    server.OwnerBand = ResolveMobBand(holder);

                }
            }

            return;
        }

        // Only initialize for player-controlled entities
        if (!TryComp<ActorComponent>(holder, out var actor))
            return;

        var userId = actor.PlayerSession.UserId.UserId;
        var charName = MetaData(holder).EntityName;
        InitializeMessengerForPda(pdaUid, progUid.Value, server, userId, charName, holder);

        // Claim PDA ownership so the password settings UI works for wild PDAs
        if (TryComp<PdaComponent>(pdaUid, out var pda) && pda.PdaOwner is null)
            _pda.SetOwner(pdaUid, pda, holder, charName);
    }

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent args)
    {
        if (!_inventory.TryGetSlotEntity(args.Mob, "id", out var idEntity))
            return;

        if (!TryComp<PdaComponent>(idEntity, out var pdaComp))
            return;

        if (!_cartridgeLoader.TryGetProgram<STMessengerComponent>(idEntity.Value, out var progUid, out _))
            return;

        if (!TryComp<STMessengerServerComponent>(progUid.Value, out var server))
            return;

        // Guard: OwnerCharacterName is set synchronously by InitializeMessengerForPda,
        // so if GotEquippedEvent already fired (e.g. during loadout equip), skip to avoid double-loading.
        if (!string.IsNullOrEmpty(server.OwnerCharacterName))
            return;

        var userId = args.Player.UserId.UserId;
        var charName = args.Profile.Name;
        InitializeMessengerForPda(idEntity.Value, progUid.Value, server, userId, charName, args.Mob);
    }

    /// <summary>
    /// Shared logic for initializing a messenger PDA for a character.
    /// Updates caches synchronously, then starts async DB loads for messenger ID and contacts.
    /// Called from both <see cref="OnPlayerSpawned"/> and <see cref="OnPdaEquipped"/>.
    /// </summary>
    private void InitializeMessengerForPda(
        EntityUid pdaUid,
        EntityUid cartridgeUid,
        STMessengerServerComponent server,
        Guid userId,
        string charName,
        EntityUid holderUid)
    {
        server.OwnerUserId = userId;
        server.OwnerCharacterName = charName;
        server.OwnerBand = ResolveMobBand(holderUid);

        _characterToPda[(userId, charName)] = pdaUid;
        _messengerPdas[pdaUid] = (cartridgeUid, pdaUid);

        LoadOrGenerateMessengerIdAsync(cartridgeUid, userId, charName);
        LoadContactsAsync(cartridgeUid, userId, charName);

        // BroadcastUiUpdate is called from LoadContactsAsync after DB loads complete
    }

    #endregion

    #region Round Lifecycle

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        _activeLoaders.Clear();
        _viewedChat.Clear();
        _channelChats.Clear();
        _dmChats.Clear();
        _nextMessageId.Clear();
        _characterToPda.Clear();
        _messengerPdas.Clear();
        _anonymousPseudonyms.Clear();
        _usedPseudonyms.Clear();
        // Do NOT clear _messengerIdCache or _characterToMessengerId — IDs persist across rounds

        foreach (var proto in _sortedChannels)
        {
            _channelChats.TryAdd(proto.ID, new List<STMessengerMessage>());
        }
    }

    #endregion

    #region Discord Webhook

    private void SendDiscordWebhook(string channelId, string senderName, string content)
    {
        if (_webhookIdentifier is not { } identifier)
            return;

        var channelName = channelId;
        if (_protoManager.TryIndex<STMessengerChannelPrototype>(channelId, out var proto))
            channelName = Loc.GetString(proto.Name);

        var payload = new WebhookPayload
        {
            Username = senderName,
            Content = $"**[{channelName}] {senderName}**\n`{content}`",
        };

        _discord.CreateMessage(identifier, payload);
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Resolves the current rank icon of an online contact by looking up their session's attached entity.
    /// Returns null if the contact is offline or has no rank.
    /// </summary>
    private string? ResolveContactRankIcon((Guid UserId, string CharName) contactKey)
    {
        if (!_playerManager.TryGetSessionById(new NetUserId(contactKey.UserId), out var session))
            return null;

        if (session.AttachedEntity is not { } mob)
            return null;

        if (!TryComp<STCharacterRankComponent>(mob, out var rank))
            return null;

        return rank.RankIconId;
    }

    /// <summary>
    /// Resolves the current faction of an online contact by looking up their session's attached entity.
    /// Returns null if the contact is offline or has no faction.
    /// </summary>
    private string? ResolveContactFaction((Guid UserId, string CharName) contactKey)
    {
        if (!_playerManager.TryGetSessionById(new NetUserId(contactKey.UserId), out var session))
            return null;

        if (session.AttachedEntity is not { } mob)
            return null;

        if (!TryComp<BandsComponent>(mob, out var bands))
            return null;

        if (MetaData(mob).EntityName != contactKey.CharName)
            return null;

        // Only Clear Sky is disguised as Loners on PDA
        if (bands.BandProto == ClearSkyBandId)
            return _factionResolution.GetBandFactionName(bands.BandName);

        if (bands.BandProto is not { } bandProtoId)
            return null;

        if (!_protoManager.TryIndex(bandProtoId, out var bandProto))
            return null;

        return _factionResolution.GetBandFactionName(bandProto.Name);
    }

    private void CacheSortedChannels()
    {
        _sortedChannels = new List<STMessengerChannelPrototype>(
            _protoManager.EnumeratePrototypes<STMessengerChannelPrototype>());
        _sortedChannels.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.ByType.ContainsKey(typeof(STMessengerChannelPrototype)))
            CacheSortedChannels();
    }

    /// <summary>
    /// Returns a stable anonymous pseudonym for the given character identity.
    /// The same character always gets the same pseudonym within a round.
    /// Pseudonyms are cleared on round restart.
    /// </summary>
    private string GetOrCreatePseudonym((Guid UserId, string CharName) identity)
    {
        if (_anonymousPseudonyms.TryGetValue(identity, out var existing))
            return existing;

        for (var attempt = 0; attempt < MaxRetryCollision; attempt++)
        {
            var suffix = _random.Next(1, MaxPseudonymSuffix + 1);
            var pseudonym = $"{AnonymousName}-{suffix}";

            if (_usedPseudonyms.Contains(pseudonym))
                continue;

            _usedPseudonyms.Add(pseudonym);
            _anonymousPseudonyms[identity] = pseudonym;
            return pseudonym;
        }

        // Fallback: use charName hash; bitwise AND avoids OverflowException on int.MinValue
        var hashSuffix = (identity.CharName.GetHashCode() & 0x7FFFFFFF) % (MaxPseudonymSuffix + 1);
        var fallback = $"{AnonymousName}-{hashSuffix}";

        while (_usedPseudonyms.Contains(fallback))
            fallback += "X";

        _usedPseudonyms.Add(fallback);
        _anonymousPseudonyms[identity] = fallback;
        return fallback;
    }

    /// <summary>
    /// Resolves the band prototype ID from a mob entity's BandsComponent.
    /// Returns null if the entity has no band.
    /// </summary>
    private ProtoId<STBandPrototype>? ResolveMobBand(EntityUid mobUid)
    {
        return TryComp<BandsComponent>(mobUid, out var bands) ? bands.BandProto : null;
    }

    /// <summary>
    /// Returns true if the server component has access to the given channel.
    /// Unrestricted channels (empty RequiredBands) are accessible to all.
    /// </summary>
    private static bool HasChannelAccess(STMessengerChannelPrototype channel, STMessengerServerComponent server)
    {
        return channel.RequiredBands.Count == 0
            || (server.OwnerBand is not null && channel.RequiredBands.Contains(server.OwnerBand.Value));
    }

    /// <summary>
    /// Normalize DM key to ensure both directions map to the same storage.
    /// Uses alphabetical ordering of messenger IDs.
    /// </summary>
    private static string NormalizeDmKey(string messengerIdA, string messengerIdB)
    {
        return string.Compare(messengerIdA, messengerIdB, StringComparison.Ordinal) < 0
            ? string.Concat(messengerIdA, ":", messengerIdB)
            : string.Concat(messengerIdB, ":", messengerIdA);
    }

    /// <summary>
    /// Generate a random unique messenger ID in "XXX-XXX" format.
    /// </summary>
    private static string GenerateMessengerId(IRobustRandom random)
    {
        var part1 = random.Next(100, 1000);
        var part2 = random.Next(100, 1000);
        return $"{part1}-{part2}";
    }

    #endregion

    #region Public API (for other cartridge systems)

    /// <summary>
    /// Looks up a character's messenger ID by their character name.
    /// Returns null if no messenger ID is cached for the given identity.
    /// </summary>
    public string? GetMessengerId(Guid userId, string characterName)
    {
        return _characterToMessengerId.GetValueOrDefault((userId, characterName));
    }

    /// <summary>
    /// Activates the messenger cartridge on a PDA, adds a contact by messenger ID,
    /// and navigates to their DM conversation. Optionally pre-fills a draft message.
    /// Called by external cartridge systems (e.g. bulletin board Contact button).
    /// </summary>
    public void OpenDm(
        EntityUid loaderUid,
        EntityUid messengerCartridgeUid,
        string contactMessengerId,
        string? draftMessage = null)
    {
        TryAddContact(messengerCartridgeUid, contactMessengerId);

        if (TryComp<STMessengerServerComponent>(messengerCartridgeUid, out var server))
        {
            var dmChatId = STMessengerChat.DmChatPrefix + contactMessengerId;
            _viewedChat[loaderUid] = dmChatId;
            MarkChatAsRead(dmChatId, server);
            server.PendingNavigateToChatId = dmChatId;
            server.PendingDraftMessage = draftMessage;
        }

        // Only one SetUiState per tick — client swaps to messenger UI,
        // sends CartridgeUiReadyEvent, then OnUiReady delivers messenger state.
        _cartridgeLoader.ActivateProgram(loaderUid, messengerCartridgeUid);
    }

    /// <summary>
    /// Adds a contact to a messenger cartridge without triggering a full UI broadcast.
    /// Used by other cartridge systems (e.g. merc board) to programmatically add contacts.
    /// Returns false if the contact already exists, the target has no messenger, or the limit is reached.
    /// </summary>
    public bool TryAddContact(EntityUid cartridgeUid, string contactMessengerId)
    {
        if (!TryComp<STMessengerServerComponent>(cartridgeUid, out var server))
            return false;

        if (string.IsNullOrEmpty(server.OwnerCharacterName))
            return false;

        if (!_messengerIdCache.TryGetValue(contactMessengerId, out var contactIdentity))
            return false;

        // Can't add yourself
        if (contactIdentity.UserId == server.OwnerUserId
            && contactIdentity.CharName == server.OwnerCharacterName)
            return false;

        if (server.Contacts.Count >= MaxContacts)
            return false;

        // Already a contact
        if (server.Contacts.ContainsKey(contactMessengerId))
            return false;

        var factionName = ResolveContactFaction(contactIdentity);
        server.Contacts[contactMessengerId] = new STContactEntry(
            contactIdentity.UserId, contactIdentity.CharName, factionName);

        AddContactAsync(server.OwnerUserId, server.OwnerCharacterName,
            contactIdentity.UserId, contactIdentity.CharName, factionName);

        return true;
    }

    #endregion
}
