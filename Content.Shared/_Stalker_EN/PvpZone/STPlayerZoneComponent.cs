using Robust.Shared.GameStates;

namespace Content.Shared._Stalker_EN.PvpZone;

/// <summary>
/// Tracks the current PvP zone for a player entity.
/// <see cref="OverrideZone"/> takes precedence over the map default when set by area triggers.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class STPlayerZoneComponent : Component
{
    /// <summary>
    /// The current effective zone being displayed to the player.
    /// </summary>
    [DataField, AutoNetworkedField]
    public STPvpZoneType CurrentZone = STPvpZoneType.Yellow;

    /// <summary>
    /// When not null, this overrides the map default zone (set by area triggers).
    /// </summary>
    [DataField, AutoNetworkedField]
    public STPvpZoneType? OverrideZone;
}
