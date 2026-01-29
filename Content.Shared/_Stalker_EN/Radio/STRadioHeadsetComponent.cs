using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stalker_EN.Radio;

/// <summary>
/// Marks an entity as a stalker radio headset that provides action bar button
/// for toggling mic and opening the frequency UI when equipped to the ears slot.
/// Speaker output is always active when equipped and sends messages only to the wearer.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class STRadioHeadsetComponent : Component
{
    [DataField]
    public EntProtoId ToggleMicAction = "ActionSTRadioToggleMic";

    [DataField, AutoNetworkedField, ViewVariables]
    public EntityUid? ToggleMicActionEntity;
}
