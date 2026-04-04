using Content.Shared._Stalker.Weapons.Ranged;
using Content.Shared.Clothing.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Inventory;
using Content.Shared.Silicons.Borgs;
using Content.Shared.Verbs;
using Robust.Shared.Utility;
using System.Linq;
using Robust.Shared.Containers;
using Content.Shared.Tag;
using Content.Shared._Stalker_EN.Clothing;
using Content.Shared._Stalker_EN.Clothing.Components;
using Content.Shared.FixedPoint;

namespace Content.Shared.Armor;

/// <summary>
///     This handles logic relating to <see cref="ArmorComponent" />
/// </summary>
public abstract partial class SharedArmorSystem : EntitySystem
{
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    /// <inheritdoc />
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ArmorComponent, MapInitEvent>(OnArmorMapInit);
        SubscribeLocalEvent<ArmorComponent, InventoryRelayedEvent<CoefficientQueryEvent>>(OnCoefficientQuery);
        SubscribeLocalEvent<ArmorComponent, InventoryRelayedEvent<DamageModifyEvent>>(OnDamageModify);
        SubscribeLocalEvent<ArmorComponent, BorgModuleRelayedEvent<DamageModifyEvent>>(OnBorgDamageModify);
        SubscribeLocalEvent<ArmorComponent, GetVerbsEvent<ExamineVerb>>(OnArmorVerbExamine);
        SubscribeLocalEvent<ArmorComponent, VisorToggledEvent>(OnVisorToggled);
    }

    private void OnCoefficientQuery(Entity<ArmorComponent> ent, ref InventoryRelayedEvent<CoefficientQueryEvent> args)
    {
        if (TryComp<MaskComponent>(ent, out var mask) && mask.IsToggled)
            return;

        if (!IsArtifactAllowed(ent.Owner))
            return;

        if (ent.Comp.Modifiers == null)
            return;

        foreach (var armorCoefficient in ent.Comp.Modifiers.Coefficients)
        {
            args.Args.DamageModifiers.Coefficients[armorCoefficient.Key] =
                args.Args.DamageModifiers.Coefficients.TryGetValue(armorCoefficient.Key, out var coefficient)
                    ? coefficient * armorCoefficient.Value
                    : armorCoefficient.Value;
        }
    }

    private void OnDamageModify(EntityUid uid, ArmorComponent component, InventoryRelayedEvent<DamageModifyEvent> args)
    {
        if (TryComp<MaskComponent>(uid, out var mask) && mask.IsToggled)
            return;

        if (!IsArtifactAllowed(uid))
            return;

        if (component.Modifiers == null)
            return;

        // Legacy binary bypass: this armor piece's coefficients are fully ignored.
        if (args.Args.IgnoreResistors.Contains(uid))
        {
            var modifiedModifiers = new DamageModifierSet
            {
                Coefficients = new Dictionary<string, float>(component.Modifiers.Coefficients),
                FlatReduction = new Dictionary<string, float>(component.Modifiers.FlatReduction)
            };
            foreach (var key in modifiedModifiers.Coefficients.Keys.ToList())
                modifiedModifiers.Coefficients[key] = 1f;
            args.Args.Damage = DamageSpecifier.ApplyModifierSet(args.Args.Damage, modifiedModifiers);
            return;
        }

        // NIJ penetration system: walk up the parent chain to find the entity with
        // STIncomingPenetrationComponent (placed by OnProjectileHit before TryChangeDamage).
        // Armor pieces are inside inventory containers so we must walk until we find it.
        var wearer = EntityUid.Invalid;
        var current = Transform(uid).ParentUid;
        while (current.IsValid())
        {
            if (TryComp<STIncomingPenetrationComponent>(current, out _))
            {
                wearer = current;
                break;
            }
            current = Transform(current).ParentUid;
        }

        if (wearer.IsValid() && TryComp<STIncomingPenetrationComponent>(wearer, out var incomingPen))
        {
            var armorClass = component.ArmorClass ?? 0;
            var piercingBypass = incomingPen.CalculatePiercingBypass(armorClass);
            var bluntBypass = incomingPen.CalculateBluntBypass(armorClass);

            // Build weakened modifier set:
            // LOW bypass = armor effective (bullet stopped).
            // HIGH bypass = armor bypassed (bullet penetrates).
            // Piercing and Blunt are affected by penetration; all other types use full modifiers.
            var weakened = new DamageModifierSet
            {
                Coefficients = new Dictionary<string, float>(),
                FlatReduction = new Dictionary<string, float>()
            };

            foreach (var (type, coeff) in component.Modifiers.Coefficients)
            {
                weakened.Coefficients[type] = type switch
                {
                    "Piercing" => coeff + (1f - coeff) * piercingBypass,
                    "Blunt"    => coeff + (1f - coeff) * bluntBypass,
                    _          => coeff
                };
            }

            foreach (var (type, flat) in component.Modifiers.FlatReduction)
            {
                weakened.FlatReduction[type] = type switch
                {
                    "Piercing" => flat * (1f - piercingBypass),
                    "Blunt"    => flat * (1f - bluntBypass),
                    _          => flat
                };
            }

            args.Args.Damage = DamageSpecifier.ApplyModifierSet(args.Args.Damage, weakened);

            // Minimum damage floor: armor can never reduce a bullet hit below 5% of the
            // original incoming damage. Prevents armor from completely negating hits.
            var originalTotal = (float) args.Args.OriginalDamage.GetTotal();
            var currentTotal  = (float) args.Args.Damage.GetTotal();
            var minDamage = originalTotal * 0.05f;

            if (originalTotal > 0f && currentTotal < minDamage)
            {
                var scale = minDamage / currentTotal;
                foreach (var key in args.Args.Damage.DamageDict.Keys.ToList())
                    args.Args.Damage.DamageDict[key] *= (FixedPoint2) scale;
            }

            return;
        }

        // Default: no penetration component present, apply full armor modifiers.
        args.Args.Damage = DamageSpecifier.ApplyModifierSet(args.Args.Damage, component.Modifiers);
    }

    private void OnBorgDamageModify(EntityUid uid, ArmorComponent component,
        ref BorgModuleRelayedEvent<DamageModifyEvent> args)
    {
        if (TryComp<MaskComponent>(uid, out var mask) && mask.IsToggled)
            return;

        if (!IsArtifactAllowed(uid))
            return;

        if (args.Args.IgnoreResistors.Contains(uid))
        {
            if (component.Modifiers == null)
                return;

            var modifiedModifiers = new DamageModifierSet
            {
                Coefficients = new Dictionary<string, float>(component.Modifiers.Coefficients),
                FlatReduction = component.Modifiers.FlatReduction
            };
            foreach (var key in modifiedModifiers.Coefficients.Keys.ToList())
                modifiedModifiers.Coefficients[key] = 1f;

            args.Args.Damage = DamageSpecifier.ApplyModifierSet(args.Args.Damage, modifiedModifiers);
            return;
        }

        if (component.Modifiers == null)
            return;

        args.Args.Damage = DamageSpecifier.ApplyModifierSet(args.Args.Damage, component.Modifiers);
    }

    private void OnArmorVerbExamine(EntityUid uid, ArmorComponent component, GetVerbsEvent<ExamineVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess || !component.ShowArmorOnExamine || component.Hidden || component.HiddenExamine)
            return;

        var examineMarkup = GetArmorExamine(component.Modifiers ?? component.BaseModifiers, component);

        var ev = new ArmorExamineEvent(examineMarkup);
        RaiseLocalEvent(uid, ref ev);

        _examine.AddDetailedExamineVerb(args, component, examineMarkup,
            Loc.GetString("armor-examinable-verb-text"), "/Textures/Interface/VerbIcons/dot.svg.192dpi.png",
            Loc.GetString("armor-examinable-verb-message"));
    }

    private FormattedMessage GetArmorExamine(DamageModifierSet armorModifiers, ArmorComponent comp)
    {
        var msg = new FormattedMessage();
        msg.AddMarkupOrThrow(Loc.GetString("armor-examine"));

        msg.PushNewline();
        msg.AddMarkup(Loc.GetString("armor-class-value", ("value", comp.ArmorClass ?? 0)));

        foreach (var coefficientArmor in armorModifiers.Coefficients)
        {
            msg.PushNewline();
            var armorType = Loc.GetString("armor-damage-type-" + coefficientArmor.Key.ToLower());
            msg.AddMarkupOrThrow(Loc.GetString("armor-coefficient-value",
                ("type", armorType),
                ("value", MathF.Round((1f - coefficientArmor.Value) * 100, 1))
            ));
        }

        foreach (var flatArmor in armorModifiers.FlatReduction)
        {
            msg.PushNewline();
            var armorType = Loc.GetString("armor-damage-type-" + flatArmor.Key.ToLower());
            msg.AddMarkupOrThrow(Loc.GetString("armor-reduction-value",
                ("type", armorType),
                ("value", flatArmor.Value)
            ));
        }

        return msg;
    }

    private bool IsArtifactAllowed(EntityUid uid)
    {
        if (!TryComp<TagComponent>(uid, out var tagComp) || !_tag.HasTag(tagComp, "STArtifact"))
            return true;

        if (!TryComp<TransformComponent>(uid, out var xform) || !TryComp<MetaDataComponent>(uid, out var meta))
            return false;

        if (!_inventory.TryGetContainingSlot((uid, xform, meta), out var slotDef) || slotDef == null)
            return false;

        var name = slotDef.Name;
        return name == "artifact1" || name == "artifact2" || name == "artifact3" || name == "artifact4" || name == "artifact5";
    }

    private void OnVisorToggled(Entity<ArmorComponent> ent, ref VisorToggledEvent args)
    {
        if (!TryComp<HelmetVisorComponent>(ent, out var visor) || visor.VisorUpModifiers == null)
            return;

        ent.Comp.Modifiers = args.IsUp ? visor.VisorUpModifiers : visor.DefaultModifiers;
        Dirty(ent);
    }
}
