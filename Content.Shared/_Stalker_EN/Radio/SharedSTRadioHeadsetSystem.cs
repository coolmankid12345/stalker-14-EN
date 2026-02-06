using Content.Shared.Actions;
using Content.Shared.Inventory;

namespace Content.Shared._Stalker_EN.Radio;

/// <summary>
/// Shared system for stalker radio headsets that handles action spawning and granting.
/// Speaker is always active when equipped (no toggle needed) - messages go directly to wearer.
/// </summary>
public abstract class SharedSTRadioHeadsetSystem : EntitySystem
{
    [Dependency] protected readonly ActionContainerSystem _actionContainer = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STRadioHeadsetComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<STRadioHeadsetComponent, ComponentRemove>(OnComponentRemove);
        SubscribeLocalEvent<STRadioHeadsetComponent, GetItemActionsEvent>(OnGetItemActions);
    }

    protected virtual void OnMapInit(Entity<STRadioHeadsetComponent> ent, ref MapInitEvent args)
    {
        if (IsActionInvalid(ent.Comp.ToggleMicActionEntity))
            ent.Comp.ToggleMicActionEntity = null;

        _actionContainer.EnsureAction(ent, ref ent.Comp.ToggleMicActionEntity, ent.Comp.ToggleMicAction);
        Dirty(ent);
    }

    private void OnComponentRemove(Entity<STRadioHeadsetComponent> ent, ref ComponentRemove args)
    {
        if (ent.Comp.ToggleMicActionEntity is { } actionEntity && Exists(actionEntity))
            Del(actionEntity);

        ent.Comp.ToggleMicActionEntity = null;
    }

    private void OnGetItemActions(Entity<STRadioHeadsetComponent> ent, ref GetItemActionsEvent args)
    {
        if (args.SlotFlags is not { } flags || !flags.HasFlag(SlotFlags.EARS))
            return;

        // During loadout restore, DeleteChildren() queues the action for deletion before
        // GetItemActionsEvent fires. Must check IsQueuedForDeletion in addition to TerminatingOrDeleted.
        if (IsActionInvalid(ent.Comp.ToggleMicActionEntity))
        {
            ent.Comp.ToggleMicActionEntity = null;
            Dirty(ent);
        }

        args.AddAction(ref ent.Comp.ToggleMicActionEntity, ent.Comp.ToggleMicAction);
    }

    /// <summary>
    /// Checks if the action entity is invalid (null, deleted, terminating, or queued for deletion).
    /// Must check IsQueuedForDeletion because QueueDeleteEntity doesn't change LifeStage immediately.
    /// </summary>
    private bool IsActionInvalid(EntityUid? actionId)
    {
        if (actionId is not { } id)
            return true;

        if (TerminatingOrDeleted(id))
            return true;

        if (EntityManager.IsQueuedForDeletion(id))
            return true;

        return false;
    }
}
