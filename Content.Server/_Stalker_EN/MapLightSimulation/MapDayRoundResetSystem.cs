using Content.Server._Stalker.MapLightSimulation;
using Content.Shared.GameTicking;

namespace Content.Server._Stalker_EN.MapLightSimulation;

/// <summary>
/// Resets the day/night cycle on round restart as a safety net.
/// Prevents <see cref="MapDaySystem"/>._enabled from being stuck at false
/// if any system failed to re-enable it before the round ended.
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
    }
}
