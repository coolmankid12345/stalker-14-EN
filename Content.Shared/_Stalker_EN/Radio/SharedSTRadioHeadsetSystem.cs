using Content.Shared.Actions;
using Content.Shared.Inventory;

namespace Content.Shared._Stalker_EN.Radio;

/// <summary>
/// Shared system for stalker radio headsets that handles action spawning and granting.
/// Speaker is always active when equipped (no toggle needed) - messages go directly to wearer.
/// </summary>
public abstract class SharedSTRadioHeadsetSystem : EntitySystem
{
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STRadioHeadsetComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<STRadioHeadsetComponent, GetItemActionsEvent>(OnGetItemActions);
    }

    private void OnMapInit(Entity<STRadioHeadsetComponent> ent, ref MapInitEvent args)
    {
        _actionContainer.EnsureAction(ent, ref ent.Comp.ToggleMicActionEntity, ent.Comp.ToggleMicAction);
        Dirty(ent);
    }

    private void OnGetItemActions(Entity<STRadioHeadsetComponent> ent, ref GetItemActionsEvent args)
    {
        // Only grant actions when equipped to ears slot (SlotFlags is null when in hands)
        if (args.SlotFlags is not { } flags || !flags.HasFlag(SlotFlags.EARS))
            return;

        args.AddAction(ent.Comp.ToggleMicActionEntity);
    }
}
