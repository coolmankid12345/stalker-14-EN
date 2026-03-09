using Content.Shared.Damage;
using Robust.Shared.Serialization;

namespace Content.Shared.Armor;

[DataDefinition, Serializable, NetSerializable, Virtual]
public partial class STArmorLevels
{
    // stalker-en-changes-start
    private static readonly string[] NonPvPPhysicalTypes = ["Blunt", "Slash"];
    private static readonly string[] PiercingTypes = ["Piercing"];
    private static readonly string[] RadiationTypes = ["Radiation"];
    private static readonly string[] EnvironmentTypes = ["Heat", "Caustic", "Shock", "Compression", "Psy"];
    private static readonly string[] HeatTypes = ["Heat"];
    private static readonly string[] CausticTypes = ["Caustic"];
    private static readonly string[] ShockTypes = ["Shock"];
    private static readonly string[] PsyTypes = ["Psy"];

    private const float CoefficientAdjustPerLevel = -0.025f;
    private const float FlatReductionScalePerLevel = 0.5f;
    // stalker-en-changes-end

    // Generic
    [DataField("nonPvPPhysical")]
    public int NonPvPPhysicalAdjust = 0;

    [DataField("piercing")]
    public int PiercingAdjust = 0;

    [DataField("radiation")]
    public int RadiationAdjust = 0;

    [DataField("environment")]
    public int EnvironmentAdjust = 0;

    // Exact
    [DataField("heat")]
    public int HeatAdjust = 0;

    [DataField("caustic")]
    public int CausticAdjust = 0;

    [DataField("shock")]
    public int ShockAdjust = 0;

    [DataField("psy")]
    public int PsyAdjust = 0;

    // stalker-en-changes-start
    /// <summary>
    /// Applies armor level adjustments to a base modifier set, returning a new set.
    /// The original <paramref name="baseModifiers"/> is not mutated.
    /// Generic adjustments are applied first, then exact type adjustments stack additively on top.
    /// </summary>
    public DamageModifierSet ApplyLevels(DamageModifierSet baseModifiers)
    {
        var newModifiers = new DamageModifierSet
        {
            Coefficients = new Dictionary<string, float>(baseModifiers.Coefficients),
            FlatReduction = new Dictionary<string, float>(baseModifiers.FlatReduction),
        };

        // Generic adjustments
        ApplyLevelToGroup(newModifiers, NonPvPPhysicalAdjust, NonPvPPhysicalTypes);
        ApplyLevelToGroup(newModifiers, PiercingAdjust, PiercingTypes);
        ApplyLevelToGroup(newModifiers, RadiationAdjust, RadiationTypes);
        ApplyLevelToGroup(newModifiers, EnvironmentAdjust, EnvironmentTypes);

        // Exact adjustments (stack additively on top of generic)
        ApplyLevelToGroup(newModifiers, HeatAdjust, HeatTypes);
        ApplyLevelToGroup(newModifiers, CausticAdjust, CausticTypes);
        ApplyLevelToGroup(newModifiers, ShockAdjust, ShockTypes);
        ApplyLevelToGroup(newModifiers, PsyAdjust, PsyTypes);
        return newModifiers;
    }

    private void ApplyLevelToGroup(DamageModifierSet modifiers, int level, string[] damageTypes)
    {
        if (level == 0)
            return;

        foreach (var damageType in damageTypes)
        {
            if (modifiers.Coefficients.TryGetValue(damageType, out var coefficient))
            {
                modifiers.Coefficients[damageType] = MathF.Round(Math.Clamp(coefficient + (level * CoefficientAdjustPerLevel), 0f, 1f), 2);
            }

            if (modifiers.FlatReduction.TryGetValue(damageType, out var flatReduction))
            {
                modifiers.FlatReduction[damageType] = MathF.Round(flatReduction + flatReduction * (level * FlatReductionScalePerLevel), 2);
            }
        }
    }
    // stalker-en-changes-end
}
