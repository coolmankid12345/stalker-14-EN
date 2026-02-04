using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Stalker_EN.FirePat;

/// <summary>
/// Component for entities that can pat burning entities to reduce their fire stacks.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class STFirePatterComponent : Component
{
    /// <summary>
    /// Fire stacks added per pat. Negative values reduce fire stacks.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Stacks = -2f;

    /// <summary>
    /// Minimum time between pats.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(1);

    /// <summary>
    /// When this entity last patted someone (absolute game time).
    /// </summary>
    [ViewVariables, AutoPausedField]
    public TimeSpan LastPatTime;

    /// <summary>
    /// Sound played when patting a burning entity.
    /// </summary>
    [DataField, AutoNetworkedField]
    public SoundSpecifier? Sound = new SoundPathSpecifier("/Audio/Effects/thudswoosh.ogg");

    /// <summary>
    /// Entities matching this blacklist cannot be patted.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityWhitelist? Blacklist;
}
