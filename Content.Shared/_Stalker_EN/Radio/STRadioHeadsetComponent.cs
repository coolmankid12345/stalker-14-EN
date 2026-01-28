using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stalker_EN.Radio;

/// <summary>
/// Marks an entity as a stalker radio headset that provides action bar buttons
/// for toggling mic/speaker and opening the frequency UI when equipped to the ears slot.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class STRadioHeadsetComponent : Component
{
    [DataField]
    public EntProtoId ToggleMicAction = "ActionSTRadioToggleMic";

    [DataField]
    public EntProtoId ToggleSpeakerAction = "ActionSTRadioToggleSpeaker";

    [DataField, AutoNetworkedField, ViewVariables]
    public EntityUid? ToggleMicActionEntity;

    [DataField, AutoNetworkedField, ViewVariables]
    public EntityUid? ToggleSpeakerActionEntity;
}
