using System.Linq;
using Content.Shared.Armor;
using Content.Shared.Clothing.Components;
using Content.Shared.Damage;
using Content.Shared.Examine;
using Content.Shared.Inventory;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Reflect;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;

namespace Content.Client.Examine;

/// <summary>
/// System for comparing armor stats between examined items and currently equipped items.
/// Provides a "Compare Stats" verb for items with ArmorComponent and ClothingComponent.
/// </summary>
public sealed class StatsExamineSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly IComponentFactory _componentFactory = default!;

    private StatsExamineWindow? _window;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ClothingComponent, GetVerbsEvent<ExamineVerb>>(OnClothingStatsVerb);
    }

    /// <summary>
    /// Adds a "Compare Stats" verb to items with ArmorComponent and ClothingComponent.
    /// </summary>
    private void OnClothingStatsVerb(EntityUid uid, ClothingComponent component, GetVerbsEvent<ExamineVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        // Only add the compare verb if the item has armor component
        if (!TryComp<ArmorComponent>(uid, out var armor))
            return;

        if (armor.Hidden || armor.HiddenExamine)
            return;

        // Create a separate verb for stat comparison
        var verb = new ExamineVerb
        {
            ClientExclusive = true,
            Act = () => OpenStatsWindow(args.User, args.Target, armor, component),
            Text = "Compare Stats",
            Message = "Compare armor stats with currently equipped item",
            Category = VerbCategory.Examine,
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/dot.svg.192dpi.png")),
            Priority = -1 // Show after the standard armor examine verb
        };

        args.Verbs.Add(verb);
    }

    /// <summary>
    /// Opens the stats comparison window for a spawned entity.
    /// Compares the examined item's stats with the equipped item in the same slot.
    /// </summary>
    private void OpenStatsWindow(EntityUid user, EntityUid target, ArmorComponent examinedArmor, ClothingComponent examinedClothing)
    {
        _window?.Close();
        _window = new StatsExamineWindow();
        _window.OpenCentered();

        // Get the examined item's modifiers
        var examinedModifiers = examinedArmor.Modifiers ?? examinedArmor.BaseModifiers;
        var examinedArmorClass = examinedArmor.ArmorClass;

        // Get reflect chance if available
        float? examinedReflectProb = null;
        if (TryComp<ReflectComponent>(target, out var reflect))
        {
            examinedReflectProb = reflect.ReflectProb;
        }

        // Get the equipped item in the same slot
        TryGetEquippedArmorStats(user, examinedClothing.Slots, out var equippedModifiers, out var equippedArmorClass, out var equippedReflectProb);

        _window.UpdateStats(examinedModifiers, examinedArmorClass, examinedReflectProb, equippedModifiers, equippedArmorClass, equippedReflectProb);
    }

    /// <summary>
    /// Opens the stats comparison window for a prototype (shop item).
    /// Compares the prototype's stats with the equipped item in the same slot.
    /// </summary>
    public void OpenStatsWindowFromPrototype(EntityUid user, string prototypeId)
    {
        _window?.Close();
        _window = new StatsExamineWindow();
        _window.OpenCentered();

        // Get the prototype
        if (!_prototypeManager.TryIndex<EntityPrototype>(prototypeId, out var prototype))
            return;

        // Get components from prototype
        DamageModifierSet? examinedModifiers = null;
        int? examinedArmorClass = null;
        float? examinedReflectProb = null;
        SlotFlags? slotFlags = null;

        if (prototype.TryGetComponent<ArmorComponent>(out var armor, _componentFactory))
        {
            examinedModifiers = armor.Modifiers ?? armor.BaseModifiers;
            examinedArmorClass = armor.ArmorClass;
        }

        if (prototype.TryGetComponent<ReflectComponent>(out var reflect, _componentFactory))
        {
            examinedReflectProb = reflect.ReflectProb;
        }

        if (prototype.TryGetComponent<ClothingComponent>(out var clothing, _componentFactory))
        {
            slotFlags = clothing.Slots;
        }

        // Get the equipped item in the same slot
        DamageModifierSet? equippedModifiers = null;
        int? equippedArmorClass = null;
        float? equippedReflectProb = null;

        if (slotFlags.HasValue)
        {
            TryGetEquippedArmorStats(user, slotFlags.Value, out equippedModifiers, out equippedArmorClass, out equippedReflectProb);
        }

        _window.UpdateStats(examinedModifiers ?? new DamageModifierSet(), examinedArmorClass, examinedReflectProb, equippedModifiers, equippedArmorClass, equippedReflectProb);
    }

    /// <summary>
    /// Gets the stats of the equipped item in the specified slot.
    /// Uses exact flag matching first, then falls back to partial matching.
    /// </summary>
    private void TryGetEquippedArmorStats(
        EntityUid user,
        SlotFlags slotFlags,
        out DamageModifierSet? modifiers,
        out int? armorClass,
        out float? reflectProb)
    {
        modifiers = null;
        armorClass = null;
        reflectProb = null;

        if (!TryComp<InventoryComponent>(user, out var inventory))
            return;

        // First try to find exact match (slot flags exactly match item flags)
        foreach (var slotDef in inventory.Slots)
        {
            if (slotDef.SlotFlags == slotFlags)
            {
                if (_inventory.TryGetSlotEntity(user, slotDef.Name, out var equippedUid))
                {
                    if (TryComp<ArmorComponent>(equippedUid, out var equippedArmor))
                    {
                        modifiers = equippedArmor.Modifiers ?? equippedArmor.BaseModifiers;
                        armorClass = equippedArmor.ArmorClass;
                    }

                    if (TryComp<ReflectComponent>(equippedUid, out var equippedReflect))
                    {
                        reflectProb = equippedReflect.ReflectProb;
                    }

                    break;
                }
            }
        }

        // If no exact match found, try partial match (slot contains all item flags)
        if (modifiers == null)
        {
            foreach (var slotDef in inventory.Slots)
            {
                if ((slotDef.SlotFlags & slotFlags) == slotFlags)
                {
                    if (_inventory.TryGetSlotEntity(user, slotDef.Name, out var equippedUid))
                    {
                        if (TryComp<ArmorComponent>(equippedUid, out var equippedArmor))
                        {
                            modifiers = equippedArmor.Modifiers ?? equippedArmor.BaseModifiers;
                            armorClass = equippedArmor.ArmorClass;
                        }

                        if (TryComp<ReflectComponent>(equippedUid, out var equippedReflect))
                        {
                            reflectProb = equippedReflect.ReflectProb;
                        }

                        break;
                    }
                }
            }
        }
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _window?.Close();
    }
}
