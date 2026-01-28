using Robust.Shared.Serialization;

namespace Content.Shared._Stalker_EN.Devices.Radar;

/// <summary>
/// Type of radar blip, used for visual differentiation.
/// </summary>
[Serializable, NetSerializable]
public enum RadarBlipType : byte
{
    Artifact = 0,
    Anomaly = 1,
}
