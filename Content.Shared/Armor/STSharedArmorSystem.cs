using Content.Shared.Damage;

namespace Content.Shared.Armor;

public abstract partial class SharedArmorSystem : EntitySystem
{
    public void OnArmorMapInit(EntityUid uid, ArmorComponent component, MapInitEvent args)
    {
        ApplyLevels(component);
    }

    // stalker-en-changes-start
    /// <summary>
    /// Computes effective damage modifiers by applying armor level adjustments to the base modifiers.
    /// If no armor levels are defined, copies the base modifiers directly.
    /// </summary>
    public void ApplyLevels(ArmorComponent component)
    {
        if (component.STArmorLevels != null)
        {
            component.Modifiers = component.STArmorLevels.ApplyLevels(component.BaseModifiers);
        }
        else
        {
            component.Modifiers = new DamageModifierSet
            {
                Coefficients = new Dictionary<string, float>(component.BaseModifiers.Coefficients),
                FlatReduction = new Dictionary<string, float>(component.BaseModifiers.FlatReduction),
            };
        }
    }
    // stalker-en-changes-end
}
