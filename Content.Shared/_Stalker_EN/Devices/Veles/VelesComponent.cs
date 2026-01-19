using Robust.Shared.GameStates;

namespace Content.Shared._Stalker_EN.Devices.Veles;

/// <summary>
/// Component for the Veles artifact radar device.
/// This device displays nearby artifacts on a 180-degree arc radar UI.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class VelesComponent : Component
{
    /// <summary>
    /// Maximum distance at which artifacts can be detected.
    /// </summary>
    [DataField]
    public float DetectionRange = 17f;

    /// <summary>
    /// Detection level - artifacts with DetectedLevel greater than this won't be detected.
    /// Level 5 is max, detects all artifacts.
    /// </summary>
    [DataField]
    public int Level = 5;

    /// <summary>
    /// Distance at which artifacts are spawned from anomalies.
    /// </summary>
    [DataField]
    public float ActivationDistance = 3f;

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
    /// Whether the device is currently active.
    /// </summary>
    [AutoNetworkedField]
    public bool Enabled;
}
