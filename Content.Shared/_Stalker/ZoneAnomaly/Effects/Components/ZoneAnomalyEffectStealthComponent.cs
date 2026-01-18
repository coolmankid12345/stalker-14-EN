namespace Content.Shared._Stalker.ZoneAnomaly.Effects.Components;

[RegisterComponent]
public sealed partial class ZoneAnomalyEffectStealthComponent : Component
{
    [DataField]
    public float Idle = -0.5f;

    [DataField]
    public float Activated = 0f;

    [DataField]
    public float Charging = -0.5f;

    /// <summary>
    /// Duration of the fade-out animation when entering Charging state (in seconds).
    /// </summary>
    [DataField]
    public float ChargingFadeDuration = 0.5f;

    // Runtime state (not serialized)

    /// <summary>
    /// Whether the entity is currently fading to charging visibility.
    /// </summary>
    public bool IsFading;

    /// <summary>
    /// The visibility value when fade started.
    /// </summary>
    public float FadeStartVisibility;

    /// <summary>
    /// The time when fade started.
    /// </summary>
    public TimeSpan FadeStartTime;
}
