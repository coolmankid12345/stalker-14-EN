namespace Content.Shared._Stalker_EN.Devices.Radar.Components;

/// <summary>
/// Component that provides artifact targets to a radar display.
/// When attached to an entity with RadarDisplayComponent, it will detect
/// ZoneArtifactDetectorTargetComponent entities and display them as blips.
/// </summary>
[RegisterComponent]
public sealed partial class ArtifactRadarTargetSourceComponent : Component
{
    /// <summary>
    /// Maximum distance at which artifacts can be detected.
    /// </summary>
    [DataField]
    public float DetectionRange = 17f;

    /// <summary>
    /// Detection level - artifacts with DetectedLevel greater than this won't be detected.
    /// Level 5 is max, detects all artifacts.
    /// </summary>
    [DataField]
    public int Level = 5;

    /// <summary>
    /// Distance at which artifacts are spawned from anomalies.
    /// </summary>
    [DataField]
    public float ActivationDistance = 3f;
}
