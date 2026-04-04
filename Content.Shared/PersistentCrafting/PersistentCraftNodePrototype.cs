using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Stalker.PersistentCrafting;

[Prototype("persistentCraftNode"), Serializable, NetSerializable]
public sealed partial class PersistentCraftNodePrototype : IPrototype
{
    [ViewVariables]
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField("name")]
    public string Name = string.Empty;

    [DataField("description")]
    public string Description = string.Empty;

    [DataField("branch", required: true)]
    public string Branch = string.Empty;

    [DataField("cost")]
    public int Cost = 1;

    [DataField("displayProto")]
    public string? DisplayProto;

    [DataField("treeColumn")]
    public int TreeColumn = -1;

    [DataField("treeRow")]
    public int TreeRow = -1;

    [DataField("prerequisites")]
    public List<string> Prerequisites = new();
}
