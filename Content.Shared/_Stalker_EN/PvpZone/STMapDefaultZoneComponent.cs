namespace Content.Shared._Stalker_EN.PvpZone;

/// <summary>
/// Placed on map entities to define the default PvP zone type for that map.
/// Players entering this map without an area override will see this zone indicator.
/// </summary>
[RegisterComponent]
public sealed partial class STMapDefaultZoneComponent : Component
{
    /// <summary>
    /// The default PvP zone type for this map.
    /// </summary>
    [DataField]
    public STPvpZoneType Zone = STPvpZoneType.Yellow;
}
