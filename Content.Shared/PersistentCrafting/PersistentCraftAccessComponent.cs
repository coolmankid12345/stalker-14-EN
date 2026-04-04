using Content.Shared.Actions.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stalker.PersistentCrafting;

[RegisterComponent]
public sealed partial class PersistentCraftAccessComponent : Component
{
    [DataField]
    public EntProtoId<InstantActionComponent> Action = "ActionOpenPersistentCraftMenu";

    [DataField]
    public EntityUid? ActionEntity;
}
