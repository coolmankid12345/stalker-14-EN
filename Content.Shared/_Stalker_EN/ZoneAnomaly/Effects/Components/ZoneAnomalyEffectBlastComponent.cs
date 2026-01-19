using Content.Shared.Whitelist;

namespace Content.Shared._Stalker_EN.ZoneAnomaly.Effects.Components;

/// <summary>
/// Throws entities outward from the anomaly center when charging state begins.
/// </summary>
/// <remarks>
/// Force scales with distance - entities closer to center are thrown harder.
/// Can be configured with a delay before the blast triggers.
/// </remarks>
[RegisterComponent]
public sealed partial class ZoneAnomalyEffectBlastComponent : Component
{
    /// <summary>
    /// Range of the blast effect (in tiles).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float ThrowRange = 5f;

    /// <summary>
    /// Base force for throwing entities.
    /// </summary>
    /// <remarks>
    /// Force is scaled by entity mass and distance from center.
    /// </remarks>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float ThrowForce = 60f;

    /// <summary>
    /// Delay after charging state begins before blast triggers (in seconds).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float Delay;

    /// <summary>
    /// Only affect entities matching this whitelist. If null, affects all non-static entities.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public EntityWhitelist? Whitelist;

    /// <summary>
    /// Whether the blast has triggered this charging cycle.
    /// </summary>
    /// <remarks>
    /// Runtime state - reset when anomaly leaves charging state.
    /// </remarks>
    [ViewVariables]
    public bool Triggered;

    /// <summary>
    /// Time accumulated since charging state began.
    /// </summary>
    [ViewVariables]
    public float AccumulatedTime;

    /// <summary>
    /// Previous anomaly state for detecting state transitions.
    /// </summary>
    [ViewVariables]
    public int? LastState;
}
