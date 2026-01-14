using Content.Server._Stalker_EN.Anomaly.Effects.Components;
using Content.Shared._Stalker.Anomaly.Triggers.Events;
using Content.Shared._Stalker.Weight;
using Content.Shared.Damage;
using Robust.Shared.Physics;

namespace Content.Server._Stalker_EN.Anomaly.Effects.Systems;

/// <summary>
/// Applies additional damage based on entity weight when anomaly triggers.
/// </summary>
public sealed class STAnomalyEffectDamageWeightBonusSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STAnomalyEffectDamageWeightBonusComponent, STAnomalyTriggerEvent>(OnTriggered);
    }

    private void OnTriggered(
        Entity<STAnomalyEffectDamageWeightBonusComponent> effect,
        ref STAnomalyTriggerEvent args)
    {
        foreach (var group in args.Groups)
        {
            if (!effect.Comp.Options.TryGetValue(group, out var options))
                continue;

            var entities =
                _entityLookup.GetEntitiesInRange<STWeightComponent>(Transform(effect).Coordinates, options.Range, LookupFlags.Uncontained);

            foreach (var entity in entities)
            {
                var bonus = CalculateWeightBonus(entity.Comp.Total, effect.Comp);
                if (bonus <= 0)
                    continue;

                // Apply bonus damage (base damage * bonus multiplier)
                var bonusDamage = options.Damage * bonus;
                _damageable.TryChangeDamage(entity, bonusDamage);
            }
        }
    }

    private float CalculateWeightBonus(
        float totalWeight,
        STAnomalyEffectDamageWeightBonusComponent comp)
    {
        if (totalWeight < comp.WeightThreshold)
            return 0f;

        // Calculate bonus: (weight - threshold) / 10 * bonusPerTenKg
        var overWeight = totalWeight - comp.WeightThreshold;
        var bonus = overWeight / 10f * comp.BonusPerTenKg;

        return Math.Min(bonus, comp.MaxBonus);
    }
}
