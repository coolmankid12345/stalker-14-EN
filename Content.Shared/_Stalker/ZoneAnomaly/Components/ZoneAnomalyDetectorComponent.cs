using Robust.Shared.Audio;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Stalker.ZoneAnomaly.Components;

[RegisterComponent]
public sealed partial class ZoneAnomalyDetectorComponent : Component
{
    [DataField]
    public int Level;

    [DataField]
    public float Distance = 10f;

    [DataField]
    public bool Enabled;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan MaxBeepInterval = TimeSpan.FromSeconds(2.5f);

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan MinBeepInterval = TimeSpan.FromSeconds(0.05f);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan NextBeepTime;

    [DataField]
    public SoundSpecifier BeepSound = new SoundPathSpecifier("/Audio/Items/locator_beep.ogg");

    /// <summary>
    /// Whether the detector can be toggled on/off via standard interactions (use in hand, etc).
    /// Set to false when the entity also has an ActivatableUI to prevent the toggle from
    /// consuming the interaction event before the UI system can open.
    /// </summary>
    [DataField]
    public bool ToggleOnInteract = true; // stalker-changes
}

[RegisterComponent]
public sealed partial class ZoneAnomalyDetectorArtifactActivatorComponent : Component
{
    [DataField]
    public int Level;
}

[Serializable, NetSerializable]
public enum ZoneAnomalyDetectorVisuals : byte
{
    Enabled,
    Layer,
}
