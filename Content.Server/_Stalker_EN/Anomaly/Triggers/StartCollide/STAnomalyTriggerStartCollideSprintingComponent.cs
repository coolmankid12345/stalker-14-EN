namespace Content.Server._Stalker_EN.Anomaly.Triggers.StartCollide;

/// <summary>
/// Adds a trigger group when the colliding entity is sprinting.
/// Used by Glass Shards anomaly to increase damage for running entities.
/// </summary>
[RegisterComponent]
public sealed partial class STAnomalyTriggerStartCollideSprintingComponent : Component
{
    /// <summary>
    /// The trigger group to add when entity is sprinting.
    /// </summary>
    [DataField]
    public string SprintingTriggerGroup = "Sprinting";
}
