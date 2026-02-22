using Content.Shared._Stalker.Weight;
using Content.Shared._Stalker_EN.MobVariant;
using Robust.Shared.Random;

namespace Content.Server._Stalker_EN.MobVariant;

/// <summary>
/// Randomizes mob weight at MapInit within a multiplier range.
/// Runs after <see cref="STMobVariantSystem"/> so per-tier weight range overrides are applied first.
/// </summary>
public sealed class STMobWeightVarianceSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STMobWeightVarianceComponent, MapInitEvent>(
            OnMapInit,
            after: [typeof(STMobVariantSystem)]);
    }

    private void OnMapInit(
        EntityUid uid,
        STMobWeightVarianceComponent variance,
        MapInitEvent args)
    {
        if (TryComp<STWeightComponent>(uid, out var weight))
        {
            var originalWeight = weight.Self;
            if (originalWeight > 0f)
            {
                var multiplier = _random.NextFloat(variance.MinWeightMultiplier, variance.MaxWeightMultiplier);
                weight.Self = originalWeight * multiplier;
                Dirty(uid, weight);
            }
        }

        // Config is consumed; remove to free memory.
        RemComp<STMobWeightVarianceComponent>(uid);
    }
}
