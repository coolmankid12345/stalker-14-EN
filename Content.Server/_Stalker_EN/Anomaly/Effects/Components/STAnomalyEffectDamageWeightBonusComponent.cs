using Content.Shared.Damage;

namespace Content.Server._Stalker_EN.Anomaly.Effects.Components;

/// <summary>
/// Applies additional damage based on entity weight.
/// Damage scales from WeightThreshold to WeightCap.
/// </summary>
[RegisterComponent]
public sealed partial class STAnomalyEffectDamageWeightBonusComponent : Component
{
    /// <summary>
    /// Weight threshold where bonus damage starts (kg).
    /// </summary>
    [DataField]
    public float WeightThreshold = 100f;

    /// <summary>
    /// Weight where bonus damage caps (kg).
    /// </summary>
    [DataField]
    public float WeightCap = 190f;

    /// <summary>
    /// Bonus multiplier per 10kg over threshold (0.1 = 10%).
    /// </summary>
    [DataField]
    public float BonusPerTenKg = 0.1f;

    /// <summary>
    /// Maximum bonus multiplier (1.0 = 100% extra damage).
    /// </summary>
    [DataField]
    public float MaxBonus = 1.0f;

    /// <summary>
    /// Options mapping trigger groups to base damage for weight bonus calculation.
    /// </summary>
    [DataField]
    public Dictionary<string, STAnomalyDamageWeightBonusOptions> Options = new();
}

/// <summary>
/// Configuration for weight-based damage bonus per trigger group.
/// </summary>
[Serializable, DataDefinition]
public partial struct STAnomalyDamageWeightBonusOptions
{
    /// <summary>
    /// Base damage to apply weight bonus to.
    /// </summary>
    [DataField]
    public DamageSpecifier Damage;

    /// <summary>
    /// Range to search for entities.
    /// </summary>
    [DataField]
    public float Range = 1f;
}
