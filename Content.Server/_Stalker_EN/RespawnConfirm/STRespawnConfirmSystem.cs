using Content.Server.EUI;
using Content.Server.GameTicking;
using Content.Shared._Stalker_EN.RespawnConfirm;
using Content.Shared._RD.DeathScreen;
using Content.Shared.Eui;
using Robust.Shared.Localization;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Stalker_EN.RespawnConfirm;

/// <summary>
/// Server-side EUI handler for the respawn confirmation dialog.
/// Handles player responses (accept/deny) and communicates with <see cref="STRespawnConfirmSystem"/>.
/// </summary>
public sealed class STRespawnConfirmEui : BaseEui
{
    private readonly STRespawnConfirmSystem _system;
    private readonly ICommonSession _session;
    private bool _handled;

    public STRespawnConfirmEui(STRespawnConfirmSystem system, ICommonSession session)
    {
        _system = system;
        _session = session;
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (_handled)
            return;

        _handled = true;

        if (msg is not STRespawnConfirmMessage choice ||
            choice.Button == STRespawnConfirmButton.Deny)
        {
            _system.HandleRespawnDenied(_session);
            Close();
            return;
        }

        // Player accepted - trigger death screen then respawn
        _system.HandleRespawnAccepted(_session);
        Close();
    }

    public override void Closed()
    {
        base.Closed();

        // Only handle if not already handled (e.g., player disconnected before choosing)
        if (_handled)
            return;

        _handled = true;
        _system.HandleRespawnDenied(_session);
    }
}

/// <summary>
/// Manages the respawn confirmation flow for dead players.
/// When a player moves while dead (attempting to ghost), shows a confirmation dialog.
/// If accepted, displays a death screen and schedules respawn after the animation completes.
/// </summary>
public sealed class STRespawnConfirmSystem : EntitySystem
{
    [Dependency] private readonly EuiManager _euiManager = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    /// <summary>
    /// Track sessions with pending respawns (after death screen shown)
    /// </summary>
    private readonly Dictionary<ICommonSession, TimeSpan> _pendingRespawns = new();

    /// <summary>
    /// Track sessions with open confirmation EUIs (prevent duplicates)
    /// </summary>
    private readonly HashSet<ICommonSession> _openConfirms = new();

    /// <summary>
    /// Duration to wait before respawning after death screen is shown.
    /// Matches the death screen animation: 4 seconds fade + 3 seconds delay.
    /// </summary>
    private static readonly TimeSpan DeathScreenDuration = TimeSpan.FromSeconds(7);

    /// <summary>
    /// Audio path for the death screen sound effect.
    /// </summary>
    private const string DeathScreenAudioPath = "/Audio/_RD/DeathScreen/controller.ogg";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<PlayerJoinedLobbyEvent>(OnPlayerJoinedLobby);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var toRemove = new List<ICommonSession>();
        foreach (var (session, respawnTime) in _pendingRespawns)
        {
            if (_timing.CurTime >= respawnTime)
            {
                _gameTicker.Respawn(session);
                toRemove.Add(session);
            }
        }

        foreach (var session in toRemove)
        {
            _pendingRespawns.Remove(session);
        }
    }

    private void OnPlayerDetached(PlayerDetachedEvent args)
    {
        // Clean up if player disconnects
        _openConfirms.Remove(args.Player);
        _pendingRespawns.Remove(args.Player);
    }

    private void OnPlayerJoinedLobby(PlayerJoinedLobbyEvent args)
    {
        // Ensure no stale pending respawn state remains when a player reaches the lobby
        // (e.g., body decomposed while respawn was pending)
        _openConfirms.Remove(args.PlayerSession);
        _pendingRespawns.Remove(args.PlayerSession);
    }

    /// <summary>
    /// Called from GhostSystem when player attempts to ghost (moves while dead)
    /// </summary>
    public void ShowRespawnConfirm(ICommonSession session)
    {
        // Don't show duplicate dialogs
        if (_openConfirms.Contains(session))
            return;

        // Don't show if already pending respawn
        if (_pendingRespawns.ContainsKey(session))
            return;

        var eui = new STRespawnConfirmEui(this, session);
        _euiManager.OpenEui(eui, session);
        _openConfirms.Add(session);
    }

    /// <summary>
    /// Called when player denies/cancels respawn or closes the dialog
    /// </summary>
    public void HandleRespawnDenied(ICommonSession session)
    {
        _openConfirms.Remove(session);
    }

    /// <summary>
    /// Called when player confirms respawn
    /// </summary>
    public void HandleRespawnAccepted(ICommonSession session)
    {
        _openConfirms.Remove(session);

        // Show death screen
        RaiseNetworkEvent(new RDDeathScreenShowEvent(
            Loc.GetString("st-respawn-death-screen-message"),
            audioPath: DeathScreenAudioPath),
            session.Channel);

        // Schedule respawn after death screen completes
        _pendingRespawns[session] = _timing.CurTime + DeathScreenDuration;
    }
}
