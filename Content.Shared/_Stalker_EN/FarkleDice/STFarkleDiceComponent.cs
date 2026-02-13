using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Stalker_EN.FarkleDice;

/// <summary>
/// Component for the Farkle dice game table. Stores all game state for a two-player session.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class STFarkleDiceComponent : Component
{
    [DataField, AutoNetworkedField]
    public STFarkleDicePhase Phase = STFarkleDicePhase.WaitingForPlayers;

    [DataField, AutoNetworkedField]
    public EntityUid? Player1;

    [DataField, AutoNetworkedField]
    public EntityUid? Player2;

    [DataField, AutoNetworkedField]
    public int CurrentPlayer = 1;

    [DataField, AutoNetworkedField]
    public int Player1Score;

    [DataField, AutoNetworkedField]
    public int Player2Score;

    /// <summary>
    /// Points accumulated this turn, lost on Farkle or added to total on bank.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int TurnScore;

    [DataField, AutoNetworkedField]
    public int[] DiceValues = [1, 1, 1, 1, 1, 1];

    [DataField, AutoNetworkedField]
    public bool[] SelectedDice = [false, false, false, false, false, false];

    /// <summary>
    /// Dice kept this turn cannot be rerolled until banked or Farkled.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool[] KeptDice = new bool[6];

    [DataField, AutoNetworkedField]
    public bool HasRolled;

    [DataField]
    public int TargetScore = 10000;
}

[Serializable, NetSerializable]
public enum STFarkleDicePhase : byte
{
    WaitingForPlayers,
    Rolling,
    SelectingDice,
    GameOver,
}
