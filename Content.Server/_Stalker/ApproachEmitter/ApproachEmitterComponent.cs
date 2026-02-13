namespace Content.Server._Stalker.ApproachEmitter;

/// <summary>
/// Entities with this component will trigger entities with ApproachTrigger component
/// </summary>
[RegisterComponent]
public sealed partial class ApproachEmitterComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool Enabled = true;

    // ST14-EN: commented this out and added it to ApproachTriggerComponent, removed UseMinRange
    /*
    [DataField]
    public float Range;

    [DataField]
    public bool UseMinRange;

    [DataField]
    public float MinRange;
    */
}
