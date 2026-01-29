using Robust.Shared.GameStates;
using Robust.Shared.Maths;
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
    /// <summary>
    /// Default frequency used when no frequency is set or when an invalid frequency is provided.
    /// Format: "000.0" (3 digits, dot, 1 digit)
    /// </summary>
    public const string DefaultFrequency = "120.0";

    /// <summary>
    /// Color used for radio messages in chat. Amber/orange evokes old radio displays
    /// and fits the Stalker aesthetic without being faction-specific.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Color ChatColor = Color.FromHex("#D4A017");

    [DataField]
    public EntProtoId ToggleMicAction = "ActionSTRadioToggleMic";

    [DataField, AutoNetworkedField, ViewVariables]
    public EntityUid? ToggleMicActionEntity;
}
