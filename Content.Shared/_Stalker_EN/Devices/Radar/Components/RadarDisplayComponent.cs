using Robust.Shared.GameStates;

namespace Content.Shared._Stalker_EN.Devices.Radar.Components;

/// <summary>
/// Component for radar display devices.
/// This component handles the visual radar UI, while target sources
/// (ArtifactRadarTargetSourceComponent, AnomalyRadarTargetSourceComponent)
/// provide the targets to display.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class RadarDisplayComponent : Component
{
    /// <summary>
    /// Maximum display range of the radar.
    /// </summary>
    [DataField]
    public float DisplayRange = 17f;

    /// <summary>
    /// How often the radar updates.
    /// </summary>
    [DataField]
    public TimeSpan UpdateInterval = TimeSpan.FromMilliseconds(125);

    /// <summary>
    /// Next time to update the radar blips.
    /// </summary>
    [AutoNetworkedField, AutoPausedField]
    public TimeSpan NextUpdateTime;

    /// <summary>
    /// Whether the radar display is currently active.
    /// </summary>
    [AutoNetworkedField]
    public bool Enabled;
}
