using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Stalker.PersistentCrafting;

[Prototype("persistentCraftRecipe"), Serializable, NetSerializable]
public sealed partial class PersistentCraftRecipePrototype : IPrototype
{
    [ViewVariables]
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField("name", required: true)]
    public string Name = string.Empty;

    [DataField("description", required: true)]
    public string Description = string.Empty;

    [DataField("displayProto")]
    public string? DisplayProto;

    [DataField("branch", required: true)]
    public string Branch = string.Empty;

    [DataField("tier", required: true)]
    public int Tier = 1;

    [DataField("requiredNode", required: true)]
    public string RequiredNode = string.Empty;

    [DataField("category")]
    public string? Category;

    [DataField("subCategory")]
    public string? SubCategory;

    [DataField("craftTime")]
    public float CraftTime = 2f;

    [DataField("pointReward")]
    public int PointReward;

    [DataField("ingredients", required: true)]
    public List<PersistentCraftIngredient> Ingredients = new();

    [DataField("results", required: true)]
    public List<PersistentCraftResult> Results = new();
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class PersistentCraftIngredient
{
    [DataField("proto")]
    public string? Proto;

    [DataField("stackType")]
    public string? StackType;

    [DataField("tag")]
    public string? Tag;

    [DataField("amount")]
    public int Amount = 1;

    public PersistentCraftIngredientSelectorKind GetSelectorKind()
    {
        var hasProto = !string.IsNullOrWhiteSpace(Proto);
        var hasStackType = !string.IsNullOrWhiteSpace(StackType);
        var hasTag = !string.IsNullOrWhiteSpace(Tag);

        var selectorCount = 0;
        if (hasProto)
            selectorCount++;
        if (hasStackType)
            selectorCount++;
        if (hasTag)
            selectorCount++;

        if (selectorCount == 0)
            return PersistentCraftIngredientSelectorKind.None;

        if (selectorCount > 1)
            return PersistentCraftIngredientSelectorKind.InvalidMultiple;

        if (hasProto)
            return PersistentCraftIngredientSelectorKind.Proto;

        if (hasStackType)
            return PersistentCraftIngredientSelectorKind.StackType;

        return PersistentCraftIngredientSelectorKind.Tag;
    }

    public string GetSelectorValue()
    {
        return GetSelectorKind() switch
        {
            PersistentCraftIngredientSelectorKind.Proto => Proto ?? string.Empty,
            PersistentCraftIngredientSelectorKind.StackType => StackType ?? string.Empty,
            PersistentCraftIngredientSelectorKind.Tag => Tag ?? string.Empty,
            _ => string.Empty,
        };
    }
}

[Serializable, NetSerializable]
public enum PersistentCraftIngredientSelectorKind : byte
{
    None = 0,
    Proto = 1,
    StackType = 2,
    Tag = 3,
    InvalidMultiple = 4,
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class PersistentCraftResult
{
    [DataField("proto", required: true)]
    public string Proto = string.Empty;

    [DataField("amount")]
    public int Amount = 1;
}
