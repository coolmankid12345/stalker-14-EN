using Content.Shared.Destructible.Thresholds;
using Content.Shared.Weather;
using Robust.Shared.Prototypes;

namespace Content.Server._Stalker_EN.Weather;

[RegisterComponent, Access(typeof(WeatherSchedulerRuleSystem))]
public sealed partial class WeatherSchedulerRuleComponent : Component
{
    /// <summary>
    /// Weighted pool of weather prototypes. Higher weight = more likely.
    /// </summary>
    [DataField(required: true)]
    public Dictionary<ProtoId<WeatherPrototype>, float> WeatherPool = new();

    /// <summary>
    /// Weight for clear weather (no weather effect).
    /// </summary>
    [DataField]
    public float ClearWeatherWeight = 1.0f;

    /// <summary>
    /// Min/max seconds between weather changes.
    /// </summary>
    [DataField]
    public MinMax ChangeInterval = new(600, 1800);

    /// <summary>
    /// Min/max duration in seconds for each weather effect.
    /// </summary>
    [DataField]
    public MinMax WeatherDuration = new(300, 900);

    /// <summary>
    /// Whether to pause scheduling during active emissions.
    /// </summary>
    [DataField]
    public bool PauseDuringEmission = true;

    /// <summary>
    /// Per-map weather overrides, keyed by STMapKey value.
    /// Maps not listed use default pool.
    /// </summary>
    [DataField]
    public Dictionary<string, MapWeatherOverride> MapOverrides = new();

    // Runtime state (not serialized)
    public TimeSpan NextWeatherChangeTime;
    public ProtoId<WeatherPrototype>? CurrentWeather;
    public bool WaitingForEmission;
    public bool CachedEmissionActive;
    public TimeSpan NextEmissionCheckTime;
}
