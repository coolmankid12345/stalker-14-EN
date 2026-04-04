using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Stalker.Weapons.Ranged;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class STArmorPenetrationComponent : Component
{
    [DataField("penetrationClass", required: true)]
    [AutoNetworkedField]
    public int PenetrationClass = 1;
}
