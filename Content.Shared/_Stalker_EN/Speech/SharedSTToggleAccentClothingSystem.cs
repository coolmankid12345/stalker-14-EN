using Content.Shared.Inventory;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Shared._Stalker_EN.Speech;

/// <summary>
///     Shared system that generates the equipment verb for toggling accent clothing.
/// </summary>
public abstract class SharedSTToggleAccentClothingSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STToggleableAccentClothingComponent, InventoryRelayedEvent<GetVerbsEvent<EquipmentVerb>>>(OnRelayedGetVerbs);
        SubscribeLocalEvent<STToggleableAccentClothingComponent, GetVerbsEvent<EquipmentVerb>>(OnGetVerbs);
    }

    private void OnRelayedGetVerbs(EntityUid uid, STToggleableAccentClothingComponent component,
        InventoryRelayedEvent<GetVerbsEvent<EquipmentVerb>> args)
    {
        OnGetVerbs(uid, component, args.Args);
    }

    private void OnGetVerbs(EntityUid uid, STToggleableAccentClothingComponent component,
        GetVerbsEvent<EquipmentVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        var wearer = Transform(uid).ParentUid;
        if (args.User != wearer)
            return;

        var text = component.Enabled
            ? Loc.GetString("toggle-accent-clothing-disable")
            : Loc.GetString("toggle-accent-clothing-enable");

        var verb = new EquipmentVerb
        {
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/bubbles.svg.192dpi.png")),
            Text = text,
            EventTarget = uid,
            ExecutionEventArgs = new STToggleAccentClothingEvent { Performer = GetNetEntity(args.User) },
        };

        args.Verbs.Add(verb);
    }
}
