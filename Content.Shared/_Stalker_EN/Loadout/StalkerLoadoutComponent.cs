using Content.Shared.Whitelist;
using Robust.Shared.GameStates;

namespace Content.Shared._Stalker_EN.Loadout;

/// <summary>
/// Configuration component for the stalker loadout system.
/// Add this to repository entities to customize loadout behavior.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class StalkerLoadoutComponent : Component
{
    /// <summary>
    /// Inventory slots to exclude from loadout operations.
    /// If null, uses default: [] (no slots excluded)
    /// </summary>
    [DataField]
    public List<string>? SlotBlacklist;

    /// <summary>
    /// Container names to skip during loadout capture.
    /// If null, uses default: ["toggleable-clothing", "actions"]
    /// </summary>
    [DataField]
    public List<string>? ContainerBlacklist;

    /// <summary>
    /// Entities matching this whitelist are EXCLUDED from loadouts.
    /// Uses standard EntityWhitelist (tags/components).
    /// If null, uses default component checks (organs, actions, etc.).
    /// </summary>
    [DataField]
    public EntityWhitelist? EntityBlacklist;
}
