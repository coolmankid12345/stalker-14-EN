using Content.Shared.Weather;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Server._Stalker_EN.Weather;

/// <summary>
/// Per-map weather configuration override.
/// </summary>
[DataDefinition, Serializable]
public sealed partial class MapWeatherOverride
{
    /// <summary>
    /// If false, this map will not receive any weather.
    /// </summary>
    [DataField]
    public bool WeatherEnabled = true;

    /// <summary>
    /// Weighted pool of weather prototypes for this map.
    /// If null/empty, uses the default pool.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<WeatherPrototype>, float>? WeatherPool;

    /// <summary>
    /// Weight for clear weather on this map.
    /// If null, uses the default.
    /// </summary>
    [DataField]
    public float? ClearWeatherWeight;

    /// <summary>
    /// Multiplier for weather duration on this map.
    /// </summary>
    [DataField]
    public float WeatherDurationMultiplier = 1.0f;
}
