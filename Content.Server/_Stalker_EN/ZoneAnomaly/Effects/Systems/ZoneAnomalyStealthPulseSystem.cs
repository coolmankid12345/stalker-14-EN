using Content.Server.Stealth;
using Content.Shared._Stalker.ZoneAnomaly;
using Content.Shared._Stalker.ZoneAnomaly.Components;
using Content.Shared._Stalker_EN.ZoneAnomaly.Effects.Components;
using Content.Shared.Stealth.Components;
using Robust.Shared.Timing;

namespace Content.Server._Stalker_EN.ZoneAnomaly.Effects.Systems;

/// <summary>
/// Pulses the stealth visibility during anomaly idle state.
/// Creates a breathing/pulsing visual effect by modifying the Stealth component's visibility.
/// </summary>
public sealed class ZoneAnomalyStealthPulseSystem : EntitySystem
{
    [Dependency] private readonly StealthSystem _stealth = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ZoneAnomalyStealthPulseComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ZoneAnomalyStealthPulseComponent, ZoneAnomalyChangedState>(OnStateChanged);
    }

    private void OnStartup(EntityUid uid, ZoneAnomalyStealthPulseComponent comp, ComponentStartup args)
    {
        // Check initial state and start pulsing if idle
        if (TryComp<ZoneAnomalyComponent>(uid, out var anomaly))
        {
            comp.IsPulsing = anomaly.State == ZoneAnomalyState.Idle;
        }
    }

    private void OnStateChanged(EntityUid uid, ZoneAnomalyStealthPulseComponent comp, ref ZoneAnomalyChangedState args)
    {
        comp.IsPulsing = args.Current == ZoneAnomalyState.Idle;
        comp.PulseTime = 0f; // Reset pulse cycle when state changes

        // Note: When not pulsing, ZoneAnomalyEffectStealth handles the visibility
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;

        var query = EntityQueryEnumerator<ZoneAnomalyStealthPulseComponent, StealthComponent>();
        while (query.MoveNext(out var uid, out var pulse, out var stealth))
        {
            // Only pulse during idle state
            if (!pulse.IsPulsing)
                continue;

            // Throttle updates based on interval
            if (curTime < pulse.NextUpdate)
                continue;

            pulse.NextUpdate = curTime + TimeSpan.FromSeconds(pulse.UpdateInterval);
            pulse.PulseTime += pulse.UpdateInterval;

            // Sine wave oscillation between MinVisibility and MaxVisibility
            // Sin output is -1 to 1, we convert to 0 to 1 for interpolation
            var t = (MathF.Sin(pulse.PulseTime * MathF.PI * 2f / pulse.PulseDuration) + 1f) / 2f;
            var visibility = pulse.MinVisibility + (pulse.MaxVisibility - pulse.MinVisibility) * t;

            _stealth.SetVisibility(uid, visibility);
        }
    }
}
