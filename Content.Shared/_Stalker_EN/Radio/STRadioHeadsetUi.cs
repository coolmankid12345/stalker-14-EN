using Robust.Shared.Serialization;

namespace Content.Shared._Stalker_EN.Radio;

/// <summary>
/// UI key for the stalker radio headset interface.
/// Separate from RadioStalkerUiKey to avoid conflicts with handheld radios.
/// </summary>
[Serializable, NetSerializable]
public enum STRadioHeadsetUiKey
{
    Key,
}

/// <summary>
/// UI state for the stalker radio headset.
/// Only includes mic toggle and frequency - speaker is always active when equipped.
/// </summary>
[Serializable, NetSerializable]
public sealed class STRadioHeadsetBoundUIState : BoundUserInterfaceState
{
    public readonly bool MicEnabled;
    public readonly string? CurrentFrequency;

    public STRadioHeadsetBoundUIState(bool micEnabled, string? currentFrequency)
    {
        MicEnabled = micEnabled;
        CurrentFrequency = currentFrequency;
    }
}

/// <summary>
/// Message sent from client to server to toggle the headset microphone.
/// </summary>
[Serializable, NetSerializable]
public sealed class STRadioHeadsetToggleMicMessage : BoundUserInterfaceMessage
{
    public bool Enabled;

    public STRadioHeadsetToggleMicMessage(bool enabled)
    {
        Enabled = enabled;
    }
}

/// <summary>
/// Message sent from client to server to change the headset frequency.
/// </summary>
[Serializable, NetSerializable]
public sealed class STRadioHeadsetSelectFrequencyMessage : BoundUserInterfaceMessage
{
    public string Frequency;

    public STRadioHeadsetSelectFrequencyMessage(string frequency)
    {
        Frequency = frequency;
    }
}
