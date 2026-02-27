using Content.Shared._Stalker.Anomaly.Prototypes;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._Stalker_EN.Emission;

/// <summary>
/// Phase of the anomaly regeneration state machine during an emission.
/// </summary>
public enum EmissionRegenPhase : byte
{
    Idle,
    WaitingForDeletion,
    Deleting,
    WaitingForRegeneration,
    Regenerating,
    Complete,
}

/// <summary>
/// Added to the emission game rule entity to enable anomaly deletion and regeneration during emissions.
/// Orchestrates staggered per-map deletion during Stage 2 and staggered regeneration during Stage 3.
/// </summary>
[RegisterComponent, Access(typeof(STEmissionAnomalyRegenSystem))]
public sealed partial class EmissionAnomalyRegenComponent : Component
{
    /// <summary>
    /// Delay after Stage 2 starts before anomaly deletion begins.
    /// </summary>
    [DataField]
    public TimeSpan DeletionDelay = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Delay between deleting anomalies on consecutive maps.
    /// </summary>
    [DataField]
    public TimeSpan DeletionStaggerInterval = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Delay after all deletions complete before anomaly regeneration begins.
    /// </summary>
    [DataField]
    public TimeSpan RegenerationDelay = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Delay between starting regeneration on consecutive maps.
    /// </summary>
    [DataField]
    public TimeSpan RegenerationStaggerInterval = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Whether anomaly regeneration is enabled. Can be toggled per emission event in YAML.
    /// </summary>
    [DataField]
    public bool Enabled = true;

    // Runtime state -- not serialized, not networked.

    /// <summary>
    /// Current phase of the anomaly regeneration state machine.
    /// </summary>
    public EmissionRegenPhase Phase = EmissionRegenPhase.Idle;

    /// <summary>
    /// Absolute time of the next stagger action (deletion or regeneration step).
    /// </summary>
    public TimeSpan NextAction;

    /// <summary>
    /// Ordered list of maps whose anomalies remain to be deleted.
    /// </summary>
    public List<MapId> PendingDeletionMaps = new();

    /// <summary>
    /// Ordered list of maps with their generation options, awaiting anomaly regeneration.
    /// </summary>
    public List<(MapId MapId, ProtoId<STAnomalyGenerationOptionsPrototype> OptionsId)> PendingRegenerationMaps = new();

    /// <summary>
    /// Index into the current pending list (deletion or regeneration) being processed.
    /// </summary>
    public int CurrentMapIndex;
}
