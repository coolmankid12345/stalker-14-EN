using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Shared._Stalker_EN.SoftCrit;

/// <summary>
/// YAML-configurable marker component that opts an entity into the soft crit system.
/// When present, entities entering <see cref="Content.Shared.Mobs.MobState.Critical"/> will have
/// their crit range split into a soft phase (crawling + whispering) and a hard phase (fully incapacitated).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class STSoftCritEnabledComponent : Component
{
    /// <summary>
    /// Fraction of the Critical-to-Dead damage range that counts as "soft crit."
    /// With default thresholds (Critical=100, Dead=200) and a fraction of 0.5,
    /// soft crit covers 100-149 damage and hard crit covers 150-199.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float SoftCritFraction = 0.5f;

    /// <summary>
    /// Movement speed multiplier applied during soft crit (0.3 = 30% of normal speed).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float CrawlSpeedModifier = 0.3f;
}
