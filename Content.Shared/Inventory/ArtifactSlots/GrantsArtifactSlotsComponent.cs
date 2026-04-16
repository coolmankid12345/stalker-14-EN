using Robust.Shared.GameStates;

namespace Content.Shared.Inventory.ArtifactSlots;

/// <summary>
/// When placed on an item (typically armor), grants a number of artifact slots
/// when equipped. Only one such item's value is active at a time — the highest.
/// </summary>
[RegisterComponent]
public sealed partial class GrantsArtifactSlotsComponent : Component
{
    /// <summary>
    /// How many artifact slots this item grants when equipped.
    /// </summary>
    [DataField(required: true)]
    public int Slots;
}
