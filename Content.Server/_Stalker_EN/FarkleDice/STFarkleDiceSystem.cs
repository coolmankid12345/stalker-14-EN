using Content.Server.Mind;
using Content.Server.Popups;
using Content.Shared._Stalker_EN.FarkleDice;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server._Stalker_EN.FarkleDice;

public sealed class STFarkleDiceSystem : EntitySystem
{
    private const int MinTargetScore = 1000;
    private const int MaxTargetScore = 50000;

    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STFarkleDiceComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<STFarkleDiceComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<STFarkleDiceComponent, BoundUIClosedEvent>(OnUiClosed);
        SubscribeLocalEvent<STFarkleDiceComponent, STFarkleJoinMessage>(OnJoin);
        SubscribeLocalEvent<STFarkleDiceComponent, STFarkleLeaveMessage>(OnLeave);
        SubscribeLocalEvent<STFarkleDiceComponent, STFarkleRollMessage>(OnRoll);
        SubscribeLocalEvent<STFarkleDiceComponent, STFarkleSelectDieMessage>(OnSelectDie);
        SubscribeLocalEvent<STFarkleDiceComponent, STFarkleKeepAndContinueMessage>(OnKeepAndContinue);
        SubscribeLocalEvent<STFarkleDiceComponent, STFarkleBankMessage>(OnBank);
        SubscribeLocalEvent<STFarkleDiceComponent, STFarkleNewGameMessage>(OnNewGame);
        SubscribeLocalEvent<STFarkleDiceComponent, STFarkleSetTargetScoreMessage>(OnSetTargetScore);
    }

    private void OnInit(EntityUid uid, STFarkleDiceComponent comp, ComponentInit args)
    {
        ResetGame(comp);
    }

    private void OnUiOpened(EntityUid uid, STFarkleDiceComponent comp, BoundUIOpenedEvent args)
    {
        UpdateUi(uid, comp, args.Actor);
    }

    private void OnUiClosed(EntityUid uid, STFarkleDiceComponent comp, BoundUIClosedEvent args)
    {
        var player = args.Actor;

        // Check if this player was in the game
        if (comp.Player1 == player)
        {
            // If game in progress, player 2 wins by forfeit
            if (comp.Phase != STFarkleDicePhase.WaitingForPlayers &&
                comp.Phase != STFarkleDicePhase.GameOver &&
                comp.Player2 != null)
            {
                _popup.PopupEntity(Loc.GetString("farkle-player-forfeit", ("winner", "2")), uid);
                comp.Phase = STFarkleDicePhase.GameOver;
            }
            else
            {
                comp.Player1 = null;
                if (comp.Phase != STFarkleDicePhase.GameOver)
                    comp.Phase = STFarkleDicePhase.WaitingForPlayers;
            }
        }
        else if (comp.Player2 == player)
        {
            // If game in progress, player 1 wins by forfeit
            if (comp.Phase != STFarkleDicePhase.WaitingForPlayers &&
                comp.Phase != STFarkleDicePhase.GameOver &&
                comp.Player1 != null)
            {
                _popup.PopupEntity(Loc.GetString("farkle-player-forfeit", ("winner", "1")), uid);
                comp.Phase = STFarkleDicePhase.GameOver;
            }
            else
            {
                comp.Player2 = null;
                if (comp.Phase != STFarkleDicePhase.GameOver)
                    comp.Phase = STFarkleDicePhase.WaitingForPlayers;
            }
        }

        Dirty(uid, comp);
        UpdateAllUi(uid, comp);
    }

    private void OnJoin(EntityUid uid, STFarkleDiceComponent comp, STFarkleJoinMessage args)
    {
        var player = args.Actor;

        // Already in the game?
        if (comp.Player1 == player || comp.Player2 == player)
            return;

        // Game already in progress?
        if (comp.Phase != STFarkleDicePhase.WaitingForPlayers && comp.Phase != STFarkleDicePhase.GameOver)
            return;

        // Try to join as player 1 or 2
        if (comp.Player1 == null)
        {
            comp.Player1 = player;
        }
        else if (comp.Player2 == null)
        {
            comp.Player2 = player;

            // Both players joined - start the game!
            StartGame(uid, comp);
        }
        else
        {
            // Game is full
            return;
        }

        Dirty(uid, comp);
        UpdateAllUi(uid, comp);
    }

    private void OnLeave(EntityUid uid, STFarkleDiceComponent comp, STFarkleLeaveMessage args)
    {
        var player = args.Actor;

        if (comp.Player1 == player)
        {
            if (comp.Phase != STFarkleDicePhase.WaitingForPlayers &&
                comp.Phase != STFarkleDicePhase.GameOver &&
                comp.Player2 != null)
            {
                _popup.PopupEntity(Loc.GetString("farkle-player-forfeit", ("winner", "2")), uid);
                comp.Phase = STFarkleDicePhase.GameOver;
            }
            else
            {
                comp.Player1 = null;
                if (comp.Phase != STFarkleDicePhase.GameOver)
                    comp.Phase = STFarkleDicePhase.WaitingForPlayers;
            }
        }
        else if (comp.Player2 == player)
        {
            if (comp.Phase != STFarkleDicePhase.WaitingForPlayers &&
                comp.Phase != STFarkleDicePhase.GameOver &&
                comp.Player1 != null)
            {
                _popup.PopupEntity(Loc.GetString("farkle-player-forfeit", ("winner", "1")), uid);
                comp.Phase = STFarkleDicePhase.GameOver;
            }
            else
            {
                comp.Player2 = null;
                if (comp.Phase != STFarkleDicePhase.GameOver)
                    comp.Phase = STFarkleDicePhase.WaitingForPlayers;
            }
        }

        Dirty(uid, comp);
        UpdateAllUi(uid, comp);
    }

    private void OnRoll(EntityUid uid, STFarkleDiceComponent comp, STFarkleRollMessage args)
    {
        var player = args.Actor;

        // Validate it's this player's turn
        if (!IsPlayersTurn(comp, player))
            return;

        // Can only roll in Rolling phase
        if (comp.Phase != STFarkleDicePhase.Rolling)
            return;

        // Already rolled this turn?
        if (comp.HasRolled)
            return;

        // Roll all non-kept dice
        for (var i = 0; i < 6; i++)
        {
            if (!comp.KeptDice[i])
                comp.DiceValues[i] = _random.Next(1, 7);
        }

        comp.HasRolled = true;

        // Clear selections
        for (var i = 0; i < 6; i++)
            comp.SelectedDice[i] = false;

        // Check for Farkle
        var scoringDice = STFarkleDiceScoring.GetScoringDice(comp.DiceValues, comp.KeptDice);
        var hasScoringDice = false;
        for (var i = 0; i < 6; i++)
        {
            if (scoringDice[i])
            {
                hasScoringDice = true;
                break;
            }
        }

        if (!hasScoringDice)
        {
            // Farkle!
            HandleFarkle(uid, comp);
        }
        else
        {
            comp.Phase = STFarkleDicePhase.SelectingDice;
        }

        Dirty(uid, comp);
        UpdateAllUi(uid, comp);
    }

    private void OnSelectDie(EntityUid uid, STFarkleDiceComponent comp, STFarkleSelectDieMessage args)
    {
        var player = args.Actor;

        // Validate it's this player's turn
        if (!IsPlayersTurn(comp, player))
            return;

        // Must be in selecting phase
        if (comp.Phase != STFarkleDicePhase.SelectingDice)
            return;

        var index = args.Index;
        if (index < 0 || index >= 6)
            return;

        // Can't select kept dice
        if (comp.KeptDice[index])
            return;

        // Toggle selection
        comp.SelectedDice[index] = !comp.SelectedDice[index];

        Dirty(uid, comp);
        UpdateAllUi(uid, comp);
    }

    private void OnKeepAndContinue(EntityUid uid, STFarkleDiceComponent comp, STFarkleKeepAndContinueMessage args)
    {
        var player = args.Actor;

        // Validate it's this player's turn
        if (!IsPlayersTurn(comp, player))
            return;

        // Must be in selecting phase
        if (comp.Phase != STFarkleDicePhase.SelectingDice)
            return;

        // Must have at least one die selected
        var hasSelection = false;
        for (var i = 0; i < 6; i++)
        {
            if (comp.SelectedDice[i])
            {
                hasSelection = true;
                break;
            }
        }

        if (!hasSelection)
        {
            _popup.PopupEntity(Loc.GetString("farkle-must-select-dice"), uid, player);
            return;
        }

        // Validate selection scores
        var selectedValues = GetSelectedDiceValues(comp);
        var score = STFarkleDiceScoring.CalculateScore(selectedValues);

        if (score == 0)
        {
            _popup.PopupEntity(Loc.GetString("farkle-invalid-selection"), uid, player);
            return;
        }

        // Add to turn score
        comp.TurnScore += score;

        // Mark selected dice as kept
        for (var i = 0; i < 6; i++)
        {
            if (comp.SelectedDice[i])
            {
                comp.KeptDice[i] = true;
                comp.SelectedDice[i] = false;
            }
        }

        // Check for hot dice (all 6 kept)
        if (CheckAndHandleHotDice(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("farkle-hot-dice"), uid);
        }

        // Back to rolling phase
        comp.Phase = STFarkleDicePhase.Rolling;
        comp.HasRolled = false;

        Dirty(uid, comp);
        UpdateAllUi(uid, comp);
    }

    private void OnBank(EntityUid uid, STFarkleDiceComponent comp, STFarkleBankMessage args)
    {
        var player = args.Actor;

        // Validate it's this player's turn
        if (!IsPlayersTurn(comp, player))
            return;

        // Must be in selecting phase
        if (comp.Phase != STFarkleDicePhase.SelectingDice)
            return;

        // Check if there's a selection - if so, add it first
        var hasSelection = false;
        for (var i = 0; i < 6; i++)
        {
            if (comp.SelectedDice[i])
            {
                hasSelection = true;
                break;
            }
        }

        if (hasSelection)
        {
            var selectedValues = GetSelectedDiceValues(comp);
            var score = STFarkleDiceScoring.CalculateScore(selectedValues);

            if (score == 0)
            {
                _popup.PopupEntity(Loc.GetString("farkle-invalid-selection"), uid, player);
                return;
            }

            comp.TurnScore += score;
        }

        // Must have something to bank
        if (comp.TurnScore == 0)
            return;

        BankPoints(uid, comp);
        UpdateAllUi(uid, comp);
    }

    private void OnNewGame(EntityUid uid, STFarkleDiceComponent comp, STFarkleNewGameMessage args)
    {
        // Only allow new game when game is over or waiting
        if (comp.Phase != STFarkleDicePhase.GameOver && comp.Phase != STFarkleDicePhase.WaitingForPlayers)
            return;

        // Reset but keep players
        ResetGame(comp);

        // If both players present, start immediately
        if (comp.Player1 != null && comp.Player2 != null)
            StartGame(uid, comp);

        Dirty(uid, comp);
        UpdateAllUi(uid, comp);
    }

    private void OnSetTargetScore(EntityUid uid, STFarkleDiceComponent comp, STFarkleSetTargetScoreMessage args)
    {
        var player = args.Actor;

        // Only the host (Player1) can change the target score
        if (comp.Player1 != player)
            return;

        // Only allowed during the waiting phase
        if (comp.Phase != STFarkleDicePhase.WaitingForPlayers)
            return;

        // Clamp to valid range
        var targetScore = Math.Clamp(args.TargetScore, MinTargetScore, MaxTargetScore);

        comp.TargetScore = targetScore;
        Dirty(uid, comp);
        UpdateAllUi(uid, comp);
    }

    private void ResetGame(STFarkleDiceComponent comp)
    {
        comp.Phase = STFarkleDicePhase.WaitingForPlayers;
        comp.CurrentPlayer = 1;
        comp.Player1Score = 0;
        comp.Player2Score = 0;
        comp.TurnScore = 0;
        comp.HasRolled = false;

        for (var i = 0; i < 6; i++)
        {
            comp.DiceValues[i] = 1;
            comp.SelectedDice[i] = false;
            comp.KeptDice[i] = false;
        }
    }

    private void StartGame(EntityUid uid, STFarkleDiceComponent comp)
    {
        ResetGame(comp);

        // Coin flip determines who goes first
        comp.CurrentPlayer = _random.Next(1, 3);
        comp.Phase = STFarkleDicePhase.Rolling;

        _popup.PopupEntity(Loc.GetString("farkle-game-started", ("player", comp.CurrentPlayer)), uid);
    }

    public void HandleFarkle(EntityUid uid, STFarkleDiceComponent comp)
    {
        _popup.PopupEntity(Loc.GetString("farkle-farkle"), uid);

        comp.TurnScore = 0;
        SwitchPlayer(comp);

        Dirty(uid, comp);
    }

    public void BankPoints(EntityUid uid, STFarkleDiceComponent comp)
    {
        if (comp.CurrentPlayer == 1)
            comp.Player1Score += comp.TurnScore;
        else
            comp.Player2Score += comp.TurnScore;

        var bankedScore = comp.TurnScore;
        comp.TurnScore = 0;

        // Check for win
        var winningScore = comp.CurrentPlayer == 1 ? comp.Player1Score : comp.Player2Score;
        if (winningScore >= comp.TargetScore)
        {
            comp.Phase = STFarkleDicePhase.GameOver;
            _popup.PopupEntity(Loc.GetString("farkle-player-wins", ("player", comp.CurrentPlayer)), uid);
        }
        else
        {
            SwitchPlayer(comp);
        }

        Dirty(uid, comp);
    }

    private void SwitchPlayer(STFarkleDiceComponent comp)
    {
        comp.CurrentPlayer = comp.CurrentPlayer == 1 ? 2 : 1;
        comp.Phase = STFarkleDicePhase.Rolling;
        comp.HasRolled = false;
        comp.TurnScore = 0;

        for (var i = 0; i < 6; i++)
        {
            comp.SelectedDice[i] = false;
            comp.KeptDice[i] = false;
        }
    }

    /// <summary>
    /// Checks for "hot dice" - when all 6 dice are kept, player gets to roll all dice again.
    /// </summary>
    public bool CheckAndHandleHotDice(EntityUid uid, STFarkleDiceComponent comp)
    {
        for (var i = 0; i < 6; i++)
        {
            if (!comp.KeptDice[i])
                return false;
        }

        for (var i = 0; i < 6; i++)
            comp.KeptDice[i] = false;

        comp.Phase = STFarkleDicePhase.Rolling;
        return true;
    }

    private bool IsPlayersTurn(STFarkleDiceComponent comp, EntityUid player)
    {
        var currentPlayerEntity = comp.CurrentPlayer == 1 ? comp.Player1 : comp.Player2;
        return currentPlayerEntity == player;
    }

    private int[] GetSelectedDiceValues(STFarkleDiceComponent comp)
    {
        var values = new List<int>();
        for (var i = 0; i < 6; i++)
        {
            if (comp.SelectedDice[i])
                values.Add(comp.DiceValues[i]);
        }
        return values.ToArray();
    }

    private string? GetPlayerName(EntityUid? player)
    {
        if (player == null)
            return null;

        return MetaData(player.Value).EntityName;
    }

    private void UpdateAllUi(EntityUid uid, STFarkleDiceComponent comp)
    {
        UpdateUi(uid, comp);
    }

    private void UpdateUi(EntityUid uid, STFarkleDiceComponent comp)
    {
        var selectedValues = GetSelectedDiceValues(comp);
        var selectedScore = STFarkleDiceScoring.CalculateScore(selectedValues);
        var scoringDice = STFarkleDiceScoring.GetScoringDice(comp.DiceValues, comp.KeptDice);

        NetEntity? player1NetEntity = comp.Player1 != null ? GetNetEntity(comp.Player1.Value) : null;
        NetEntity? player2NetEntity = comp.Player2 != null ? GetNetEntity(comp.Player2.Value) : null;

        var state = new STFarkleDiceBoundUiState(
            comp.Phase,
            GetPlayerName(comp.Player1),
            GetPlayerName(comp.Player2),
            player1NetEntity,
            player2NetEntity,
            comp.CurrentPlayer,
            comp.Player1Score,
            comp.Player2Score,
            comp.TurnScore,
            comp.DiceValues,
            comp.SelectedDice,
            comp.KeptDice,
            comp.HasRolled,
            comp.TargetScore,
            selectedScore,
            scoringDice
        );

        _ui.SetUiState(uid, STFarkleDiceUiKey.Key, state);
    }

    private void UpdateUi(EntityUid uid, STFarkleDiceComponent comp, EntityUid viewer)
    {
        UpdateUi(uid, comp);
    }
}
