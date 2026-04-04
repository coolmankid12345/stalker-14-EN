using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Stalker.PersistentCrafting;

[Prototype("persistentCraftCategory"), Serializable, NetSerializable]
public sealed partial class PersistentCraftCategoryPrototype : IPrototype
{
    [ViewVariables]
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField("name", required: true)]
    public string Name = string.Empty;

    [DataField("order")]
    public int Order = 99;
}

[Prototype("persistentCraftSubCategory"), Serializable, NetSerializable]
public sealed partial class PersistentCraftSubCategoryPrototype : IPrototype
{
    [ViewVariables]
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField("category")]
    public string? Category;

    [DataField("name", required: true)]
    public string Name = string.Empty;

    [DataField("order")]
    public int Order = 99;
}
