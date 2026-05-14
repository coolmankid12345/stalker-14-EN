using System.Linq;
using Content.Server._Stalker_EN.FactionRelations;
using Content.Server.Administration.Managers;
using Content.Server.CartridgeLoader;
using Content.Server.CartridgeLoader.Events;
using Content.Server.PDA;
using Content.Shared._Stalker.Bands;
using Content.Shared._Stalker.Bands.Components;
using Content.Shared._Stalker_EN.CharacterRank;
using Content.Shared._Stalker_EN.FactionRelations;
using Content.Shared._Stalker_EN.Leaderboard;
using Content.Shared._Stalker_EN.Portraits;
using Content.Shared.CartridgeLoader;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.PDA;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Stalker_EN.Leaderboard;

/// <summary>
/// Server-side system that manages the Stalker Leaderboard cartridge.
/// Uses BandsComponent for faction display, STFactionRelationsCartridgeSystem for relations.
/// Supports multiple characters per player (keyed by UserId + CharacterName).
/// </summary>
public sealed partial class STLeaderboardSystem : EntitySystem
{
    [Dependency] private readonly CartridgeLoaderSystem _cartridgeLoader = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IAdminManager _adminManager = default!;
    [Dependency] private readonly SharedSTFactionResolutionSystem _factionResolution = default!;
    [Dependency] private readonly STFactionRelationsCartridgeSystem _factionRelations = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IConsoleHost _consoleHost = default!;
    [Dependency] private readonly ILogManager _logManager = default!;
    [Dependency] private readonly ILocalizationManager _loc = default!;

    private ISawmill _sawmill = default!;

    /// <summary>
    /// PDAs with leaderboard cartridge currently active (UI open). Receive broadcast updates.
    /// </summary>
    private readonly HashSet<EntityUid> _activeLoaders = new();

    /// <summary>
    /// Cached set of all PDAs that have a leaderboard cartridge.
    /// Avoids full entity query in BroadcastUiState (pattern from STMessenger).
    /// Stores (CartridgeUid, PdaUid) — resolve components via TryComp to avoid stale references.
    /// </summary>
    private readonly Dictionary<EntityUid, (EntityUid Cartridge, EntityUid Pda)> _leaderboardPdas = new();
    private static readonly ProtoId<STBandPrototype> ClearSkyBandId = "STClearSkyBand";

    /// <summary>
    /// Cached static data for a stalker entry.
    /// All data is captured at spawn time and never changes until respawn.
    /// </summary>
    private record StalkerData(
        string Name,
        EntityUid? Mob,
        string? BandName,
        string? BandIcon,
        string? FactionId,
        string? RelationFactionId, // Original faction for relation checks
        string? DisguisedRelationFactionId, // Disguised faction for others' relation checks
        int RankIndex,
        string? RankName,
        TimeSpan AccumulatedTime,
        string? PortraitPath,
        bool UsePatch,
        string? SelfPortraitPath,
        bool SelfUsePatch,
        string? DisplayBandName,
        string? SelfDisplayBandName, // Original faction for self-view
        bool Hidden = false);

    /// <summary>
    /// Cache of all known stalkers. Key includes both UserId and CharacterName.
    /// All stats are cached at spawn time and never change until respawn.
    /// When Mob is deleted, entry still shows cached data.
    /// Entries persist across player disconnects until round restart.
    /// </summary>
    private readonly Dictionary<StalkerKey, StalkerData> _knownStalkers = new();

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = _logManager.GetSawmill("st.leaderboard");

        SubscribeLocalEvent<STLeaderboardCartridgeComponent, CartridgeUiReadyEvent>(OnUiReady);
        SubscribeLocalEvent<STLeaderboardCartridgeComponent, CartridgeActivatedEvent>(OnCartridgeActivated);
        SubscribeLocalEvent<STLeaderboardCartridgeComponent, CartridgeDeactivatedEvent>(OnCartridgeDeactivated);
        SubscribeLocalEvent<STLeaderboardCartridgeComponent, EntityTerminatingEvent>(OnCartridgeTerminating);
        SubscribeLocalEvent<STLeaderboardServerComponent, EntityTerminatingEvent>(OnServerTerminating);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<STLeaderboardCartridgeComponent, CartridgeMessageEvent>(OnCartridgeMessage);
        SubscribeLocalEvent<STLeaderboardCartridgeComponent, CartridgeGetStateEvent>(OnGetState);
        SubscribeLocalEvent<STCharacterRankComponent, STCharacterRankLoadedEvent>(OnCharacterRankLoaded);

