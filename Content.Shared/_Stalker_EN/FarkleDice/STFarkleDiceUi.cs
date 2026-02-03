using Robust.Shared.Serialization;

namespace Content.Shared._Stalker_EN.FarkleDice;

[Serializable, NetSerializable]
public enum STFarkleDiceUiKey : byte
{
    Key,
}

#region Messages

[Serializable, NetSerializable]
public sealed class STFarkleJoinMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class STFarkleLeaveMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class STFarkleRollMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class STFarkleSelectDieMessage : BoundUserInterfaceMessage
{
    public readonly int Index;

    public STFarkleSelectDieMessage(int index)
    {
        Index = index;
    }
}

[Serializable, NetSerializable]
public sealed class STFarkleKeepAndContinueMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class STFarkleBankMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class STFarkleNewGameMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class STFarkleSetTargetScoreMessage : BoundUserInterfaceMessage
{
    public readonly int TargetScore;

    public STFarkleSetTargetScoreMessage(int targetScore)
    {
        TargetScore = targetScore;
    }
}

#endregion

/// <summary>
/// UI state sent from server to client containing all data needed to render the Farkle game.
/// </summary>
[Serializable, NetSerializable]
public sealed class STFarkleDiceBoundUiState : BoundUserInterfaceState
{
    public readonly STFarkleDicePhase Phase;
    public readonly string? Player1Name;
    public readonly string? Player2Name;

    /// <summary>
    /// NetEntities needed for client to determine if it's their turn.
    /// </summary>
    public readonly NetEntity? Player1NetEntity;
    public readonly NetEntity? Player2NetEntity;

    public readonly int CurrentPlayer;
    public readonly int Player1Score;
    public readonly int Player2Score;
    public readonly int TurnScore;
    public readonly int[] DiceValues;
    public readonly bool[] SelectedDice;
    public readonly bool[] KeptDice;
    public readonly bool HasRolled;
    public readonly int TargetScore;

    /// <summary>
    /// Preview score from current selection, used to enable/disable Keep button.
    /// </summary>
    public readonly int SelectedScore;

    /// <summary>
    /// Indicates which dice can potentially score, used for visual hints.
    /// </summary>
    public readonly bool[] ScoringDice;

    public STFarkleDiceBoundUiState(
        STFarkleDicePhase phase,
        string? player1Name,
        string? player2Name,
        NetEntity? player1NetEntity,
        NetEntity? player2NetEntity,
        int currentPlayer,
        int player1Score,
        int player2Score,
        int turnScore,
        int[] diceValues,
        bool[] selectedDice,
        bool[] keptDice,
        bool hasRolled,
        int targetScore,
        int selectedScore,
        bool[] scoringDice)
    {
        Phase = phase;
        Player1Name = player1Name;
        Player2Name = player2Name;
        Player1NetEntity = player1NetEntity;
        Player2NetEntity = player2NetEntity;
        CurrentPlayer = currentPlayer;
        Player1Score = player1Score;
        Player2Score = player2Score;
        TurnScore = turnScore;
        DiceValues = diceValues;
        SelectedDice = selectedDice;
        KeptDice = keptDice;
        HasRolled = hasRolled;
        TargetScore = targetScore;
        SelectedScore = selectedScore;
        ScoringDice = scoringDice;
    }
}
