using Robust.Shared.GameStates;

namespace Content.Shared.Inventory.ArtifactSlots;

/// <summary>
/// Runtime state stored on a mob tracking how many artifact slots are currently active.
/// Synced from server to client so the UI can reflect slot availability.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
[Access(typeof(SharedArtifactSlotSystem))]
public sealed partial class ArtifactSlotsComponent : Component
{
    /// <summary>
    /// The current number of active artifact slots on this entity.
    /// Updated server-side and synced to clients.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public int ActiveCount;
}
