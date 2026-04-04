using Content.Shared.Damage;
using Content.Shared.Inventory;
using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared.Armor;

/// <summary>
/// Used for clothing that reduces damage when worn.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(SharedArmorSystem))]
public sealed partial class ArmorComponent : Component
{
    /// <summary>
    /// The base damage reduction (coefficients and flat reductions from YAML)
    /// </summary>
    [DataField("modifiers", required: true)]
    public DamageModifierSet BaseModifiers = default!;

    /// <summary>
    /// The current damage reduction, after applying armor levels
    /// </summary>
    [ViewVariables]
    public DamageModifierSet? Modifiers = default!;

    /// <summary>
    /// The armor levels that modify the base modifiers
    /// </summary>
    [DataField("armorLevels")]
    public STArmorLevels? STArmorLevels = default!;

    /// <summary>
    /// The armor's penetration class (1-10). Used for bullet penetration calculation.
    /// Higher value = better protection against bullets.
    /// </summary>
    [DataField("armorClass", required: false)]
    public int? ArmorClass;

    [DataField("hidden")]
    public bool Hidden;

    [DataField("hiddenExamine")]
    public bool HiddenExamine;

    /// <summary>
    /// A multiplier applied to the calculated point value to determine the monetary value of the armor
    /// </summary>
    [DataField]
    public float PriceMultiplier = 1;

    /// <summary>
    /// If true, you can examine the armor to see the protection. If false, the verb won't appear.
    /// </summary>
    [DataField]
    public bool ShowArmorOnExamine = true;
}

/// <summary>
/// Event raised on an armor entity to get additional examine text relating to its armor.
/// </summary>
[ByRefEvent]
public record struct ArmorExamineEvent(FormattedMessage Msg);

/// <summary>
/// A Relayed inventory event, gets the total Armor for all Inventory slots defined by the Slotflags in TargetSlots
/// </summary>
public sealed class CoefficientQueryEvent : EntityEventArgs, IInventoryRelayEvent
{
    public SlotFlags TargetSlots { get; set; }
    public DamageModifierSet DamageModifiers { get; set; } = new DamageModifierSet();

    public CoefficientQueryEvent(SlotFlags slots)
    {
        TargetSlots = slots;
    }
}
