using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.Timing;

namespace Content.Shared._Stalker_EN.ZoneAnomaly.Effects.Components;

/// <summary>
/// Gibs entities that reach the anomaly's core after a delay.
/// When entity reaches core: stun → teleport to center → wait → gib → throw nearby entities.
/// </summary>
/// <remarks>
/// Used by vortex-type anomalies that pull entities in and destroy them at the center.
/// The gib effect continues processing even if the anomaly changes state, ensuring
/// doomed entities are always gibbed once marked.
/// </remarks>
[RegisterComponent]
public sealed partial class ZoneAnomalyEffectGibComponent : Component
{
    /// <summary>
    /// Distance from anomaly center at which entity becomes doomed (in tiles).
    /// </summary>
    /// <remarks>
    /// Recommended range: 0.3 to 1.0. Smaller values require precise positioning.
    /// Should be smaller than the anomaly's collision fixture radius.
    /// </remarks>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float CoreRadius = 0.5f;

    /// <summary>
    /// Time before doomed entity gets gibbed.
    /// </summary>
    /// <remarks>
    /// Recommended range: 2.0 to 4 seconds.
    /// Entity remains stunned and pinned to center during this time.
    /// </remarks>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan GibDelay = TimeSpan.FromSeconds(3.5);

    /// <summary>
    /// Entities marked for gibbing with their scheduled gib time.
    /// </summary>
    /// <remarks>
    /// Runtime state - not serialized. Cleared when anomaly deactivates.
    /// </remarks>
    [ViewVariables]
    public Dictionary<EntityUid, TimeSpan> DoomedEntities = new();

    /// <summary>
    /// Entities currently in the core radius with their doom deadline.
    /// If they stay until the deadline, they become doomed.
    /// If they escape before, they're removed from this tracking.
    /// </summary>
    /// <remarks>
    /// Runtime state - not serialized. Entities can escape by leaving the core radius
    /// before their deadline expires.
    /// </remarks>
    [ViewVariables]
    public Dictionary<EntityUid, TimeSpan> PendingDoom = new();

    /// <summary>
    /// Sound to play when an entity enters pending doom state (warning of imminent death).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public SoundSpecifier? PendingDoomSound;

    /// <summary>
    /// Whether to also gib organs when gibbing the body.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool GibOrgans = true;

    /// <summary>
    /// Only gib entities matching this whitelist. If null, gibs all valid entities.
    /// </summary>
    /// <remarks>
    /// Typically set to MobState component to only affect living creatures.
    /// </remarks>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public EntityWhitelist? Whitelist;

    /// <summary>
    /// Whether to throw everything away from center after gibbing.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool ThrowOnGib = true;

    /// <summary>
    /// Range of the throw effect after gibbing (in tiles).
    /// </summary>
    /// <remarks>
    /// Recommended range: 3.0 to 10.0. Determines how far gibs and nearby items scatter.
    /// </remarks>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float ThrowRange = 4f;

    /// <summary>
    /// Force multiplier for the throw effect.
    /// </summary>
    /// <remarks>
    /// Recommended range: 30.0 to 100.0. Higher values throw entities farther.
    /// Force is scaled by entity mass and distance from center.
    /// </remarks>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float ThrowForce = 50f;

    /// <summary>
    /// Tracks the last sent danger state to avoid redundant appearance updates.
    /// </summary>
    /// <remarks>
    /// Runtime state - not serialized. Used to prevent calling SetData every frame
    /// when the danger state hasn't changed.
    /// </remarks>
    [ViewVariables]
    public bool LastDangerState;

    // Reusable lists to avoid per-frame allocations
    public readonly List<EntityUid> PendingRemovalBuffer = new();
    public readonly List<EntityUid> GibRemovalBuffer = new();
    public readonly List<EntityUid> StaleEntityBuffer = new();
}
