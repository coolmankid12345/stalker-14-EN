using Robust.Shared.GameStates;

namespace Content.Shared._Stalker_EN.Trophy;

/// <summary>
/// Marks an item as a trophy obtained from a variant mutant mob.
/// Provides quality-based examine text, a price multiplier for selling,
/// and visual shader parameters inherited from the source mob's variant.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class STTrophyComponent : Component
{
    /// <summary>
    /// The quality tier of this trophy, determines examine text color.
    /// </summary>
    [DataField, AutoNetworkedField]
    public STTrophyQuality Quality = STTrophyQuality.Common;

    /// <summary>
    /// Multiplier applied to the item's base Currency values at sell time.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float PriceMultiplier = 1f;

    /// <summary>
    /// Weight (kg) of the source mob this trophy was obtained from.
    /// Set at spawn time when butchered from a variant mob.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float SourceMobWeight;

    /// <summary>
    /// Optional color tint inherited from the source mob's variant shader.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Color? SpriteTint;

    /// <summary>
    /// Saturation multiplier inherited from the source mob's variant shader.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float SpriteSaturation = 1f;

    /// <summary>
    /// Brightness multiplier inherited from the source mob's variant shader.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float SpriteBrightness = 1f;
}
