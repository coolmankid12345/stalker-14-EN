using Content.Shared._Stalker.Anomaly.Triggers.Events;
using Content.Shared.Movement.Components;

namespace Content.Server._Stalker_EN.Anomaly.Triggers.StartCollide;

/// <summary>
/// Adds a trigger group when the colliding entity is sprinting.
/// </summary>
public sealed class STAnomalyTriggerStartCollideSprintingSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STAnomalyTriggerStartCollideSprintingComponent,
            STAnomalyTriggerStartCollideGetAdditionalGroupsEvent>(OnGetAdditionalGroups);
    }

    private void OnGetAdditionalGroups(
        Entity<STAnomalyTriggerStartCollideSprintingComponent> trigger,
        ref STAnomalyTriggerStartCollideGetAdditionalGroupsEvent args)
    {
        // args.Target is the entity that collided with the anomaly
        if (!TryComp<InputMoverComponent>(args.Target, out var mover))
            return;

        // In Stalker, MoveButtons.Walk flag means SPRINTING (inverted from vanilla SS14)
        if (mover.Sprinting)
            args.Add(trigger.Comp.SprintingTriggerGroup);
    }
}
