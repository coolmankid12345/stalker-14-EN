namespace Content.Server._Stalker.Shovel;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent]
public sealed partial class StalkerShovelComponent : Component
{
    public bool IsFree = true;

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField]
    public float DoAfter = 2f;

    [DataField, ViewVariables]
    public bool CanPlow = true;

    [DataField, ViewVariables]
    public bool CanMakeGrave = true;
}
