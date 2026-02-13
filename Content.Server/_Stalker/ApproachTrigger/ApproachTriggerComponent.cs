using Content.Server._Stalker_EN.Approach; // ST14-EN addition
using Content.Shared.Whitelist;

namespace Content.Server._Stalker.ApproachTrigger;

[Obsolete("Use TriggerOnProximityComponent instead")] // ST14-EN addition
[RegisterComponent]
public sealed partial class ApproachTriggerComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public bool Enabled = true;

    // ST14-EN change: Moved from ApproachEmitterComponent to here
    [DataField]
    [Access(typeof(ApproachTriggerMigrationSystem))] /* ST14-EN: added access modifier, because this depends on fixture size */
    public float Range = 30f;

    // ST14-EN change: Moved from ApproachEmitterComponent to here
    [DataField]
    public float MinRange = 20f;
}
