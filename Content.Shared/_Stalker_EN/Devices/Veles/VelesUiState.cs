using Robust.Shared.Serialization;

namespace Content.Shared._Stalker_EN.Devices.Veles;

[Serializable, NetSerializable]
public enum VelesUiKey : byte
{
    Key,
}

/// <summary>
/// Message sent when user clicks anomaly detector toggle button.
/// </summary>
[Serializable, NetSerializable]
public sealed class VelesToggleAnomalyDetectorMessage : BoundUserInterfaceMessage
{
}

/// <summary>
/// Message sent when user clicks artifact scanner toggle button.
/// </summary>
[Serializable, NetSerializable]
public sealed class VelesToggleArtifactScannerMessage : BoundUserInterfaceMessage
{
}

/// <summary>
/// Represents a single blip on the Veles radar.
/// </summary>
[Serializable, NetSerializable]
public struct VelesBlip
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

    public VelesBlip(NetEntity id, float angle, float distance, int level)
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
public sealed class VelesBoundUIState : BoundUserInterfaceState
{
    /// <summary>
    /// List of artifact blips to display on the radar.
    /// </summary>
    public readonly List<VelesBlip> Blips;

    /// <summary>
    /// Maximum detection range of the device.
    /// </summary>
    public readonly float Range;

    /// <summary>
    /// Whether the anomaly detector (beeping) is enabled.
    /// </summary>
    public readonly bool AnomalyDetectorEnabled;

    /// <summary>
    /// Whether the artifact scanner (radar) is enabled.
    /// </summary>
    public readonly bool ArtifactScannerEnabled;

    /// <summary>
    /// Distance to the closest detected anomaly (null if none detected or detector disabled).
    /// </summary>
    public readonly float? ClosestAnomalyDistance;

    public VelesBoundUIState(List<VelesBlip> blips, float range, bool anomalyDetectorEnabled, bool artifactScannerEnabled, float? closestAnomalyDistance = null)
    {
        Blips = blips;
        Range = range;
        AnomalyDetectorEnabled = anomalyDetectorEnabled;
        ArtifactScannerEnabled = artifactScannerEnabled;
        ClosestAnomalyDistance = closestAnomalyDistance;
    }
}
