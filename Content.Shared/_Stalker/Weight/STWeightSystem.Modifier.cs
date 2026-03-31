using Content.Shared._Stalker.Modifier;
using Content.Shared._Stalker.Weight.Modifier;
using Content.Shared._Stalker_EN.Weight.Modifier;

namespace Content.Shared._Stalker.Weight;

public sealed partial class STWeightSystemModifier : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<STWeightComponent, UpdatedFloatModifierEvent<STWeightMaximumModifierComponent>>(OnUpdatedMaximum);
        SubscribeLocalEvent<STWeightComponent, UpdatedFloatModifierEvent<STWeightSelfModifierComponent>>(OnUpdatedSelf);
    }

    private void OnUpdatedMaximum(Entity<STWeightComponent> weight, ref UpdatedFloatModifierEvent<STWeightMaximumModifierComponent> args)
    {
        weight.Comp.MaximumModifier = args.Modifier;
        Dirty(weight.Owner, weight.Comp);
    }

    private void OnUpdatedSelf(Entity<STWeightComponent> weight, ref UpdatedFloatModifierEvent<STWeightSelfModifierComponent> args)
    {
        weight.Comp.Self += args.Modifier;
        Dirty(weight.Owner, weight.Comp);
    }
}
