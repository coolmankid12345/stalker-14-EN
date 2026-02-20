using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Stalker_EN.Bloodstream;

/// <summary>
/// Reduces bleed stacks when an entity is healed of damage types that originally cause bleeding.
/// Complements <see cref="SharedBloodstreamSystem"/> which only adds bleed from positive damage
/// but never reduces it from healing.
/// </summary>
/// <remarks>
/// Subscribes on <see cref="DamageableComponent"/> instead of <see cref="BloodstreamComponent"/>
/// because RobustToolbox only allows one subscription per (Component, Event) pair, and
/// <see cref="SharedBloodstreamSystem"/> already owns (BloodstreamComponent, DamageChangedEvent).
/// </remarks>
public sealed class STHealingBleedReductionSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedBloodstreamSystem _bloodstream = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DamageableComponent, DamageChangedEvent>(OnDamageChanged);
    }

    private void OnDamageChanged(Entity<DamageableComponent> ent, ref DamageChangedEvent args)
    {
        if (_timing.ApplyingState)
            return;

        if (args.DamageDelta is null)
            return;

        if (!TryComp<BloodstreamComponent>(ent, out var bloodstream))
            return;

        if (bloodstream.BleedAmount <= 0)
            return;

        var healing = DamageSpecifier.GetNegative(args.DamageDelta);
        if (healing.Empty)
            return;

        if (!_proto.TryIndex(bloodstream.DamageBleedModifiers, out var modifiers))
            return;

        var bleedReduction = CalculateBleedReduction(healing, modifiers);

        if (bleedReduction >= 0f)
            return;

        _bloodstream.TryModifyBleedAmount((ent.Owner, bloodstream), bleedReduction);
    }

    /// <summary>
    /// Calculates bleed reduction from healing amounts using the bleed modifier set coefficients.
    /// Unlisted damage types default to coefficient 1.0 (matching implicit passthrough when adding bleed).
    /// Negative coefficients (e.g. Heat -0.5 for cauterization) are skipped to avoid un-cauterizing wounds.
    /// </summary>
    private static float CalculateBleedReduction(DamageSpecifier healing, DamageModifierSet modifiers)
    {
        var totalReduction = 0f;

        foreach (var (damageType, healValue) in healing.DamageDict)
        {
            if (healValue >= FixedPoint2.Zero)
                continue;

            float coefficient;
            if (modifiers.Coefficients.TryGetValue(damageType, out var coeff))
            {
                if (coeff <= 0f)
                    continue;

                coefficient = coeff;
            }
            else
            {
                coefficient = 1.0f;
            }

            totalReduction += healValue.Float() * coefficient;
        }

        return totalReduction;
    }
}
