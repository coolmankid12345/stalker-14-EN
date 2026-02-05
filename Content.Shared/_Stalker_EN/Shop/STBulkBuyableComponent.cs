using Robust.Shared.GameObjects;

namespace Content.Shared._Stalker_EN.Shop;

/// <summary>
/// Opt-in component that enables bulk purchasing for shop items.
/// Items without this component use the standard single-buy flow.
/// </summary>
[RegisterComponent]
public sealed partial class STBulkBuyableComponent : Component
{
    [DataField]
    public int MaxQuantity = 20;
}
