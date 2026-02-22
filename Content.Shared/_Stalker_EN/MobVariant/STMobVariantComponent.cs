using Content.Shared._Stalker_EN.Trophy;
using Robust.Shared.GameStates;

namespace Content.Shared._Stalker_EN.MobVariant;

/// <summary>
/// Runtime marker component added to entities that have been promoted to a variant.
/// Prevents re-rolling on subsequent MapInit events and networks the variant quality
/// and sprite tint to clients.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class STMobVariantComponent : Component
{
    /// <summary>
    /// The quality tier this entity was promoted to.
    /// </summary>
    [DataField, AutoNetworkedField]
    public STTrophyQuality Quality = STTrophyQuality.Common;

    /// <summary>
    /// Optional color tint applied via the STMobTint shader on the client.
    /// Stored here because SpriteComponent is client-only.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Color? SpriteTint;

    /// <summary>
    /// Saturation multiplier for the STMobTint shader (0=greyscale, 1=normal, >1=vivid).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float SpriteSaturation = 1f;

    /// <summary>
    /// Brightness multiplier for the STMobTint shader (0=black, 1=normal, >1=bright).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float SpriteBrightness = 1f;

    /// <summary>
    /// Whether variant modifications have already been applied. Prevents re-processing.
    /// When false and Quality is set (from YAML), the system applies the matching variant on MapInit.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Applied;
}
