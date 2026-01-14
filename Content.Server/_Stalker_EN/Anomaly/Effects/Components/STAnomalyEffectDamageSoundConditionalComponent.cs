using Robust.Shared.Audio;

namespace Content.Server._Stalker_EN.Anomaly.Effects.Components;

/// <summary>
/// Plays different sounds based on damage type and target weight.
/// </summary>
[RegisterComponent]
public sealed partial class STAnomalyEffectDamageSoundConditionalComponent : Component
{
    /// <summary>
    /// Sound to play whenever walking through the anomaly (always plays on trigger).
    /// </summary>
    [DataField]
    public SoundSpecifier? PassthroughSound;

    /// <summary>
    /// Weight threshold for weight bonus sound (kg).
    /// </summary>
    [DataField]
    public float WeightThreshold = 100f;

    /// <summary>
    /// Sound to play for base damage.
    /// </summary>
    [DataField]
    public SoundSpecifier? BaseDamageSound;

    /// <summary>
    /// Sound to play for double damage (no boots).
    /// </summary>
    [DataField]
    public SoundSpecifier? DoubleDamageSound;

    /// <summary>
    /// Sound to play for weight bonus damage. Replaces other sounds.
    /// </summary>
    [DataField]
    public SoundSpecifier? WeightBonusSound;

    /// <summary>
    /// Trigger group name for base damage.
    /// </summary>
    [DataField]
    public string BaseDamageGroup = "StateActiveBase";

    /// <summary>
    /// Trigger group name for double damage.
    /// </summary>
    [DataField]
    public string DoubleDamageGroup = "StateActiveDouble";

    /// <summary>
    /// Range to search for entities.
    /// </summary>
    [DataField]
    public float Range = 0.5f;
}
