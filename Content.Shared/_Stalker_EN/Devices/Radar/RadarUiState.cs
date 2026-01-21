using Robust.Shared.Serialization;

namespace Content.Shared._Stalker_EN.Devices.Radar;

[Serializable, NetSerializable]
public enum RadarDisplayUiKey : byte
{
    Key,
}

/// <summary>
/// Message sent when user clicks anomaly detector toggle button.
/// </summary>
[Serializable, NetSerializable]
public sealed class RadarToggleAnomalyDetectorMessage : BoundUserInterfaceMessage
{
}

/// <summary>
/// Message sent when user clicks artifact scanner toggle button.
/// </summary>
[Serializable, NetSerializable]
public sealed class RadarToggleArtifactScannerMessage : BoundUserInterfaceMessage
{
}

/// <summary>
/// UI state sent from server to client containing radar blip data.
/// </summary>
[Serializable, NetSerializable]
public sealed class RadarDisplayBoundUIState : BoundUserInterfaceState
{
    /// <summary>
    /// List of blips to display on the radar.
    /// </summary>
    public readonly List<RadarBlip> Blips;

    /// <summary>
    /// Maximum display range of the device.
    /// </summary>
    public readonly float Range;

    /// <summary>
    /// Whether the radar display is enabled.
    /// </summary>
    public readonly bool RadarEnabled;

    /// <summary>
    /// Whether the device has anomaly detection capability (ZoneAnomalyDetectorComponent).
    /// </summary>
    public readonly bool HasAnomalyDetector;

    /// <summary>
    /// Whether the anomaly detector (beeping) is enabled.
    /// </summary>
    public readonly bool AnomalyDetectorEnabled;

    /// <summary>
    /// Whether the device can detect artifacts (has ArtifactRadarTargetSourceComponent).
    /// </summary>
    public readonly bool HasArtifactDetection;

    /// <summary>
    /// Whether the device can detect anomalies on radar (has AnomalyRadarTargetSourceComponent).
    /// </summary>
    public readonly bool HasAnomalyDetection;

    /// <summary>
    /// Localized device name for display in UI header.
    /// </summary>
    public readonly string DeviceName;

    /// <summary>
    /// Distance to the closest detected anomaly (null if none detected or detector disabled).
    /// </summary>
    public readonly float? ClosestAnomalyDistance;

    public RadarDisplayBoundUIState(
        List<RadarBlip> blips,
        float range,
        bool radarEnabled,
        bool hasAnomalyDetector,
        bool anomalyDetectorEnabled,
        string deviceName,
        bool hasArtifactDetection,
        bool hasAnomalyDetection,
        float? closestAnomalyDistance = null)
    {
        Blips = blips;
        Range = range;
        RadarEnabled = radarEnabled;
        HasAnomalyDetector = hasAnomalyDetector;
        AnomalyDetectorEnabled = anomalyDetectorEnabled;
        DeviceName = deviceName;
        HasArtifactDetection = hasArtifactDetection;
        HasAnomalyDetection = hasAnomalyDetection;
        ClosestAnomalyDistance = closestAnomalyDistance;
    }
}
