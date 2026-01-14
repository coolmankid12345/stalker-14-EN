using Content.Shared._Stalker.Anomaly.Triggers.Events;
using Content.Shared.Inventory;
using Content.Shared.Movement.Components;

namespace Content.Server._Stalker_EN.Anomaly.Triggers.StartCollide;

/// <summary>
/// Adds trigger groups based on whether the colliding entity has boots equipped.
/// </summary>
public sealed class STAnomalyTriggerStartCollideBootsSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STAnomalyTriggerStartCollideBootsComponent,
            STAnomalyTriggerStartCollideGetAdditionalGroupsEvent>(OnGetAdditionalGroups);
    }

    private void OnGetAdditionalGroups(
        Entity<STAnomalyTriggerStartCollideBootsComponent> trigger,
        ref STAnomalyTriggerStartCollideGetAdditionalGroupsEvent args)
    {
        // Check if target has boots equipped
        var hasBoots = _inventory.TryGetSlotEntity(args.Target, trigger.Comp.SlotName, out _);

        if (hasBoots)
            return; // Has boots - let sprint system handle it separately

        // No boots - check if sprinting for double damage
        var isSprinting = TryComp<InputMoverComponent>(args.Target, out var mover) && mover.Sprinting;

        if (isSprinting)
            args.Add(trigger.Comp.NoBootsSprintingGroup);
        else
            args.Add(trigger.Comp.NoBootsWalkingGroup);
    }
}
