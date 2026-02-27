using Robust.Shared.Prototypes;

namespace Content.Server._Stalker.ZoneArtifact.Components;

[RegisterComponent]
public sealed partial class ZoneArtifactComponent : Component
{
    [DataField]
    public EntProtoId? Anomaly;
}
