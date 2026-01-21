using System.Numerics;
using Robust.Shared.Map;

namespace Content.Shared._Stalker_EN.Devices.Radar;

/// <summary>
/// Event raised during radar update cycle to collect blips from target sources.
/// Target source systems subscribe to this event and add their targets to the Blips list.
/// </summary>
[ByRefEvent]
public struct RadarTargetSourceUpdateEvent
{
    /// <summary>
    /// The user holding/using the radar.
    /// </summary>
    public EntityUid User;

    /// <summary>
    /// Map coordinates of the user for distance/angle calculations.
    /// </summary>
    public MapCoordinates UserMapCoords;

    /// <summary>
    /// World position of the user.
    /// </summary>
    public Vector2 UserWorldPos;

    /// <summary>
    /// Grid UID the user is on (null if not on a grid).
    /// </summary>
    public EntityUid? UserGridUid;

    /// <summary>
    /// List of blips that target sources add to.
    /// </summary>
    public List<RadarBlip> Blips;

    public RadarTargetSourceUpdateEvent(
        EntityUid user,
        MapCoordinates userMapCoords,
        Vector2 userWorldPos,
        EntityUid? userGridUid,
        List<RadarBlip> blips)
    {
        User = user;
        UserMapCoords = userMapCoords;
        UserWorldPos = userWorldPos;
        UserGridUid = userGridUid;
        Blips = blips;
    }
}
