using Robust.Shared.Serialization;

namespace Content.Shared._Stalker_EN.Emission;

/// <summary>
/// Event raised when emission state changes (starts or ends).
/// Used by systems like STMessenger to react to emission events.
/// </summary>
[ByRefEvent]
public readonly struct EmissionStateChangedEvent
{
    public readonly bool IsActive;

    public EmissionStateChangedEvent(bool isActive)
    {
        IsActive = isActive;
    }
}
