namespace Content.Shared._Stalker_EN.MobVariant;

/// <summary>
/// When present on a mob, randomizes the entity's weight at MapInit within
/// a multiplier range. Heavier mobs are visually bigger via proportional sprite scaling.
/// </summary>
[RegisterComponent]
public sealed partial class STMobWeightVarianceComponent : Component
{
    /// <summary>
    /// Minimum weight multiplier applied to STWeightComponent.Self at spawn.
    /// </summary>
    [DataField]
    public float MinWeightMultiplier = 0.85f;

    /// <summary>
    /// Maximum weight multiplier applied to STWeightComponent.Self at spawn.
    /// </summary>
    [DataField]
    public float MaxWeightMultiplier = 1.15f;
}
