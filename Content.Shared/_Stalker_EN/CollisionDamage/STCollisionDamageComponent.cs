using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Stalker_EN.CollisionDamage;

/// <summary>
/// Applies damage to entities that collide with this entity.
/// Useful for charging mutants, projectiles, environmental hazards, and other contact damage mechanics.
/// </summary>
/// <remarks>
/// For collision events to fire between KinematicController bodies (mobs),
/// at least one fixture must have <c>hard: false</c> in its prototype definition.
/// This is a RobustToolbox physics engine requirement.
/// </remarks>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class STCollisionDamageComponent : Component
{
    /// <summary>
    /// The damage to apply on collision.
    /// </summary>
    [DataField(required: true)]
    public DamageSpecifier Damage = new();

    /// <summary>
    /// Whether to ignore target's damage resistances.
    /// </summary>
    [DataField]
    public bool IgnoreResistances;

    /// <summary>
    /// If true, only damages entities with an active mind (players).
    /// </summary>
    [DataField]
    public bool OnlyMindedEntities = true;

    /// <summary>
    /// Sound to play when hitting a target.
    /// </summary>
    [DataField]
    public SoundSpecifier? HitSound;

    /// <summary>
    /// If true, deletes this entity after hitting MaxTargets.
    /// </summary>
    [DataField]
    public bool DeleteOnHit = true;

    /// <summary>
    /// If set, only collisions with this fixture trigger damage.
    /// </summary>
    [DataField]
    public string? FixtureId;

    /// <summary>
    /// Maximum unique targets that can be damaged. Null for unlimited.
    /// </summary>
    [DataField]
    public int? MaxTargets = 1;

    /// <summary>
    /// Cooldown before the same target can be damaged again.
    /// </summary>
    [DataField]
    public TimeSpan DamageCooldown = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Number of unique targets hit so far.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public int TargetsHit;

    /// <summary>
    /// Tracks when each target was last damaged for cooldown purposes.
    /// Not networked - server authoritative.
    /// </summary>
    [ViewVariables]
    public Dictionary<EntityUid, TimeSpan> LastDamageTime = new();

    /// <summary>
    /// Set to true when deletion has been queued to prevent further processing.
    /// </summary>
    [ViewVariables]
    public bool PendingDeletion;
}
