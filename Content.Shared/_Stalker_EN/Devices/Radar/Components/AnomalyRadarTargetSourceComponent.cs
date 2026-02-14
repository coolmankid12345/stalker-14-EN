namespace Content.Shared._Stalker_EN.Devices.Radar.Components;

/// <summary>
/// Component that provides anomaly targets to a radar display.
/// When attached to an entity with RadarDisplayComponent, it will detect
/// ZoneAnomalyComponent entities and display them as blips.
/// </summary>
[RegisterComponent]
public sealed partial class AnomalyRadarTargetSourceComponent : Component
{
    /// <summary>
    /// Maximum distance at which anomalies can be detected.
    /// </summary>
    [DataField]
    public float DetectionRange = 17f;

    /// <summary>
    /// Detection level - anomalies with DetectedLevel greater than this won't be detected.
    /// Level 5 is max, detects all anomalies.
    /// </summary>
    [DataField]
    public int Level = 5;
}
