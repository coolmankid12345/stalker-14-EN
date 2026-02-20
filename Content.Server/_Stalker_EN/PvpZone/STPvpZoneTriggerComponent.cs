using Content.Shared._Stalker_EN.PvpZone;

namespace Content.Server._Stalker_EN.PvpZone;

/// <summary>
/// Placed on collision trigger entities to override the PvP zone indicator when players walk through them.
/// Use paired In/Out triggers to create zone override areas within a map.
/// </summary>
[RegisterComponent]
public sealed partial class STPvpZoneTriggerComponent : Component
{
    /// <summary>
    /// If true, entering this trigger sets the override zone.
    /// If false, clears the override and reverts to map default.
    /// </summary>
    [DataField]
    public bool IsEntering = true;

    /// <summary>
    /// The zone type to set when <see cref="IsEntering"/> is true.
    /// Ignored when <see cref="IsEntering"/> is false.
    /// </summary>
    [DataField]
    public STPvpZoneType Zone = STPvpZoneType.Green;
}
