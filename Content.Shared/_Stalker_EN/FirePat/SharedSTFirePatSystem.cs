using Content.Shared.Atmos;
using Content.Shared.Interaction;

namespace Content.Shared._Stalker_EN.FirePat;

/// <summary>
/// Suppresses the interaction popup (hug) on both client and server when patting
/// a burning entity, then delegates actual fire patting to the server override.
/// Must be subclassed on both client and server so the handler runs on both sides.
/// </summary>
public abstract class SharedSTFirePatSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();

        // InteractionPopupSystem is shared and uses prediction — it sets Handled = true
        // on both client and server for alive mobs. We must run before it on both sides
        // to suppress the hug popup when the target is on fire.
        SubscribeLocalEvent<STFirePattableComponent, InteractHandEvent>(OnInteractHand,
            before: [typeof(InteractionPopupSystem)]);
    }

    private void OnInteractHand(Entity<STFirePattableComponent> entity, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        if (args.User == args.Target)
            return;

        if (!HasComp<STFirePatterComponent>(args.User))
            return;

        if (!_appearance.TryGetData<bool>(args.Target, FireVisuals.OnFire, out var onFire) || !onFire)
            return;

        args.Handled = true;
        HandleFirePat(args.User, args.Target);
    }

    protected virtual void HandleFirePat(EntityUid user, EntityUid target)
    {
    }
}
