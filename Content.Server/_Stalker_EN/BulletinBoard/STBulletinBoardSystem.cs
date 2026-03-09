using Content.Server._Stalker_EN.PdaMessenger;
using Content.Server.Administration.Logs;
using Content.Server.CartridgeLoader;
using Content.Shared._Stalker.Bands;
using Content.Shared._Stalker_EN.BulletinBoard;
using Content.Shared._Stalker_EN.FactionRelations;
using Content.Shared._Stalker_EN.PdaMessenger;
using Content.Shared.CartridgeLoader;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.Inventory;
using Content.Shared.PDA;
using Content.Shared.PDA.Ringer;
using Content.Server.PDA.Ringer;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Stalker_EN.BulletinBoard;

/// <summary>
/// Server system for generic bulletin board PDA cartridges.
/// Manages offers across multiple board types, handling posting, withdrawal,
/// contact integration, and UI state broadcasting.
/// </summary>
public sealed class STBulletinBoardSystem : EntitySystem
{
    [Dependency] private readonly CartridgeLoaderSystem _cartridgeLoader = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly RingerSystem _ringer = default!;
    [Dependency] private readonly SharedSTFactionResolutionSystem _factionResolution = default!;
    [Dependency] private readonly STMessengerSystem _messenger = default!;

    private static readonly ProtoId<STBandPrototype> ClearSkyBandId = "STClearSkyBand";

    /// <summary>
    /// Offers grouped by board type ID, then keyed by offer ID.
    /// </summary>
    private readonly Dictionary<string, Dictionary<uint, STBulletinOffer>> _offersByBoardType = new();

    /// <summary>
    /// Global index for O(1) offer lookup by ID (across all board types).
    /// </summary>
    private readonly Dictionary<uint, string> _globalOfferIndex = new();

    /// <summary>
    /// Auto-increment offer ID counter (global across all board types).
    /// </summary>
    private uint _nextOfferId;

