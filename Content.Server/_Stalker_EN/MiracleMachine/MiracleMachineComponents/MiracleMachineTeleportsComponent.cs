using Robust.Shared.Prototypes;

namespace Content.Server._Stalker_EN.MiracleMachine.MiracleMachineComponents;

/// <summary>
/// Put on to teleporter spawners that will enable new routes when the Miracle Machine is disabled.
/// </summary>
[RegisterComponent]
public sealed partial class MiracleMachineTeleportsComponent : Component
{
    [DataField("teleport"), AutoNetworkedField]
    public EntProtoId Teleport = string.Empty;
}
