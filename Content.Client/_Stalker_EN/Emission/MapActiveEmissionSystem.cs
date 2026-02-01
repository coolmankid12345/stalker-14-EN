using Content.Shared._Stalker_EN.CCVar;
using Content.Shared._Stalker_EN.Emission;
using Robust.Shared.Configuration;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Client._Stalker_EN.Emission;

public sealed class MapActiveEmissionSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _configurationManager = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;

    private bool _simpleVisuals = false;

    public override void Initialize()
    {
        base.Initialize();

        // this can just go under reduced motion but whatever
        _configurationManager.OnValueChanged(STCCVars.EmissionSimpleVisuals, x => _simpleVisuals = x, invokeImmediately: true);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<MapActiveEmissionComponent, MapLightComponent>();
        while (query.MoveNext(out var activeEmissionComponent, out var mapLightComponent))
        {
            if (_simpleVisuals ||
                activeEmissionComponent.Deviation == 0)
            {
                mapLightComponent.AmbientLightColor = activeEmissionComponent.PrimaryEmissionColor;
                continue;
            }

            if (activeEmissionComponent.TotalDeviationDecreaseStartTime is { } decreaseStartTime &&
                _gameTiming.CurTime >= decreaseStartTime)
            {
                activeEmissionComponent.Deviation -= activeEmissionComponent.TotalDeviationDecreaseRate * frameTime;
                Log.Info($"Decreasing; rate {activeEmissionComponent.TotalDeviationDecreaseRate}, real {activeEmissionComponent.TotalDeviationDecreaseRate * frameTime}, now {_gameTiming.CurTime}, start {decreaseStartTime}, now {activeEmissionComponent.Deviation}");
            }

            mapLightComponent.AmbientLightColor = CalcEmissionColor(
                activeEmissionComponent,
                activeEmissionComponent.Deviation
            );
        }
    }

    // This is meant to be deterministic or whatever
    private Color CalcEmissionColorFFF(
        MapActiveEmissionComponent activeEmissionComponent,
        float deviation // from 0-1; 0 means no deviation from primary color
    )
    {
        // Yes these values are giga hardcoded but it doesn't really matter.

        var t = (float)_gameTiming.CurTime.TotalSeconds * activeEmissionComponent.Strength; // makes it smaller so that emission is slower

        var slow = MathF.Sin(t * 0.6f);
        var fast = MathF.Sin(t * 7.0f + slow * 4f);
        var folded = MathF.Abs(fast);

        var chs = MathF.Pow(folded, 0.35f);

        // Flicker
        var envelope = MathF.Abs(MathF.Sin(t * 0.7f));
        var flicker = MathF.Abs(MathF.Sin(t * 8f + fast * 3f));
        flicker = MathF.Pow(flicker, 0.35f);
        chs += 0.25f * flicker * envelope;

        chs = Math.Clamp(chs, 0f, 1f) * Math.Clamp(deviation, 0f, 1f);

        var surge = MathF.Abs(MathF.Sin(t * 0.8f + MathF.Sin(t * 3.7f)));
        surge = MathF.Pow(surge, 6f);
        chs = Math.Clamp(chs + surge * deviation * 0.5f, 0f, 1f);

        return Color.InterpolateBetween(activeEmissionComponent.SecondaryEmissionColor, activeEmissionComponent.PrimaryEmissionColor, chs);
    }

    private Color CalcEmissionColor(
        MapActiveEmissionComponent activeEmissionComponent,
        float deviation // from 0-1; 0 means no deviation from primary color
    )
    {
        // Yes these values are giga hardcoded but it doesn't really matter.

        var t = (float)_gameTiming.CurTime.TotalSeconds * activeEmissionComponent.Strength; // makes it smaller so that emission is slower

        // --- Base "anger" chaos for extra surges (mostly primary) ---
        var slow = MathF.Sin(t * 0.6f);
        var fast = MathF.Sin(t * 7.0f + slow * 4f);
        var folded = MathF.Abs(fast);

        // --- Flicker generation (secondary color) ---
        var envelope = MathF.Abs(MathF.Sin(t * 0.7f));     // slow rise/fall
        var flicker = MathF.Abs(MathF.Sin(t * 8f + fast * 3f)); // fast unstable
        flicker = MathF.Pow(flicker, 0.35f);

        // Scale flicker by envelope and overall strength
        var flickerAmount = Math.Clamp(flicker * envelope * deviation, 0f, 1f);

        // --- Surges that temporarily boost flickers ---
        var surge = MathF.Abs(MathF.Sin(t * 0.8f + MathF.Sin(t * 3.7f)));
        surge = MathF.Pow(surge, 6f);
        flickerAmount = Math.Clamp(flickerAmount + surge * deviation * 0.5f, 0f, 1f);

        return Color.InterpolateBetween(activeEmissionComponent.SecondaryEmissionColor, activeEmissionComponent.PrimaryEmissionColor, flickerAmount);
    }
}
