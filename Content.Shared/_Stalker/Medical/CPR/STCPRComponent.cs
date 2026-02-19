using Robust.Shared.GameStates;

namespace Content.Shared._Stalker.Medical.CPR;

[RegisterComponent, NetworkedComponent]
public sealed partial class STCPRComponent : Component
{
    [DataField]
    public float DoAfterDuration = 4f;

    [DataField]
    public float HealAmount = 15f;

    [DataField]
    public float CooldownSeconds = 7f;
}
