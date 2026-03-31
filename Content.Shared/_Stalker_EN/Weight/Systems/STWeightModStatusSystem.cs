using Content.Shared._Stalker.Weight;
using Content.Shared._Stalker_EN.Weight;
using Content.Shared._Stalker_EN.Weight.Modifier;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stalker_EN.Weight.Systems;

/// <summary>
/// System for handling weight modifying status effects.
/// Applies temporary weight modifiers (e.g., -20kg from Hercules reagent) via status effects.
/// Weight is modified when status effect is created and restored when removed.
/// </summary>
public sealed class STWeightModStatusSystem : EntitySystem
{
    [Dependency] private readonly StatusEffectsSystem _status = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<STWeightSelfModifierComponent, StatusEffectRemovedEvent>(OnWeightModRemoved);
    }

    // stalker-en-changes - restores entity weight when status effect is removed
    private void OnWeightModRemoved(Entity<STWeightSelfModifierComponent> ent, ref StatusEffectRemovedEvent args)
    {
        if (TryComp<STWeightComponent>(args.Target, out var weight))
        {
            // Remove the modifier from current Self (add back the negative value)
            weight.Self -= ent.Comp.Modifier;
            Dirty(args.Target, weight);
        }
    }

    // stalker-en-changes - creates/updates status effect and applies weight modifier only on first creation
    public bool TryUpdateWeightModDuration(
        EntityUid uid,
        EntProtoId effectProto,
        TimeSpan? duration,
        float modifier)
    {
        // Check if status effect already exists
        var hasExistingEffect = _status.HasStatusEffect(uid, effectProto);

        // Use TryUpdateStatusEffectDuration to extend existing effect without recreating it
        if (!_status.TryUpdateStatusEffectDuration(uid, effectProto, out var statusEffect, duration ?? TimeSpan.FromSeconds(2)))
            return false;

        if (EntityManager.TryGetComponent<STWeightSelfModifierComponent>(statusEffect, out var comp))
        {
            comp.Modifier = modifier;
            if (statusEffect.HasValue)
                Dirty(statusEffect.Value, comp);
        }

        // Apply weight modifier only when status effect is first created (not on duration updates)
        if (!hasExistingEffect && TryComp<STWeightComponent>(uid, out var weight))
        {
            // stalker-en-changes - apply modifier directly to current Self
            weight.Self += modifier;
            Dirty(uid, weight);
        }

        return true;
    }
}
