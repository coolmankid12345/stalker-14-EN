using System.Numerics;
using Robust.Shared.Prototypes;

namespace Content.Server._Stalker_EN.Emission;

/// <summary>
///     Periodically spawns lightning around this.
/// </summary>
[RegisterComponent, Access([typeof(EmissionLightningSystem), typeof(EmissionEventRuleSystem)])]
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
