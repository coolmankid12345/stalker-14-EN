namespace Content.Shared._Stalker_EN.ZoneAnomaly.Effects.Components;

/// <summary>
/// Pulses the stealth visibility during anomaly idle state.
/// Works with the existing Stealth system by modifying visibility values.
/// </summary>
[RegisterComponent]
public sealed partial class ZoneAnomalyStealthPulseComponent : Component
{
    /// <summary>
    /// Minimum stealth visibility during idle pulse (0.0 to 1.0).
    /// </summary>
    [DataField]
    public float MinVisibility = 0.3f;

    /// <summary>
    /// Maximum stealth visibility during idle pulse (0.0 to 1.0).
    /// </summary>
    [DataField]
    public float MaxVisibility = 0.7f;

    /// <summary>
    /// Duration of one complete pulse cycle (min→max→min) in seconds.
    /// </summary>
    [DataField]
    public float PulseDuration = 2.0f;

    /// <summary>
    /// How often to update visibility in seconds.
    /// Lower values = smoother animation but more server updates.
    /// </summary>
    [DataField]
    public float UpdateInterval = 0.1f;

    // Runtime state (not serialized)

    /// <summary>
    /// Next time to update the visibility.
    /// </summary>
    public TimeSpan NextUpdate;

    /// <summary>
    /// Current position in the pulse cycle.
    /// </summary>
    public float PulseTime;

    /// <summary>
    /// Whether the component is currently pulsing (only in Idle state).
    /// </summary>
    public bool IsPulsing;
}
