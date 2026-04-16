using Robust.Shared.GameStates;

namespace Content.Shared.Teleportation.Components;

/// <summary>
///     Attached to an entity after portal transit to mark that they should not immediately be portaled back
///     at the end destination.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class PortalTimeoutComponent : Component
{
    /// <summary>
    ///     The portal that was entered. Null if coming from a hand teleporter, etc.
    /// </summary>
    [ViewVariables, DataField]
    public EntityUid? EnteredPortal;

    // Stalker-Changes
    [ViewVariables, AutoNetworkedField, AutoPausedField]
    public TimeSpan Cooldown;
}
