using Robust.Shared.Prototypes;

namespace Content.Server._Stalker.SpawnOnApproach;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class SpawnOnApproachComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public bool Enabled;

    /// <summary>
    /// Determines whether to spawn entities on componentInit
    /// </summary>
    [DataField]
    public bool InstantSpawn;

    [DataField("prototypes")]
    public List<EntProtoId> EntProtoIds;

    [DataField("restricted")]
    public List<EntProtoId> RestrictedProtos;

    [DataField]
    public int MinAmount;

    [DataField]
    public int MaxAmount;

    [DataField]
    public float MaxOffset;

    [DataField]
    public float MinOffset;

    [DataField]
    public float Chance;

    // ST14-EN Addition
    [DataField]
    public bool SpawnNearPlayers = false;

    /// <summary>
    /// If system should avoid spawning entities inside each other
    /// Useful when you need to spawn some static objects, like bushes
    /// </summary>
    [DataField]
    public bool SpawnInside = true;

    /// <summary>
    /// Cooldown in minutes
    /// </summary>
    [DataField]
    public float Cooldown;

    /// <summary>
    /// System field to track cooldown
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly), AutoPausedField]
    public TimeSpan? CoolDownTime;

    [DataField]
    public TimeSpan? MinStartAction;

    /// <summary>
    /// Set timeout on each dice roll
    /// It's needed for crates triggers. Or they will try to spawn each time when somebody goes by
    /// Making chance of spawn basically useless
    /// </summary>
    [DataField("timeoutOnRoll")]
    public bool ShouldTimeoutOnRoll;
}
