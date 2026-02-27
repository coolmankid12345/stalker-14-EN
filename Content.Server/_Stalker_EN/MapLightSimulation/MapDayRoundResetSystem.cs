using Content.Server._Stalker.MapLightSimulation;
using Content.Shared._Stalker_EN.Emission;
using Content.Shared.GameTicking;
using Content.Shared.Light.Components;

namespace Content.Server._Stalker_EN.MapLightSimulation;

/// <summary>
/// Resets the day/night cycle and emission lighting state on round restart.
/// Safety net that prevents lighting from being stuck if any system
/// (emission, anomaly explosion, etc.) failed to clean up before the round ended.
/// </summary>
public sealed class MapDayRoundResetSystem : EntitySystem
{
    [Dependency] private readonly MapDaySystem _mapDay = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent args)
    {
        _mapDay.SetEnabled(true);

        // Remove any lingering emission overlays
        var emissionQuery = EntityQueryEnumerator<MapActiveEmissionComponent>();
        while (emissionQuery.MoveNext(out var uid, out var comp))
            RemCompDeferred(uid, comp);

        // Restore light cycle state (covers both emission and anomaly explosion corruption)
        var cycleQuery = EntityQueryEnumerator<LightCycleComponent>();
        while (cycleQuery.MoveNext(out var uid, out var cycle))
        {
            cycle.Enabled = true;
            cycle.OriginalColor = cycle.UnchangedOriginalColor;
            cycle.MinLevel = cycle.OriginalMinLevel;
            Dirty(uid, cycle);
        }
    }
}
