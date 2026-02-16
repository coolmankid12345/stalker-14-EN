using Content.Shared._Stalker.ZoneAnomaly.Components;
using Content.Shared._Stalker_EN.ZoneAnomaly.Effects.Components;
using Content.Shared.Actions.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.CartridgeLoader;
using Content.Shared.Clothing.Components;
using Content.Shared.Implants.Components;
using Content.Shared.Interaction.Components;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Item;
using Content.Shared.Mind.Components;
using Content.Shared.StatusEffectNew.Components;
using Content.Shared.Throwing;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Random;

namespace Content.Server._Stalker_EN.ZoneAnomaly.Effects.Systems;

/// <summary>
/// Throws items instead of deleting them, giving players a chance to recover their belongings.
/// </summary>
public sealed class ZoneAnomalyEffectThrowItemSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ZoneAnomalyEffectThrowItemComponent, ZoneAnomalyActivateEvent>(OnActivate);
    }

    private void OnActivate(Entity<ZoneAnomalyEffectThrowItemComponent> effect, ref ZoneAnomalyActivateEvent args)
    {
        foreach (var trigger in args.Triggers)
        {
            if (!HasComp<ContainerManagerComponent>(trigger))
                continue;

            var items = GetRecursiveContainerElements(trigger);
            items.Remove(trigger);

            if (items.Count == 0)
                continue;

            for (var i = 0; i < effect.Comp.Count; i++)
            {
                if (items.Count == 0)
                    break;

                var item = _random.Pick(items);
                items.Remove(item);

                _container.TryRemoveFromContainer(item, force: true);
                _transform.DropNextTo(item, trigger);

                var angle = _random.NextAngle();
                var direction = angle.ToVec();

                _throwing.TryThrow(item, direction * effect.Comp.Distance, effect.Comp.Force);
            }
        }
    }

    /// <summary>
    /// Filters combine upstream anomaly and repository systems to avoid stealing non-removable internals.
    /// </summary>
    private List<EntityUid> GetRecursiveContainerElements(
        EntityUid uid,
        ContainerManagerComponent? managerComponent = null)
    {
        var result = new List<EntityUid>();

        if (!Resolve(uid, ref managerComponent))
            return result;

        foreach (var container in managerComponent.Containers)
        {
            if (container.Key == "toggleable-clothing")
                continue;

            foreach (var element in container.Value.ContainedEntities)
            {
                if (HasComp<OrganComponent>(element) ||
                    HasComp<InstantActionComponent>(element) ||
                    HasComp<WorldTargetActionComponent>(element) ||
                    HasComp<EntityTargetActionComponent>(element) ||
                    HasComp<SubdermalImplantComponent>(element) ||
                    HasComp<BodyPartComponent>(element) ||
                    HasComp<CartridgeComponent>(element) ||
                    HasComp<VirtualItemComponent>(element) ||
                    HasComp<MindContainerComponent>(element) ||
                    HasComp<StatusEffectComponent>(element) ||
                    HasComp<UnremoveableComponent>(element) ||
                    HasComp<SelfUnremovableClothingComponent>(element) ||
                    HasComp<BloodstreamComponent>(element) ||
                    !HasComp<ItemComponent>(element))
                    continue;

                if (TryComp<ContainerManagerComponent>(element, out var manager))
                    result.AddRange(GetRecursiveContainerElements(element, manager));

                result.Add(element);
            }
        }

        return result;
    }
}
