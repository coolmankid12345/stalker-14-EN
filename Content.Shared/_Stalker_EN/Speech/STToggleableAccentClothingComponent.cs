using Robust.Shared.GameStates;

namespace Content.Shared._Stalker_EN.Speech;

/// <summary>
///     Marker component for accent clothing that can be toggled on/off via the verb menu.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class STToggleableAccentClothingComponent : Component
{
    /// <summary>
    ///     Whether the accent effect is currently active.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Enabled = true;
}
