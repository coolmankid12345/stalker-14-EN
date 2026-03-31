using Robust.Shared.Prototypes;

namespace Content.Server._Stalker_EN.MiracleMachine.MiracleMachineComponents;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent]
public sealed partial class MiracleMachineSpawnerComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public List<EntityUid> Inside = new();

    [DataField("ghosts"), AutoNetworkedField]
    public List<EntProtoId> Ghosts = new();

    /// <summary>
    /// If true, this spawner will be removed when the miracle machine is disabled.
    /// </summary>
    [DataField("miracleMachine"), AutoNetworkedField]
    public bool MiracleMachine = true;
}
