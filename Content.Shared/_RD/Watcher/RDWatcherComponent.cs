using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RD.Watcher;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class RDWatcherComponent : Component
{
    [DataField, AutoNetworkedField]
    public HashSet<EntityUid> Entities = new();

    [DataField, AutoNetworkedField]
    public List<EntProtoId> VirtualStorage = new();
}
