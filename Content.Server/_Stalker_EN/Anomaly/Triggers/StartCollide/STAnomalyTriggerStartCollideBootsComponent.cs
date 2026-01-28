namespace Content.Server._Stalker_EN.Anomaly.Triggers.StartCollide;

/// <summary>
/// Adds trigger groups based on whether the colliding entity has boots equipped.
/// Used by Glass Shards anomaly for boot-dependent damage.
/// </summary>
[RegisterComponent]
public sealed partial class STAnomalyTriggerStartCollideBootsComponent : Component
{
    /// <summary>
    /// Group added when entity has no boots AND is sprinting (double damage).
    /// </summary>
    [DataField]
    public string NoBootsSprintingGroup = "NoBootsSprinting";

    /// <summary>
    /// Group added when entity has no boots but is NOT sprinting (base damage).
    /// </summary>
    [DataField]
    public string NoBootsWalkingGroup = "NoBootsWalking";

    /// <summary>
    /// Inventory slot to check for boots.
    /// </summary>
    [DataField]
    public string SlotName = "shoes";
}
