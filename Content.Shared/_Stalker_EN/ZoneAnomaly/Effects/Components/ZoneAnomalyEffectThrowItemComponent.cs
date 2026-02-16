namespace Content.Shared._Stalker_EN.ZoneAnomaly.Effects.Components;

/// <summary>
/// Steals items from a triggered entity's containers and throws them in random directions.
/// Replaces the upstream remove-item effect to give players a chance to recover their belongings.
/// </summary>
[RegisterComponent]
public sealed partial class ZoneAnomalyEffectThrowItemComponent : Component
{
    /// <summary>
    /// Number of items to steal per activation, per trigger entity.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int Count = 1;

    /// <summary>
    /// Direction vector multiplier controlling how far items are thrown.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float Distance = 5f;

    /// <summary>
    /// Throw speed/force applied to stolen items.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float Force = 10f;
}
