using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;

namespace Content.Shared._Stalker.Medical.CPR;

/// <summary>
/// Shared handler for CPR's InteractHandEvent. Validates CPR conditions and marks
/// the event as handled on both client and server so the interaction pipeline does
/// not fall through to ActivateInWorldEvent (which would open the stripping UI).
/// Server-side override performs the actual CPR logic (popups, DoAfter, healing).
/// </summary>
public abstract class SharedSTCPRSystem : EntitySystem
{
    [Dependency] private readonly MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STCPRComponent, InteractHandEvent>(OnInteractHand,
            before: [typeof(InteractionPopupSystem)]);
    }

    private void OnInteractHand(EntityUid uid, STCPRComponent comp, InteractHandEvent args)
    {
        if (args.Handled)
            return;

        var user = args.User;
        var target = uid;

        // Can't CPR yourself
        if (user == target)
            return;

        // Target must have MobState and be critical or dead
        if (!TryComp<MobStateComponent>(target, out var mobState))
            return;

        if (_mobState.IsAlive(target, mobState))
            return;

        // User must be alive
        if (TryComp<MobStateComponent>(user, out var userMobState) && !_mobState.IsAlive(user, userMobState))
            return;

        // All validation passed — mark as handled to prevent stripping UI fallthrough.
        args.Handled = true;

        // Server override performs the actual CPR logic.
        AttemptCPR(uid, comp, args);
    }

    /// <summary>
    /// Called on the server after validation passes. Does nothing on the client.
    /// </summary>
    protected virtual void AttemptCPR(EntityUid uid, STCPRComponent comp, InteractHandEvent args)
    {
    }
}
