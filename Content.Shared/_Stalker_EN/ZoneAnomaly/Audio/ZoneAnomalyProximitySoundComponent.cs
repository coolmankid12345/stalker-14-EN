using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Stalker_EN.ZoneAnomaly.Audio;

/// <summary>
/// Plays an ambient sound that changes volume based on player distance to the entity center.
/// Volume scales linearly from MinVolume at MaxRange to MaxVolume at the center.
/// </summary>
/// <remarks>
/// Uses cooldown-based updates for performance (not per-frame).
/// </remarks>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ZoneAnomalyProximitySoundComponent : Component
{
    /// <summary>
    /// Maximum distance from center where the sound is audible.
    /// At this distance, volume equals MinVolume.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MaxRange = 12f;

    /// <summary>
    /// Volume at maximum range (0.0 to 1.0).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MinVolume = 0.2f;

    /// <summary>
    /// Volume at center (0.0 to 1.0).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MaxVolume = 1.0f;

    /// <summary>
    /// How often to update volume (in seconds).
    /// Lower values = smoother but more expensive.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float UpdateCooldown = 0.25f;

    /// <summary>
    /// The sound to play.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public SoundSpecifier Sound = default!;

    // Runtime state (not serialized)

    /// <summary>
    /// Entity of the currently playing audio stream.
    /// </summary>
    [ViewVariables]
    public EntityUid? PlayingStream;

    /// <summary>
    /// Next time to update volume.
    /// </summary>
    public TimeSpan NextUpdate;

    /// <summary>
    /// Current calculated volume (for smooth transitions).
    /// </summary>
    public float CurrentVolume;
}
