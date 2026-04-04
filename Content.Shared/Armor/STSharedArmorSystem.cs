using Content.Shared.Damage;

namespace Content.Shared.Armor;

public abstract partial class SharedArmorSystem : EntitySystem
{
    public void OnArmorMapInit(EntityUid uid, ArmorComponent component, MapInitEvent args)
    {
        ApplyLevels(component);
    }

    /// <summary>
    /// Sets component.Modifiers from BaseModifiers.
    /// YAML modifiers are the final balanced values — no scaling is applied on top.
    /// ArmorClass only controls bullet penetration bypass in OnDamageModify.
    /// STArmorLevels is still supported for environmental/radiation adjustments
    /// if present, but physical damage types are NOT scaled by it.
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
}
