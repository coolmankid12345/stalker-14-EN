namespace Content.Shared._Stalker_EN.Devices;

/// <summary>
/// Makes the owner unable to use artifact detectors.
/// </summary>
[RegisterComponent]
public sealed partial class CantUseDetectorComponent : Component
{
    /// <summary>
    /// The message given to the user when attempting to use a detector.
    /// </summary>
    [DataField]
    public string? DenialMessage;
}
