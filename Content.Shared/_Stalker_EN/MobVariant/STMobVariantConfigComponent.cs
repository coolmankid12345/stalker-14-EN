using Content.Shared._Stalker_EN.Trophy;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stalker_EN.MobVariant;

/// <summary>
/// Configuration component placed on specific mob species prototypes to define
/// possible variant spawns with stat multipliers, sprite modifications, and loot swaps.
/// </summary>
[RegisterComponent]
public sealed partial class STMobVariantConfigComponent : Component
{
    /// <summary>
    /// Ordered list of variant entries. Evaluated top-to-bottom with cumulative probability.
    /// </summary>
    [DataField]
    public List<STVariantEntry> Variants = new();
}

/// <summary>
/// Defines a single variant tier with its spawn chance, stat multipliers,
/// visual overrides, and butcherable loot swaps.
/// </summary>
[DataDefinition]
public sealed partial class STVariantEntry
{
    /// <summary>
    /// Independent probability of this variant being selected (0.0 to 1.0).
    /// </summary>
    [DataField]
    public float Chance;

    /// <summary>
    /// The trophy quality tier assigned to this variant.
    /// </summary>
    [DataField]
    public STTrophyQuality Quality;

    /// <summary>
    /// Localization key for the variant's display name override.
    /// </summary>
    [DataField]
    public LocId? NameOverride;

    /// <summary>
    /// Multiplier applied to all HP thresholds (MobThresholds, Destructible, SlowOnDamage).
    /// </summary>
    [DataField]
    public float HealthMultiplier = 1f;

    /// <summary>
    /// Multiplier applied to MeleeWeapon damage values.
    /// </summary>
    [DataField]
    public float DamageMultiplier = 1f;

    /// <summary>
    /// Uniform scale factor applied to the entity's sprite.
    /// </summary>
    [DataField]
    public float SpriteScale = 1f;

    /// <summary>
    /// Optional color tint applied to all sprite layers via the STMobTint shader.
    /// </summary>
    [DataField]
    public Color? SpriteTint;

    /// <summary>
    /// Saturation multiplier for the STMobTint shader (0=greyscale, 1=normal, >1=vivid).
    /// </summary>
    [DataField]
    public float SpriteSaturation = 1f;

    /// <summary>
    /// Brightness multiplier for the STMobTint shader (0=black, 1=normal, >1=bright).
    /// </summary>
    [DataField]
    public float SpriteBrightness = 1f;

    /// <summary>
    /// Maps base butcherable part prototype IDs to variant part prototype IDs.
    /// </summary>
    [DataField]
    public Dictionary<EntProtoId, EntProtoId> ButcherSwaps = new();

    /// <summary>
    /// Optional override for minimum weight multiplier. If null, uses STMobWeightVarianceComponent default.
    /// </summary>
    [DataField]
    public float? MinWeightMultiplier;

    /// <summary>
    /// Optional override for maximum weight multiplier. If null, uses STMobWeightVarianceComponent default.
    /// </summary>
    [DataField]
    public float? MaxWeightMultiplier;
}
