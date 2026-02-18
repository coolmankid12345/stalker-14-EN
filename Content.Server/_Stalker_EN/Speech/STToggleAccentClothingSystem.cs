using Content.Server.Speech.Components;
using Content.Shared._Stalker_EN.Speech;
using Content.Shared.Clothing;

namespace Content.Server._Stalker_EN.Speech;

/// <summary>
///     Server-side handler for toggling accent clothing on/off.
/// </summary>
public sealed class STToggleAccentClothingSystem : SharedSTToggleAccentClothingSystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STToggleableAccentClothingComponent, STToggleAccentClothingEvent>(OnToggleAccent);
        SubscribeLocalEvent<STToggleableAccentClothingComponent, ClothingGotEquippedEvent>(OnGotEquipped);
    }

    /// <summary>
    ///     When the hat is re-equipped and the accent toggle is disabled,
    ///     undo the accent that <see cref="AddAccentClothingSystem"/> just applied.
    /// </summary>
    private void OnGotEquipped(EntityUid uid, STToggleableAccentClothingComponent component,
        ref ClothingGotEquippedEvent args)
    {
        if (component.Enabled)
            return;

        if (!TryComp<AddAccentClothingComponent>(uid, out var accentClothing))
            return;

        if (!accentClothing.IsActive)
            return;

        var componentType = Factory.GetRegistration(accentClothing.Accent).Type;
        RemComp(args.Wearer, componentType);
        accentClothing.IsActive = false;
    }

    private void OnToggleAccent(EntityUid uid, STToggleableAccentClothingComponent component,
        STToggleAccentClothingEvent args)
    {
        if (!TryComp<AddAccentClothingComponent>(uid, out var accentClothing))
            return;

        var wearer = Transform(uid).ParentUid;
        if (args.Performer != wearer)
            return;

        var componentType = Factory.GetRegistration(accentClothing.Accent).Type;

        if (component.Enabled)
        {
            RemComp(wearer, componentType);
            accentClothing.IsActive = false;
            component.Enabled = false;
        }
        else
        {
            if (HasComp(wearer, componentType))
                return;

            var accentComp = (Component) Factory.GetComponent(componentType);
            AddComp(wearer, accentComp);

            if (accentComp is ReplacementAccentComponent rep)
                rep.Accent = accentClothing.ReplacementPrototype!;

            accentClothing.IsActive = true;
            component.Enabled = true;
        }

        Dirty(uid, component);
    }
}
