using Content.Shared._Stalker.Pack;
using Robust.Shared.Prototypes;

namespace Content.Server._Stalker.Pack;

[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(STPackSystem))]
public sealed partial class STPackSpawnerComponent : Component
{
    [DataField]
    public ProtoId<STPackPrototype> ProtoId;

    /// <summary>
    /// Chance of spawning a pack when triggered (0-1).
    /// </summary>
    [DataField]
    public float Chance = 1.0f;

    /// <summary>
    /// Cooldown in seconds before trigger can fire again.
    /// </summary>
    [DataField]
    public float Cooldown = 600f;

    /// <summary>
    /// Time when cooldown expires and trigger can fire again.
    /// </summary>
    [AutoPausedField]
    public TimeSpan? CooldownTime;

    /// <summary>
    /// Whether this spawner is currently enabled.
    /// </summary>
    public bool Enabled = true;
}
