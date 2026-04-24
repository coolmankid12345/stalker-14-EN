using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.ViewVariables;

namespace Content.Shared._Stalker_EN.Portraits;

/// <summary>
/// Stores the selected character portrait for a mob entity.
/// Set during character spawning from the player's profile or manually via VV for NPCs.
/// Used for PDA notification sender icons.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CharacterPortraitComponent : Component
{
    /// <summary>
    /// The resolved texture path of the selected portrait.
    /// Empty string means no portrait selected.
    /// Set directly from player profile or manually via VV for NPCs.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public string PortraitTexturePath = string.Empty;

    /// <summary>
    /// If true, the portrait was explicitly selected by the player in the character profile.
    /// Prevents automatic re-resolution via BandsComponent ComponentAdd to preserve player choice.
    /// Set to false for NPCs and when portrait is auto-resolved.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public bool ExplicitlySelected = false;

    /// <summary>
    /// Optional job ID for portrait selection, overrides hierarchy-based job resolution.
    /// Used for NPCs that need specific portraits but aren't in the band hierarchy (e.g., rookie).
    /// Set in YAML for NPC dolls.
    /// </summary>
    [DataField("portraitJobId"), ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public string? PortraitJobId;

    /// <summary>
    /// Resolved texture path for the disguise portrait (e.g., Stalker portrait for Clear Sky).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public string DisguisedPortraitPath = string.Empty;

    /// <summary>
    /// If true, faction members use DisguisedPortraitPath for PDA icons.
    /// Defaults to false (not masked). Set to true only for factions with CanDisguiseAsStalker.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public bool IsDisguised = false;
}
