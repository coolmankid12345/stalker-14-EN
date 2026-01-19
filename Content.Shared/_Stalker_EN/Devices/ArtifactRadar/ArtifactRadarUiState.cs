using Robust.Shared.Serialization;

namespace Content.Shared._Stalker_EN.Devices.ArtifactRadar;

[Serializable, NetSerializable]
public enum ArtifactRadarUiKey : byte
{
    Key,
}

/// <summary>
/// Message sent when user clicks anomaly detector toggle button.
/// </summary>
[Serializable, NetSerializable]
public sealed class ArtifactRadarToggleAnomalyDetectorMessage : BoundUserInterfaceMessage
{
}

/// <summary>
/// Message sent when user clicks artifact scanner toggle button.
/// </summary>
[Serializable, NetSerializable]
public sealed class ArtifactRadarToggleArtifactScannerMessage : BoundUserInterfaceMessage
{
}

/// <summary>
/// Represents a single blip on the artifact radar.
/// </summary>
[Serializable, NetSerializable]
public struct ArtifactRadarBlip
{
    /// <summary>
    /// Unique identifier for this artifact.
    /// </summary>
    public NetEntity Id;

    /// <summary>
    /// Angle in radians relative to the player's facing direction.
    /// 0 = directly ahead, positive = clockwise.
    /// </summary>
    public float Angle;

    /// <summary>
    /// Distance to the artifact in units.
    /// </summary>
    public float Distance;

    /// <summary>
    /// Detection level of the artifact (used for coloring).
    /// </summary>
    public int Level;

    public ArtifactRadarBlip(NetEntity id, float angle, float distance, int level)
    {
        Id = id;
        Angle = angle;
        Distance = distance;
        Level = level;
    }
}

/// <summary>
/// UI state sent from server to client containing radar blip data.
/// </summary>
[Serializable, NetSerializable]
public sealed class ArtifactRadarBoundUIState : BoundUserInterfaceState
{
    /// <summary>
    /// List of artifact blips to display on the radar.
    /// </summary>
    public readonly List<ArtifactRadarBlip> Blips;

    /// <summary>
    /// Maximum detection range of the device.
    /// </summary>
    public readonly float Range;

    /// <summary>
    /// Whether the artifact scanner (radar) is enabled.
    /// </summary>
    public readonly bool ArtifactScannerEnabled;

    /// <summary>
    /// Whether the device has anomaly detection capability (ZoneAnomalyDetectorComponent).
    /// </summary>
    public readonly bool HasAnomalyDetector;

    /// <summary>
    /// Whether the anomaly detector (beeping) is enabled.
    /// </summary>
    public readonly bool AnomalyDetectorEnabled;

    /// <summary>
    /// Localized device name for display in UI header.
    /// </summary>
    public readonly string DeviceName;

    /// <summary>
    /// Distance to the closest detected anomaly (null if none detected or detector disabled).
    /// </summary>
    public readonly float? ClosestAnomalyDistance;

    public ArtifactRadarBoundUIState(
        List<ArtifactRadarBlip> blips,
        float range,
        bool artifactScannerEnabled,
        bool hasAnomalyDetector,
        bool anomalyDetectorEnabled,
        string deviceName,
        float? closestAnomalyDistance = null)
    {
        Blips = blips;
        Range = range;
        ArtifactScannerEnabled = artifactScannerEnabled;
        HasAnomalyDetector = hasAnomalyDetector;
        AnomalyDetectorEnabled = anomalyDetectorEnabled;
        DeviceName = deviceName;
        ClosestAnomalyDistance = closestAnomalyDistance;
    }
}
