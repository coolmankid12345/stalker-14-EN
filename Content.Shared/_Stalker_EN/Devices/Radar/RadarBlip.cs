using Robust.Shared.Serialization;

namespace Content.Shared._Stalker_EN.Devices.Radar;

/// <summary>
/// Represents a single blip on the radar display.
/// </summary>
[Serializable, NetSerializable]
public struct RadarBlip
{
    /// <summary>
    /// Unique identifier for this entity.
    /// </summary>
    public NetEntity Id;

    /// <summary>
    /// Angle in radians relative to grid-local space.
    /// 0 = directly north, positive = clockwise.
    /// </summary>
    public float Angle;

    /// <summary>
    /// Distance to the target in units.
    /// </summary>
    public float Distance;

    /// <summary>
    /// Detection level of the target.
    /// </summary>
    public int Level;

    /// <summary>
    /// Type of blip, used for visual differentiation (color).
    /// </summary>
    public RadarBlipType Type;

    public RadarBlip(NetEntity id, float angle, float distance, int level, RadarBlipType type)
    {
        Id = id;
        Angle = angle;
        Distance = distance;
        Level = level;
        Type = type;
    }
}
