using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Stalker_EN.Portraits;

/// <summary>
/// Defines a set of character portraits for a specific role.
/// Multiple textures per portrait allow random variation for NPCs
/// and player choice in the loadout screen.
/// </summary>
[Prototype("characterPortrait")]
public sealed partial class CharacterPortraitPrototype : IPrototype
{
    [ViewVariables]
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Display name shown in the portrait selector UI.
    /// </summary>
    [DataField(required: true)]
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Optional description for tooltip in the portrait selector.
    /// </summary>
    [DataField]
    public string? Description { get; private set; }

    /// <summary>
    /// The faction (band) this portrait belongs to.
    /// Leave empty to make the portrait admin-only (hidden from players).
    /// </summary>
    [DataField]
    public string BandId { get; private set; } = string.Empty;

    /// <summary>
    /// Optional role restriction. If set, only characters with this job can use the portrait.
    /// If null/empty, any member of the band can use it.
    /// </summary>
    [DataField]
    public string? JobId { get; private set; }

    /// <summary>
    /// List of available portrait textures for this role.
    /// NPCs pick one randomly. Players choose one in the loadout screen.
    /// Uses SpriteSpecifier to support both direct PNG paths and RSI states.
    /// </summary>
    [DataField(required: true)]
    public List<SpriteSpecifier> Textures { get; private set; } = new();
}
