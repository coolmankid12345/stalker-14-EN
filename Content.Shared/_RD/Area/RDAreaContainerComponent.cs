using System.Collections.Frozen;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RD.Area;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class RDAreaContainerComponent : Component
{
    [DataField, AutoNetworkedField]
    public Dictionary<Vector2i, EntProtoId<RDAreaComponent>> Areas = new();

    [ViewVariables] public FrozenDictionary<Vector2i, EntProtoId<RDAreaComponent>> CachedAreas = new Dictionary<Vector2i, EntProtoId<RDAreaComponent>>().ToFrozenDictionary();
    [ViewVariables] public bool CacheValid;
}