        _consoleHost.RegisterCommand("leaderboard-clear",
            "Clears all entries from the stalker leaderboard.",
            "Usage: leaderboard-clear",
            LeaderboardClearCommand);

        _consoleHost.RegisterCommand("leaderboard-list",
            "Lists all entries in the stalker leaderboard.",
            "Usage: leaderboard-list",
            LeaderboardListCommand);

        _consoleHost.RegisterCommand("leaderboard-remove",
            "Removes entries matching a character name from the stalker leaderboard.",
            "Usage: leaderboard-remove <name>",
            LeaderboardRemoveCommand);
    }

    /// <summary>
    /// Returns true if the entity is a real player MobHuman.
    /// All doll/NPC prototypes use different IDs, so this strictly filters real players.
    /// </summary>
    private bool IsPlayerMob(EntityUid mob) => MetaData(mob).EntityPrototype?.ID == "MobHuman";

    /// <summary>
    /// Called when the cartridge UI is first opened.
    /// </summary>
    private void OnUiReady(EntityUid uid, STLeaderboardCartridgeComponent component, CartridgeUiReadyEvent args)
    {
        // Only send UI state if the loader is active (prevents updates during program switching)
        if (!_activeLoaders.Contains(args.Loader))
            return;

        // Broadcast UI state to all active loaders
        BroadcastUiState();
    }
    private void OnGetState(EntityUid uid, STLeaderboardCartridgeComponent component, CartridgeGetStateEvent args)
    {
        if (!TryComp<STLeaderboardServerComponent>(uid, out var server))
            return;

        args.State = BuildUiState(args.LoaderUid, server);
    }

    private void OnCartridgeActivated(EntityUid uid, STLeaderboardCartridgeComponent component, ref CartridgeActivatedEvent args)
    {
        _activeLoaders.Add(args.Loader);

        // Initialize owner data when cartridge is activated
        TryInitializeLeaderboard(args.Loader);
    }

    private void OnCartridgeDeactivated(EntityUid uid, STLeaderboardCartridgeComponent component, ref CartridgeDeactivatedEvent args)
    {
        _activeLoaders.Remove(args.Loader);
    }

    private void OnCartridgeTerminating(EntityUid uid, STLeaderboardCartridgeComponent component, ref EntityTerminatingEvent args)
    {
        // The loader is the PDA entity that owns this cartridge
        if (TryComp<TransformComponent>(uid, out var xform) && xform.ParentUid.IsValid())
        {
            _activeLoaders.Remove(xform.ParentUid);
            _leaderboardPdas.Remove(xform.ParentUid);
        }
    }

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent args)
    {
        if (!args.Mob.IsValid())
            return;

        var mob = args.Mob;
        var session = args.Player;

        // Only real player mobs (MobHuman) - filters out dolls, NPCs, etc.
        if (!IsPlayerMob(mob))
            return;

        if (HasComp<GhostComponent>(mob))
            return;

        // Cache PDA with leaderboard cartridge (pattern from STMessenger)
        if (TryComp<TransformComponent>(mob, out var xform))
        {
            var current = xform.ParentUid;
            while (current.IsValid())
            {
                if (TryComp<CartridgeLoaderComponent>(current, out var loader) &&
                    TryComp<STLeaderboardCartridgeComponent>(loader.ActiveProgram, out var cartridge))
                {
                    _leaderboardPdas[current] = (cartridge.Owner, current);

                    // Initialize owner data for this PDA
                    TryInitializeLeaderboard(current);
                    break;
                }

                var parentXform = CompOrNull<TransformComponent>(current);
                if (parentXform == null)
                    break;

                current = parentXform.ParentUid;
            }
        }

        // Add to leaderboard
        UpdateStalkerEntry(session);

        // Broadcast UI update to all active loaders
        BroadcastUiState();
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        _knownStalkers.Clear();
    }

    /// <summary>
    /// Attempts to initialize the leaderboard on a PDA for the given loader.
    /// </summary>
    private void TryInitializeLeaderboard(EntityUid loaderUid)
    {
        if (!_cartridgeLoader.TryGetProgram<STLeaderboardCartridgeComponent>(loaderUid, out var progUid, out _))
            return;

        // Add to cache even if already initialized (needed for program switching)
        _leaderboardPdas[loaderUid] = (progUid.Value, loaderUid);

        // Ensure the server component exists
        var server = EnsureComp<STLeaderboardServerComponent>(progUid.Value);

        // Guard: OwnerCharacterName is set synchronously, so if already set, skip to avoid double-loading
        if (!string.IsNullOrEmpty(server.OwnerCharacterName))
            return;

        // Get holder from TransformComponent
        if (!TryComp<TransformComponent>(loaderUid, out var xform))
            return;

        var holderUid = xform.ParentUid;
        if (!holderUid.IsValid())
            return;

        if (!TryComp<ActorComponent>(holderUid, out var actor))
            return;

        var userId = actor.PlayerSession.UserId.UserId;
        var charName = MetaData(holderUid).EntityName;

        InitializeLeaderboardForPda(loaderUid, progUid.Value, server, userId, charName, holderUid);
    }

    /// <summary>
    /// Shared logic for initializing a leaderboard PDA for a character.
    /// </summary>
    private void InitializeLeaderboardForPda(
        EntityUid pdaUid,
        EntityUid cartridgeUid,
        STLeaderboardServerComponent server,
        Guid userId,
        string charName,
        EntityUid holderUid)
    {
        server.OwnerUserId = userId;
        server.OwnerCharacterName = charName;

        _leaderboardPdas[pdaUid] = (cartridgeUid, pdaUid);
    }

    /// <summary>
    /// Handles server component termination.
    /// </summary>
    private void OnServerTerminating(Entity<STLeaderboardServerComponent> ent, ref EntityTerminatingEvent args)
    {
        // Guard against race: only remove if this entity is still the registered PDA for this character
        if (!string.IsNullOrEmpty(ent.Comp.OwnerCharacterName))
        {
            var key = (ent.Comp.OwnerUserId, ent.Comp.OwnerCharacterName);

            if (_leaderboardPdas.TryGetValue(ent.Owner, out var existing) && existing.Pda == ent.Owner)
                _leaderboardPdas.Remove(ent.Owner);
        }

        // The loader is the PDA entity that owns this cartridge
        if (TryComp<TransformComponent>(ent, out var xform) && xform.ParentUid.IsValid())
        {
            _activeLoaders.Remove(xform.ParentUid);
            _leaderboardPdas.Remove(xform.ParentUid);
        }
    }

    /// <summary>
    /// Gets the faction name for AltBand mapping.
    /// When IsDisguised, BandStatusIcon contains the original AltBand value (e.g. "stalker")
    /// AltBand contains the original BandStatusIcon (e.g. "cn") - not usable for mapping.
    /// </summary>
    private string? GetAltBandFaction(BandsComponent bands)
    {
        var bandForMapping = bands.IsDisguised ? bands.BandStatusIcon : bands.AltBand;
        if (string.IsNullOrEmpty(bandForMapping))
            return null;
        return _factionResolution.GetBandFactionName(bandForMapping);
    }

    /// <summary>
    /// Gets the faction ID for relation checks based on the player's band prototype.
    /// Returns mapped faction names (Duty, Freedom, etc.) for proper relation calculation.
    /// </summary>
    private string? GetPlayerFaction(EntityUid mob)
    {
        if (!TryComp<BandsComponent>(mob, out var bands))
            return null;

        string? rawFactionId = null;

        // Get faction from BandProto Name (not FactionId) to match bandMapping
        // bandMapping uses band names (e.g. "Bandits", "Freedom") not factionIds (e.g. "Bandit")
        if (bands.BandProto.HasValue && _proto.TryIndex(bands.BandProto.Value, out STBandPrototype? bandProto))
        {
            rawFactionId = bandProto.Name;
        }
        // Fallback: resolve band name via mapping
        else if (!string.IsNullOrEmpty(bands.BandName))
        {
            rawFactionId = _factionResolution.GetBandFactionName(bands.BandName);
        }

        if (string.IsNullOrEmpty(rawFactionId))
            return null;

        // Use bandMapping from defaults.yml via _factionResolution
        return _factionResolution.GetBandFactionName(rawFactionId);
    }

    /// <summary>
    /// Updates or creates a leaderboard entry for a specific player.
    /// All data is captured at spawn time and never changes until respawn.
    /// </summary>
    private void UpdateStalkerEntry(ICommonSession session)
    {
        if (session.AttachedEntity is not { } mob)
            return;

        var characterName = MetaData(mob).EntityName;
        if (string.IsNullOrEmpty(characterName))
            return;

        var key = new StalkerKey(session.UserId, characterName);

        // Capture all static data at spawn time
        string? bandName = null;
        string? bandIcon = null;
        string? factionId = null;
        string? relationFactionId = null;
        string? disguisedRelationFactionId = null;
        int rankIndex = 0;
        string? rankName = null;
        TimeSpan accumulatedTime = TimeSpan.Zero;
        string? portraitPath = null;
        bool usePatch = true;
        string? selfPortraitPath = null;
        bool selfUsePatch = true;
        string? displayBandName = null;
        string? selfDisplayBandName = null;

        // Get band data
        if (TryComp<BandsComponent>(mob, out var bands))
        {
            bandName = bands.BandName;
            bandIcon = bands.BandStatusIcon;
            factionId = GetPlayerFaction(mob);

            // Get display band name (mapped)
            if (bands.BandProto.HasValue && _proto.TryIndex(bands.BandProto.Value, out var bandProto))
            {
                // Store mapped faction for relation checks
                relationFactionId = _factionResolution.GetBandFactionName(bandProto.Name);

                // Store disguised faction for others' relation checks
                if (bandProto.Name.Equals("Monolith", StringComparison.OrdinalIgnoreCase))
                {
                    disguisedRelationFactionId = relationFactionId; // Monolith never disguised
                }
                // Clear Sky maps to Loners when disguised
                else if (bands.BandProto == ClearSkyBandId && bands.IsDisguised)
                {
                    disguisedRelationFactionId = _factionResolution.GetBandFactionName(bands.BandName);
                }
                else
                {
                    disguisedRelationFactionId = relationFactionId; // Not disguised
                }

                // For self-view, always use original faction name
                selfDisplayBandName = bandProto.Name;

                if (bandProto.Name.Equals("Monolith", StringComparison.OrdinalIgnoreCase))
                {
                    displayBandName = factionId;
                }
                // Clear Sky maps to Loners when disguised (same logic as STMessenger)
                else if (bands.BandProto == ClearSkyBandId && bands.IsDisguised)
                {
                    displayBandName = _factionResolution.GetBandFactionName(bands.BandName);
                }
                else if (!string.IsNullOrEmpty(bands.AltBand))
                {
                    displayBandName = GetAltBandFaction(bands);
                }
                else
                {
                    displayBandName = factionId;
                }
            }
            else
            {
                displayBandName = factionId;
                selfDisplayBandName = factionId;
            }
        }

        // Get rank data
        if (TryComp<STCharacterRankComponent>(mob, out var rankComp))
        {
            accumulatedTime = rankComp.AccumulatedTime;
            rankName = _loc.GetString(rankComp.RankName);
            rankIndex = rankComp.RankIndex;
        }

        // Get portrait data - capture both disguised (for others) and original (for self)
        (portraitPath, usePatch) = GetPortraitOrPatch(mob, factionId, isMe: false);
        (selfPortraitPath, selfUsePatch) = GetPortraitOrPatch(mob, factionId, isMe: true);

        // Preserve hidden state and existing rank data if entry already exists
        bool hidden = false;
        if (_knownStalkers.TryGetValue(key, out var existingData))
        {
            hidden = existingData.Hidden;
            // If this is an update and we have default rank values, preserve existing rank
            // This prevents overwriting valid rank data with defaults when rank hasn't loaded yet
            if (rankIndex == 0 && existingData.RankIndex > 0)
            {
                rankIndex = existingData.RankIndex;
                rankName = existingData.RankName;
                accumulatedTime = existingData.AccumulatedTime;
            }
        }

        // Create/update entry with cached data
        _knownStalkers[key] = new StalkerData(
            characterName,
            mob,
            bandName,
            bandIcon,
            factionId,
            relationFactionId,
            disguisedRelationFactionId,
            rankIndex,
            rankName,
            accumulatedTime,
            portraitPath,
            usePatch,
            selfPortraitPath,
            selfUsePatch,
            displayBandName,
            selfDisplayBandName,
            hidden);
    }

    /// <summary>
    /// Gets the faction relation type between two factions.
    /// Uses original faction names (ClearSky, Duty, etc.) for accurate relation calculation.
    /// </summary>
    private STLeaderboardFactionRelation GetRelation(string viewerFaction, string targetFaction)
    {
        if (string.IsNullOrEmpty(viewerFaction) || string.IsNullOrEmpty(targetFaction))
            return STLeaderboardFactionRelation.Neutral;

        if (viewerFaction == targetFaction)
            return STLeaderboardFactionRelation.Same;

        var relationType = _factionRelations.GetRelation(viewerFaction, targetFaction);

        return relationType switch
        {
            STFactionRelationType.Alliance => STLeaderboardFactionRelation.Alliance,
            STFactionRelationType.Neutral => STLeaderboardFactionRelation.Neutral,
            STFactionRelationType.Hostile => STLeaderboardFactionRelation.Hostile,
            STFactionRelationType.War => STLeaderboardFactionRelation.War,
            _ => STLeaderboardFactionRelation.Neutral,
        };
    }

    /// <summary>
    /// Broadcasts personalized leaderboard state to all open cartridges.
    /// Each viewer gets colors relative to their own faction.
    /// Uses cached PDAs to avoid full entity query (pattern from STMessenger).
    /// Uses stored owner data from STLeaderboardServerComponent to avoid ActorComponent lookup.
    /// </summary>
    private void BroadcastUiState()
    {
        // Use cached PDAs instead of full entity query (pattern from STMessenger)
        foreach (var (pdaUid, (cartridgeUid, _)) in _leaderboardPdas)
        {
            if (!TryComp<STLeaderboardCartridgeComponent>(cartridgeUid, out _))
                continue;

            // Only send to active loaders (UI open)
            if (!_activeLoaders.Contains(pdaUid))
                continue;

            if (!TryComp<STLeaderboardServerComponent>(cartridgeUid, out var server))
                continue;

            var state = BuildUiState(pdaUid, server);
            _cartridgeLoader.UpdateCartridgeUiState(pdaUid, state);
        }
    }

    /// <summary>
    /// Builds personalized leaderboard state for a specific loader.
    /// Colors are computed relative to the viewer's faction.
    /// The viewer's own entry is marked with IsMe=true for client-side pinning.
    /// All data is read from cache, not live components.
    /// </summary>
    private STLeaderboardUiState BuildUiState(EntityUid loaderUid, STLeaderboardServerComponent server)
    {
        string? viewerFaction = null;
        string? viewerName = server.OwnerCharacterName;

        // Try to get the actual session from the player manager
        if (_playerManager.TryGetSessionById(new NetUserId(server.OwnerUserId), out var viewerSession))
        {
            if (viewerSession.AttachedEntity is { } viewerMob)
            {
                viewerFaction = GetPlayerFaction(viewerMob);
            }
        }

        var entries = _knownStalkers.Values
            .Select(v =>
            {
                var isMe = viewerName != null && v.Name == viewerName;

                // Get relation using cached faction ID
                STLeaderboardFactionRelation relation = STLeaderboardFactionRelation.Neutral;
                string? factionForRelation = isMe ? v.RelationFactionId : v.DisguisedRelationFactionId;
                if (factionForRelation != null && viewerFaction != null)
                {
                    relation = GetRelation(viewerFaction, factionForRelation);
                }

                return new STLeaderboardEntry(
                    v.Name,
                    v.BandName,
                    v.BandIcon,
                    v.RankIndex,
                    v.RankName,
                    relation,
                    IsMe: isMe,
                    AccumulatedTime: v.AccumulatedTime,
                    PortraitPath: isMe ? v.SelfPortraitPath : v.PortraitPath,
                    UsePatchInsteadOfPortrait: isMe ? v.SelfUsePatch : v.UsePatch,
                    isMe ? v.SelfDisplayBandName : v.DisplayBandName,
                    v.Hidden);
            })
            .Where(e => !e.Hidden || e.IsMe) // Filter out hidden entries, but always show viewer's own entry
            .OrderByDescending(e => e.RankIndex)
            .ThenByDescending(e => e.AccumulatedTime)
            .ThenBy(e => e.CharacterName)
            .ToList();

        return new STLeaderboardUiState(entries);
    }

    /// <summary>
    /// Gets the portrait path for a stalker, or determines if patch should be used instead.
    /// For factions with AltBand (Clear Sky, Duty, Freedom), always use disguised portrait.
    /// Monolith is never disguised - always use true portrait.
    /// Priority: disguised portrait (for AltBand factions) > normal portrait > patch (for disguise-capable factions) > patch (fallback)
    /// </summary>
    private (string? PortraitPath, bool UsePatch) GetPortraitOrPatch(EntityUid? mob, string? factionId, bool isMe)
    {
        if (!mob.HasValue || !mob.Value.IsValid())
            return (null, true); // Offline - use patch

        // If viewer is the target, always use true portrait
        if (isMe)
        {
            if (TryComp<CharacterPortraitComponent>(mob.Value, out var myPortrait))
            {
                if (!string.IsNullOrEmpty(myPortrait.PortraitTexturePath))
                    return (myPortrait.PortraitTexturePath, false);
            }
            return (null, true);
        }

        // Check if this is a faction with AltBand (always disguised in leaderboard)
        bool shouldUseDisguisedPortrait = false;
        if (TryComp<BandsComponent>(mob.Value, out var bands))
        {
            if (bands.BandProto.HasValue && _proto.TryIndex(bands.BandProto.Value, out var bandProto))
            {
                // Monolith is never disguised
                if (!bandProto.Name.Equals("Monolith", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(bands.AltBand))
                {
                    shouldUseDisguisedPortrait = true;
                }
            }
        }

        // Try to get portrait from CharacterPortraitComponent first
        if (TryComp<CharacterPortraitComponent>(mob.Value, out var portrait))
        {
            // If faction with AltBand, always use disguised portrait
            if (shouldUseDisguisedPortrait && !string.IsNullOrEmpty(portrait.DisguisedPortraitPath))
                return (portrait.DisguisedPortraitPath, false);

            // Otherwise use normal portrait
            if (!string.IsNullOrEmpty(portrait.PortraitTexturePath))
                return (portrait.PortraitTexturePath, false);
        }

        // No portrait available - use patch
        return (null, true);
    }

    /// <summary>
    /// Clears all entries from the leaderboard (admin use).
    /// </summary>
    public void ClearLeaderboard()
    {
        _knownStalkers.Clear();
        BroadcastUiState();
    }

    /// <summary>
    /// Removes a specific stalker from the leaderboard.
    /// </summary>
    public bool RemoveStalker(StalkerKey key)
    {
        if (_knownStalkers.Remove(key))
        {
            BroadcastUiState();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Handles cartridge messages from client.
    /// </summary>
    private void OnCartridgeMessage(EntityUid uid, STLeaderboardCartridgeComponent component, CartridgeMessageEvent args)
    {
        if (args is STLeaderboardToggleHiddenMessage)
        {
            if (!TryComp<STLeaderboardServerComponent>(uid, out var server))
                return;

            if (string.IsNullOrEmpty(server.OwnerCharacterName))
                return;

            var key = new StalkerKey(new NetUserId(server.OwnerUserId), server.OwnerCharacterName);

            if (_knownStalkers.TryGetValue(key, out var data))
            {
                // Toggle hidden state
                var newData = data with { Hidden = !data.Hidden };
                _knownStalkers[key] = newData;

                // Broadcast update to all clients
                BroadcastUiState();
            }
        }
    }

    /// <summary>
    /// Handles rank data loaded event to update leaderboard with actual rank from database.
    /// This fires after CharacterRank system loads data from DB, ensuring we capture the correct rank at spawn.
    /// </summary>
    private void OnCharacterRankLoaded(EntityUid uid, STCharacterRankComponent comp, STCharacterRankLoadedEvent args)
    {
        if (!TryComp<ActorComponent>(uid, out var actor))
            return;

        var session = actor.PlayerSession;
        // Use character name from entity metadata, not session name
        var characterName = MetaData(uid).EntityName;
        if (string.IsNullOrEmpty(characterName))
            return;

        var key = new StalkerKey(session.UserId, characterName);

        // Update rank data in existing entry if present, or create new entry
        if (_knownStalkers.TryGetValue(key, out var existingData))
        {
            // Preserve existing data but update rank from the loaded event
            var rankName = _loc.GetString(args.RankName);
            _knownStalkers[key] = existingData with
            {
                RankIndex = args.RankIndex,
                RankName = rankName,
                AccumulatedTime = args.AccumulatedTime
            };
        }
        else
        {
            // Create new entry with rank data
            UpdateStalkerEntry(session);
        }

        BroadcastUiState();
    }
}
