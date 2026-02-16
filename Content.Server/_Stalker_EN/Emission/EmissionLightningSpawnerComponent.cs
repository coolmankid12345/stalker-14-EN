using System.Numerics;
using Robust.Shared.Prototypes;

namespace Content.Server._Stalker_EN.Emission;

/// <summary>
///     Periodically spawns lightning around this.
/// </summary>
[RegisterComponent, Access([typeof(EmissionLightningSystem), typeof(EmissionEventRuleSystem), typeof(EmissionLightningSpawnerSystem), typeof(EmissionSafeZoneSystem)])]
[AutoGenerateComponentPause]
public sealed partial class EmissionLightningSpawnerComponent : Component
{
    /// <summary>
    ///     Minimum and maximum random interval (in seconds) between lightning spawned.
    /// </summary>
    [DataField]
    public Vector2 LightningIntervalRange = new(5f, 10f);

    /// <summary>
    ///     Interval between lightning spawned per player.
    /// </summary>
    [AutoPausedField]
    public TimeSpan NextLightning = TimeSpan.MinValue;

    /// <summary>
    ///     Entity prototype of lightning effect to spawn.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId LightningEffectProtoId;

    /// <summary>
    ///     Radius in which lightning can spawn
    /// </summary>
    [DataField]
    public float SpawnRadius = 30f;

    /// <summary>
    ///     When this spawner is on someone with <see cref="_Stalker.StationEvents.Components.StalkerSafeZoneComponent"/>,
    ///         the *minimum* spawn radius will effectively be this multiplied by <see cref="SpawnRadius"/>.
    ///
    ///     Otherwise, there is no minimum.
    /// </summary>
    [DataField]
    public float SafeMinimumSpawnRadiusMultiplier = 0.05f; // This used to be 65% but i lowered it because i added raycast and roof checks

    /// <summary>
    ///     Range of lightning bolts.
    /// </summary>
    [DataField]
    public float BoltRange = 4.5f;

    /// <summary>
    ///     Bolts of lightning to shoot on every spawn.
    /// </summary>
    [DataField]
    public int BoltCount = 3;
}