    /// <summary>
    /// PDAs with bulletin board cartridges currently active (UI open). Receive broadcast updates.
    /// </summary>
    private readonly HashSet<EntityUid> _activeLoaders = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STBulletinBoardComponent, CartridgeUiReadyEvent>(OnUiReady);
        SubscribeLocalEvent<STBulletinBoardComponent, CartridgeActivatedEvent>(OnCartridgeActivated);
        SubscribeLocalEvent<STBulletinBoardComponent, CartridgeDeactivatedEvent>(OnCartridgeDeactivated);
        SubscribeLocalEvent<STBulletinBoardComponent, CartridgeMessageEvent>(OnMessage);
        SubscribeLocalEvent<STBulletinBoardComponent, STOpenBulletinOfferEvent>(OnOpenOffer);
        SubscribeLocalEvent<CartridgeLoaderComponent, EntityTerminatingEvent>(OnLoaderTerminating);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
    }

    #region Cartridge Events

    private void OnUiReady(Entity<STBulletinBoardComponent> ent, ref CartridgeUiReadyEvent args)
    {
        if (!TryComp<STBulletinServerComponent>(ent, out var server))
            return;

        // Clear notification badge when the board is viewed
        if (TryComp<CartridgeComponent>(ent, out var cartComp) && cartComp.HasNotification)
        {
            cartComp.HasNotification = false;
            Dirty(ent, cartComp);
        }

        // Lazy init: if the board wasn't initialized at spawn (e.g. PDA re-equipped from stash),
        // resolve owner from the PDA holder now.
        if (string.IsNullOrEmpty(server.OwnerCharacterName))
            TryLazyInit(args.Loader, ent, server);

        UpdateUiState(ent, args.Loader, server);
    }

    private void OnCartridgeActivated(Entity<STBulletinBoardComponent> ent, ref CartridgeActivatedEvent args)
    {
        _activeLoaders.Add(args.Loader);
    }

    private void OnCartridgeDeactivated(Entity<STBulletinBoardComponent> ent, ref CartridgeDeactivatedEvent args)
    {
        // Engine bug workaround: DeactivateProgram passes programUid as args.Loader instead of loaderUid.
        // Resolve the real loader from CartridgeComponent to ensure we remove the correct entry.
        if (TryComp<CartridgeComponent>(ent, out var cartridge) && cartridge.LoaderUid is { } loaderUid)
            _activeLoaders.Remove(loaderUid);
        else
            _activeLoaders.Remove(args.Loader);
    }

    private void OnLoaderTerminating(Entity<CartridgeLoaderComponent> ent, ref EntityTerminatingEvent args)
    {
        _activeLoaders.Remove(ent);
    }

    private void OnOpenOffer(Entity<STBulletinBoardComponent> ent, ref STOpenBulletinOfferEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<STBulletinServerComponent>(ent, out var server))
            return;

        // Only activate if this board type owns the offer (or offer doesn't exist anymore)
        if (_globalOfferIndex.TryGetValue(args.OfferId, out var ownerBoardType)
            && ownerBoardType != ent.Comp.BoardTypeId)
        {
            return;
        }

        args.Handled = true;
        server.PendingSearchQuery = $"#{args.OfferId}";

        // Look up the offer's category so the client switches to the correct tab
        if (_offersByBoardType.TryGetValue(ent.Comp.BoardTypeId, out var offerStorage)
            && offerStorage.TryGetValue(args.OfferId, out var linkedOffer))
        {
            server.PendingCategory = linkedOffer.Category;
        }

        _cartridgeLoader.ActivateProgram(args.LoaderUid, ent);
    }

    private void OnMessage(Entity<STBulletinBoardComponent> ent, ref CartridgeMessageEvent args)
    {
        if (!TryComp<STBulletinServerComponent>(ent, out var server))
            return;

        switch (args)
        {
            case STBulletinPostOfferEvent post:
                OnPostOffer(ent, server, post, args);
                break;
            case STBulletinWithdrawOfferEvent withdraw:
                OnWithdrawOffer(ent, server, withdraw, args);
                break;
            case STBulletinContactPosterEvent contact:
                OnContactPoster(ent, server, contact, args);
                break;
            case STBulletinToggleMuteEvent:
                OnToggleMute(ent, server, args);
                break;
        }
    }

    #endregion

    #region Post / Withdraw / Contact

    private void OnPostOffer(
        Entity<STBulletinBoardComponent> ent,
        STBulletinServerComponent server,
        STBulletinPostOfferEvent post,
        CartridgeMessageEvent args)
    {
        if (string.IsNullOrEmpty(server.OwnerCharacterName))
            return;

        var board = ent.Comp;

        // Merc board restriction: only band members can post primary
        if (post.Category == STBulletinCategory.Primary
            && TryComp<STMercBoardRestrictionsComponent>(ent, out var restrictions))
        {
            if (!IsBandMember(args.Actor, restrictions.RequiredBandForPrimary))
                return;
        }

        var description = post.Description.Trim();

        if (string.IsNullOrEmpty(description))
            return;

        if (description.Length > board.MaxDescriptionLength)
            description = description[..board.MaxDescriptionLength];

        var storage = GetOrCreateStorage(board.BoardTypeId);
        var playerCount = CountPlayerOffers(storage, server.OwnerCharacterName, post.Category);
        if (playerCount >= board.MaxOffersPerPlayer)
            return;

        var totalCount = storage.Count;
        if (totalCount >= board.MaxTotalOffers)
            return;

        var posterFaction = ResolveFaction(args.Actor);
        var posterMessengerId = _messenger.GetMessengerId(server.OwnerUserId, server.OwnerCharacterName);

        var offer = new STBulletinOffer(
            ++_nextOfferId,
            post.Category,
            board.OfferRefPrefix,
            server.OwnerCharacterName,
            posterMessengerId,
            posterFaction,
            description,
            _timing.CurTime);

        storage[offer.Id] = offer;
        _globalOfferIndex[offer.Id] = board.BoardTypeId;

        _adminLogger.Add(LogType.STBulletinBoard, LogImpact.Low,
            $"{ToPrettyString(args.Actor):player} posted bulletin board [{board.BoardTypeId}] {post.Category}: " +
            $"desc=\"{description}\"");

        BroadcastUiUpdate(board.BoardTypeId);
        NotifyBoardRecipients(board.BoardTypeId, args.Actor, post.Category);
    }

    private void OnWithdrawOffer(
        Entity<STBulletinBoardComponent> ent,
        STBulletinServerComponent server,
        STBulletinWithdrawOfferEvent withdraw,
        CartridgeMessageEvent args)
    {
        if (string.IsNullOrEmpty(server.OwnerCharacterName))
            return;

        var boardTypeId = ent.Comp.BoardTypeId;
        if (!_offersByBoardType.TryGetValue(boardTypeId, out var storage))
            return;

        if (!storage.TryGetValue(withdraw.OfferId, out var offer))
            return;

        if (offer.PosterName != server.OwnerCharacterName)
            return;

        storage.Remove(withdraw.OfferId);
        _globalOfferIndex.Remove(withdraw.OfferId);

        _adminLogger.Add(LogType.STBulletinBoard, LogImpact.Low,
            $"{ToPrettyString(args.Actor):player} withdrew bulletin board [{boardTypeId}] offer #{withdraw.OfferId}");

        BroadcastUiUpdate(boardTypeId);
    }

    private void OnContactPoster(
        Entity<STBulletinBoardComponent> ent,
        STBulletinServerComponent server,
        STBulletinContactPosterEvent contact,
        CartridgeMessageEvent args)
    {
        if (string.IsNullOrEmpty(server.OwnerCharacterName))
            return;

        if (string.IsNullOrEmpty(contact.PosterMessengerId))
            return;

        if (server.NextContactTime > _timing.CurTime)
            return;
        server.NextContactTime = _timing.CurTime + ent.Comp.ContactCooldown;

        var loaderUid = GetEntity(args.LoaderUid);
        if (!_cartridgeLoader.TryGetProgram<STMessengerComponent>(loaderUid, out var messengerUid, out _))
            return;

        // Look up the offer to get its prefix for the draft message
        string draftPrefix = ent.Comp.OfferRefPrefix;
        if (_offersByBoardType.TryGetValue(ent.Comp.BoardTypeId, out var storage)
            && storage.TryGetValue(contact.OfferId, out var offer))
        {
            draftPrefix = offer.OfferRefPrefix;
        }

        var draftMessage = STBulletinOffer.FormatRef(draftPrefix, contact.OfferId);
        _messenger.OpenDm(loaderUid, messengerUid.Value, contact.PosterMessengerId, draftMessage);

        _adminLogger.Add(LogType.STBulletinBoard, LogImpact.Low,
            $"{ToPrettyString(args.Actor):player} opened DM from bulletin board with: " +
            $"{contact.PosterMessengerId} (offer #{contact.OfferId})");
    }

    private void OnToggleMute(
        Entity<STBulletinBoardComponent> ent,
        STBulletinServerComponent server,
        CartridgeMessageEvent args)
    {
        server.Muted = !server.Muted;
        UpdateUiState(ent, GetEntity(args.LoaderUid), server);
    }

    #endregion

    #region UI State

    private void UpdateUiState(
        Entity<STBulletinBoardComponent> ent,
        EntityUid loaderUid,
        STBulletinServerComponent server)
    {
        // Consume one-shot search pre-fill and category tab switch
        var searchQuery = server.PendingSearchQuery;
        server.PendingSearchQuery = null;

        var activeCategory = server.PendingCategory;
        server.PendingCategory = null;

        var state = BuildUiState(ent, server, searchQuery, activeCategory);
        _cartridgeLoader.UpdateCartridgeUiState(loaderUid, state);
    }

    private STBulletinUiState BuildUiState(
        Entity<STBulletinBoardComponent> ent,
        STBulletinServerComponent server,
        string? searchQuery = null,
        STBulletinCategory? activeCategory = null)
    {
        var board = ent.Comp;
        var storage = GetOrCreateStorage(board.BoardTypeId);

        var hasMercRestrictions = TryComp<STMercBoardRestrictionsComponent>(ent, out var restrictions);
        var isBandMember = hasMercRestrictions && IsBandMemberCached(server, restrictions!);

        var primaryOffers = new List<STBulletinOffer>();
        var secondaryOffers = new List<STBulletinOffer>();
        var myPrimaryCount = 0;
        var mySecondaryCount = 0;

        foreach (var offer in storage.Values)
        {
            if (offer.Category == STBulletinCategory.Primary)
            {
                primaryOffers.Add(offer);
                if (offer.PosterName == server.OwnerCharacterName)
                    myPrimaryCount++;
            }
            else
            {
                // Merc restriction: non-members only see their own secondary offers
                if (hasMercRestrictions && !isBandMember && offer.PosterName != server.OwnerCharacterName)
                    continue;

                secondaryOffers.Add(offer);
                if (offer.PosterName == server.OwnerCharacterName)
                    mySecondaryCount++;
            }
        }

        var config = BuildConfig(ent, hasMercRestrictions, isBandMember);

        return new STBulletinUiState(
            config,
            primaryOffers,
            secondaryOffers,
            server.OwnerCharacterName,
            myPrimaryCount,
            mySecondaryCount,
            server.Muted,
            searchQuery,
            activeCategory);
    }

    private STBulletinBoardConfig BuildConfig(
        Entity<STBulletinBoardComponent> ent,
        bool hasMercRestrictions,
        bool isBandMember)
    {
        var board = ent.Comp;

        // Permission flags
        bool canPostPrimary;
        bool canPostSecondary = true;
        bool showSecondaryCountBadge;

        if (hasMercRestrictions)
        {
            canPostPrimary = isBandMember;
            showSecondaryCountBadge = isBandMember;
        }
        else
        {
            canPostPrimary = true;
            showSecondaryCountBadge = true;
        }

        return new STBulletinBoardConfig(
            board.HeaderTitle,
            board.PrimaryTabName,
            board.SecondaryTabName,
            board.PrimaryPostButton,
            board.SecondaryPostButton,
            board.PrimaryInfoLabel,
            board.SecondaryInfoLabel,
            board.SecondaryHint,
            board.NewPrimaryTitle,
            board.NewSecondaryTitle,
            board.SearchPlaceholder,
            board.OfferRefPrefix,
            board.PrimaryBorderColor,
            board.PrimaryOwnBorderColor,
            board.SecondaryBorderColor,
            board.SecondaryOwnBorderColor,
            canPostPrimary,
            canPostSecondary,
            showSecondaryCountBadge,
            board.MaxDescriptionLength,
            board.MaxOffersPerPlayer);
    }

    private void BroadcastUiUpdate(string boardTypeId)
    {
        foreach (var loaderUid in _activeLoaders)
        {
            if (!HasComp<CartridgeLoaderComponent>(loaderUid))
                continue;

            // Iterate all installed programs to find the one matching boardTypeId.
            // TryGetProgram only returns the first match, which breaks when multiple
            // bulletin board cartridges are installed on the same PDA.
            var installed = _cartridgeLoader.GetInstalled(loaderUid);
            foreach (var progUid in installed)
            {
                if (!TryComp<STBulletinBoardComponent>(progUid, out var board))
                    continue;

                if (board.BoardTypeId != boardTypeId)
                    continue;

                if (!TryComp<STBulletinServerComponent>(progUid, out var server))
                    continue;

                var state = BuildUiState((progUid, board), server);
                _cartridgeLoader.UpdateCartridgeUiState(loaderUid, state);
            }
        }
    }

    #endregion

    #region Player Spawn & Init

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent args)
    {
        if (!_inventory.TryGetSlotEntity(args.Mob, "id", out var idEntity))
            return;

        if (!TryComp<PdaComponent>(idEntity, out _))
            return;

        // Initialize all bulletin board cartridges on this PDA
        var installed = _cartridgeLoader.GetInstalled(idEntity.Value);
        foreach (var progUid in installed)
        {
            if (!TryComp<STBulletinBoardComponent>(progUid, out _))
                continue;

            if (!TryComp<STBulletinServerComponent>(progUid, out var server))
                continue;

            if (!string.IsNullOrEmpty(server.OwnerCharacterName))
                continue;

            var userId = args.Player.UserId.UserId;
            InitializeBoardForPda(server, userId, args.Profile.Name, args.Mob);
        }
    }

    /// <summary>
    /// Initializes the bulletin board server component for a character.
    /// </summary>
    private static void InitializeBoardForPda(
        STBulletinServerComponent server,
        Guid userId,
        string charName,
        EntityUid mobUid)
    {
        server.OwnerUserId = userId;
        server.OwnerCharacterName = charName;
        server.OwnerMob = mobUid;
    }

    /// <summary>
    /// Lazy initialization when the board UI is opened but the component wasn't set up at spawn.
    /// </summary>
    private void TryLazyInit(
        EntityUid loaderUid,
        EntityUid cartridgeUid,
        STBulletinServerComponent server)
    {
        if (!TryComp<TransformComponent>(loaderUid, out var xform))
            return;

        var holder = xform.ParentUid;
        if (!holder.IsValid())
            return;

        if (!TryComp<ActorComponent>(holder, out var actor))
            return;

        var userId = actor.PlayerSession.UserId.UserId;
        var charName = MetaData(holder).EntityName;
        InitializeBoardForPda(server, userId, charName, holder);
    }

    #endregion

    #region Round Lifecycle

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        _offersByBoardType.Clear();
        _globalOfferIndex.Clear();
        _nextOfferId = 0;
        _activeLoaders.Clear();
    }

    #endregion

    #region Notifications

    /// <summary>
    /// Rings all PDAs with a bulletin board cartridge of the given type, except the poster's own PDA.
    /// For secondary offers on merc-restricted boards, only notifies band members.
    /// Also sets the notification badge on the cartridge for the program list.
    /// </summary>
    private void NotifyBoardRecipients(string boardTypeId, EntityUid posterMob, STBulletinCategory category)
    {
        var query = EntityQueryEnumerator<STBulletinBoardComponent, STBulletinServerComponent, CartridgeComponent>();
        while (query.MoveNext(out var uid, out var board, out var server, out var cartridge))
        {
            if (board.BoardTypeId != boardTypeId)
                continue;

            if (cartridge.LoaderUid is not { } loaderUid)
                continue;

            // Don't notify the poster's own PDA
            if (server.OwnerMob == posterMob)
                continue;

            // For secondary offers on merc-restricted boards, skip non-band-members
            if (category == STBulletinCategory.Secondary
                && TryComp<STMercBoardRestrictionsComponent>(uid, out var restrictions)
                && !IsBandMemberCached(server, restrictions))
            {
                continue;
            }

            // Set notification badge
            cartridge.HasNotification = true;
            Dirty(uid, cartridge);

            // Ring the PDA (unless muted)
            if (!server.Muted && TryComp<RingerComponent>(loaderUid, out var ringer))
                _ringer.RingerPlayRingtone((loaderUid, ringer));
        }
    }

    #endregion

    #region Helpers

    private Dictionary<uint, STBulletinOffer> GetOrCreateStorage(string boardTypeId)
    {
        if (!_offersByBoardType.TryGetValue(boardTypeId, out var storage))
        {
            storage = new Dictionary<uint, STBulletinOffer>();
            _offersByBoardType[boardTypeId] = storage;
        }

        return storage;
    }

    /// <summary>
    /// Checks if an entity is a member of a specific band.
    /// </summary>
    private bool IsBandMember(EntityUid uid, ProtoId<STBandPrototype> bandId)
    {
        if (!TryComp<BandsComponent>(uid, out var bands))
            return false;

        return bands.BandProto == bandId;
    }

    /// <summary>
    /// Checks merc board membership using the server component's cached owner mob.
    /// </summary>
    private bool IsBandMemberCached(STBulletinServerComponent server, STMercBoardRestrictionsComponent restrictions)
    {
        if (server.OwnerMob is not { } ownerMob)
            return false;

        return IsBandMember(ownerMob, restrictions.RequiredBandForPrimary);
    }

    /// <summary>
    /// Resolves the faction name for an entity via BandsComponent.
    /// </summary>
    private string? ResolveFaction(EntityUid uid)
    {
        if (!TryComp<BandsComponent>(uid, out var bands))
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

    /// <summary>
    /// Counts how many offers of a specific category a player has in a storage.
    /// </summary>
    private static int CountPlayerOffers(
        Dictionary<uint, STBulletinOffer> storage,
        string playerName,
        STBulletinCategory category)
    {
        var count = 0;
        foreach (var offer in storage.Values)
        {
            if (offer.Category == category && offer.PosterName == playerName)
                count++;
        }

        return count;
    }

    #endregion
}
