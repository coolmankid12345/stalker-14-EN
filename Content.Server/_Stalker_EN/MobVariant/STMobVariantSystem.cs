using System.Numerics;
using Content.Server.Destructible;
using Content.Shared._Stalker_EN.MobVariant;
using Content.Shared._Stalker_EN.Trophy;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Server.Destructible.Thresholds.Behaviors;
using Content.Shared.Destructible.Thresholds.Triggers;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Sprite;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Stalker_EN.MobVariant;

/// <summary>
/// Rolls variant upgrades for mobs at MapInit. When a variant is selected,
/// modifies the entity's stats, sprite, name, and butcherable loot in-place
/// to preserve EntityUid for spawner restricted lists and pack references.
/// </summary>
public sealed class STMobVariantSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly SharedScaleVisualsSystem _scaleVisuals = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STMobVariantConfigComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(EntityUid uid, STMobVariantConfigComponent config, MapInitEvent args)
    {
        // If the component already exists (e.g. from YAML for GM force-spawning),
        // apply the matching variant entry instead of rolling randomly.
        if (TryComp<STMobVariantComponent>(uid, out var existing))
        {
            if (existing.Applied)
                return;

            foreach (var entry in config.Variants)
            {
                if (entry.Quality != existing.Quality)
                    continue;

                ApplyVariant(uid, entry, existing);
                break;
            }

            return;
        }

        var variant = RollVariant(config);
        if (variant == null)
            return;

        ApplyVariant(uid, variant);
    }

    /// <summary>
    /// Rolls against cumulative variant chances to select a variant tier.
    /// Returns null if no variant was selected (common mob).
    /// </summary>
    private STVariantEntry? RollVariant(STMobVariantConfigComponent config)
    {
        var roll = _random.NextFloat();
        var cumulative = 0f;

        foreach (var entry in config.Variants)
        {
            cumulative += entry.Chance;
            if (roll < cumulative)
                return entry;
        }

        return null;
    }

    /// <summary>
    /// Applies all variant modifications to the entity in-place.
    /// If <paramref name="existing"/> is provided, reuses it instead of creating a new marker.
    /// </summary>
    private void ApplyVariant(EntityUid uid, STVariantEntry variant, STMobVariantComponent? existing = null)
    {
        var marker = existing ?? EnsureComp<STMobVariantComponent>(uid);
        marker.Quality = variant.Quality;
        marker.SpriteTint = variant.SpriteTint;
        marker.SpriteSaturation = variant.SpriteSaturation;
        marker.SpriteBrightness = variant.SpriteBrightness;
        marker.Applied = true;
        Dirty(uid, marker);

        ScaleHealthThresholds(uid, variant.HealthMultiplier);
        ScaleDestructibleTriggers(uid, variant.HealthMultiplier);
        ScaleMeleeDamage(uid, variant.DamageMultiplier);
        ScaleSlowOnDamage(uid, variant.HealthMultiplier);
        ApplySpriteScale(uid, variant.SpriteScale);
        ApplyNameOverride(uid, variant.NameOverride);
        SwapButcherableLoot(uid, variant.ButcherSwaps);
        SwapDestructibleLoot(uid, variant.ButcherSwaps);
        ApplyWeightRange(uid, variant);
    }

    /// <summary>
    /// Scales all MobThresholds entries by the health multiplier using the MobThresholdSystem API.
    /// </summary>
    private void ScaleHealthThresholds(EntityUid uid, float multiplier)
    {
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (multiplier == 1f)
            return;

        if (!TryComp<MobThresholdsComponent>(uid, out var thresholds))
            return;

        // Snapshot current thresholds to avoid modifying during enumeration.
        var snapshot = new List<(FixedPoint2 Damage, MobState State)>();
        foreach (var (damage, state) in thresholds.Thresholds)
        {
            snapshot.Add((damage, state));
        }

        foreach (var (damage, state) in snapshot)
        {
            var scaled = damage * multiplier;
            _mobThreshold.SetMobStateThreshold(uid, scaled, state, thresholds);
        }
    }

    /// <summary>
    /// Scales DamageTrigger values in DestructibleComponent thresholds.
    /// </summary>
    private void ScaleDestructibleTriggers(EntityUid uid, float multiplier)
    {
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (multiplier == 1f)
            return;

        if (!TryComp<DestructibleComponent>(uid, out var destructible))
            return;

        // DestructibleComponent is server-only (not networked), no Dirty() needed.
        foreach (var threshold in destructible.Thresholds)
        {
            if (threshold.Trigger is DamageTrigger damageTrigger)
            {
                damageTrigger.Damage *= multiplier;
            }
        }
    }

    /// <summary>
    /// Scales MeleeWeaponComponent.Damage by the damage multiplier.
    /// </summary>
    private void ScaleMeleeDamage(EntityUid uid, float multiplier)
    {
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (multiplier == 1f)
            return;

        if (!TryComp<MeleeWeaponComponent>(uid, out var melee))
            return;

        melee.Damage = melee.Damage * multiplier;
        Dirty(uid, melee);
    }

    /// <summary>
    /// Scales SlowOnDamageComponent threshold keys by the health multiplier.
    /// </summary>
    private void ScaleSlowOnDamage(EntityUid uid, float multiplier)
    {
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (multiplier == 1f)
            return;

        if (!TryComp<SlowOnDamageComponent>(uid, out var slow))
            return;

        var scaled = new Dictionary<FixedPoint2, float>(slow.SpeedModifierThresholds.Count);
        foreach (var (damage, speedMod) in slow.SpeedModifierThresholds)
        {
            scaled[damage * multiplier] = speedMod;
        }

        slow.SpeedModifierThresholds = scaled;
        Dirty(uid, slow);
    }

    /// <summary>
    /// Applies a uniform sprite scale via the ScaleVisuals system.
    /// </summary>
    private void ApplySpriteScale(EntityUid uid, float scale)
    {
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (scale == 1f)
            return;

        // Get current scale and multiply it, so we stack with any existing scale.
        var current = _scaleVisuals.GetSpriteScale(uid);
        _scaleVisuals.SetSpriteScale(uid, current * new Vector2(scale, scale));
    }

    /// <summary>
    /// Sets the entity's display name from a localization key.
    /// </summary>
    private void ApplyNameOverride(EntityUid uid, LocId? nameOverride)
    {
        if (nameOverride == null)
            return;

        var meta = MetaData(uid);
        _metaData.SetEntityName(uid, Loc.GetString(nameOverride.Value), meta);
    }

    /// <summary>
    /// Swaps butcherable loot prototype IDs according to the variant's ButcherSwaps map.
    /// </summary>
    private void SwapButcherableLoot(EntityUid uid, Dictionary<EntProtoId, EntProtoId> swaps)
    {
        if (swaps.Count == 0)
            return;

        if (!TryComp<ButcherableComponent>(uid, out var butcherable))
            return;

        for (var i = 0; i < butcherable.SpawnedEntities.Count; i++)
        {
            var entry = butcherable.SpawnedEntities[i];
            if (entry.PrototypeId is { } protoId && swaps.TryGetValue(protoId, out var replacement))
            {
                entry.PrototypeId = replacement;
                butcherable.SpawnedEntities[i] = entry;
            }
        }

        Dirty(uid, butcherable);
    }

    /// <summary>
    /// Swaps entity prototype IDs in DestructibleComponent's SpawnEntitiesBehavior entries.
    /// Used for mobs that drop parts via Destructible instead of Butcherable (e.g. Psidog).
    /// </summary>
    private void SwapDestructibleLoot(EntityUid uid, Dictionary<EntProtoId, EntProtoId> swaps)
    {
        if (swaps.Count == 0)
            return;

        if (!TryComp<DestructibleComponent>(uid, out var destructible))
            return;

        foreach (var threshold in destructible.Thresholds)
        {
            foreach (var behavior in threshold.Behaviors)
            {
                if (behavior is not SpawnEntitiesBehavior spawnBehavior)
                    continue;

                var toSwap = new List<(EntProtoId Old, EntProtoId New)>();
                foreach (var (protoId, _) in spawnBehavior.Spawn)
                {
                    if (swaps.TryGetValue(protoId, out var replacement))
                        toSwap.Add((protoId, replacement));
                }

                foreach (var (old, newId) in toSwap)
                {
                    var minMax = spawnBehavior.Spawn[old];
                    spawnBehavior.Spawn.Remove(old);
                    spawnBehavior.Spawn[newId] = minMax;
                }
            }
        }

        // DestructibleComponent is server-only, no Dirty() needed.
    }

    /// <summary>
    /// Overrides weight variance range for this variant tier.
    /// Runs before STMobWeightVarianceSystem due to event ordering.
    /// </summary>
    private void ApplyWeightRange(EntityUid uid, STVariantEntry variant)
    {
        if (variant.MinWeightMultiplier == null && variant.MaxWeightMultiplier == null)
            return;

        if (!TryComp<STMobWeightVarianceComponent>(uid, out var variance))
            return;

        if (variant.MinWeightMultiplier != null)
            variance.MinWeightMultiplier = variant.MinWeightMultiplier.Value;

        if (variant.MaxWeightMultiplier != null)
            variance.MaxWeightMultiplier = variant.MaxWeightMultiplier.Value;
    }
}
