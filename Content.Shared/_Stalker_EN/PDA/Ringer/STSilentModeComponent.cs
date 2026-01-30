using Robust.Shared.GameStates;

namespace Content.Shared._Stalker_EN.PDA.Ringer;

/// <summary>
/// Component that enables silent mode on a PDA ringer.
/// When enabled, the PDA will be completely silent - no ringtone sounds and no "PDA vibrates" popup.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class STSilentModeComponent : Component
{
    /// <summary>
    /// Whether silent mode is currently enabled.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Enabled;
}
