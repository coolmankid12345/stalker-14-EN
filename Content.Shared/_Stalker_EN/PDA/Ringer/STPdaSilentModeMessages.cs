using Robust.Shared.Serialization;

namespace Content.Shared._Stalker_EN.PDA.Ringer;

/// <summary>
/// Message sent from the PDA UI to toggle silent mode on/off.
/// </summary>
[Serializable, NetSerializable]
public sealed class STPdaToggleSilentModeMessage : BoundUserInterfaceMessage
{
    public STPdaToggleSilentModeMessage() { }
}
