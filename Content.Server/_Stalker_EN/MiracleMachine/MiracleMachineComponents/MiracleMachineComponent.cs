namespace Content.Server._Stalker_EN.MiracleMachine.MiracleMachineComponents;

/// <summary>
/// Used on Miracle Machine itself, tracks active batteries for shutdown.
/// </summary>
[RegisterComponent]
public sealed partial class MiracleMachineComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public List<EntityUid> Batteries = new();
}
