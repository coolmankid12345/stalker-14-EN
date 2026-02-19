using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Shared._Stalker_EN.SoftCrit;

/// <summary>
/// Runtime component that is dynamically added/removed when an entity is in the soft crit sub-phase
/// of <see cref="Content.Shared.Mobs.MobState.Critical"/>. Its presence means the entity can crawl
/// slowly and whisper, but cannot stand, use items, or speak normally.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class STSoftCritComponent : Component
{
    /// <summary>
    /// Movement speed multiplier while crawling in soft crit (copied from
    /// <see cref="STSoftCritEnabledComponent.CrawlSpeedModifier"/> at creation time).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float CrawlSpeedModifier = 0.3f;

    /// <summary>
    /// Total damage threshold at which soft crit ends and hard crit begins.
    /// Computed as: critThreshold + (deadThreshold - critThreshold) * softCritFraction.
    /// </summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2 HardCritThreshold;
}
