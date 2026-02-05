using Content.Shared._Stalker.Shop.Prototypes;
using Content.Shared._Stalker.Sponsors;
using Content.Shared._Stalker_EN.Shop.Buyback; // stalker-changes-en: buyback system
using Robust.Shared.Network; // stalker-changes-en: buyback system
using Robust.Shared.Prototypes;

namespace Content.Shared._Stalker.Shop;

[RegisterComponent]
public sealed partial class ShopComponent : Component
{
    /// <summary>
    /// Id of currency the shop using right now
    /// This will influence on item the shop is trying to find in player's inventory
    /// </summary>
    [DataField]
    public string MoneyId = "Roubles";

    [DataField("shopPresetId")]
    public string ShopPresetPrototype = "DebugShopPreset";

    [DataField("permitId")]
    public EntProtoId? Permit = default!;

    /// <summary>
    /// Made to not renew listings on each UI update
    /// </summary>
    public List<CategoryInfo> ShopCategories = new();

    public Dictionary<ProtoId<SponsorPrototype>, List<CategoryInfo>> ShopSponsorCategories = new();

    public List<CategoryInfo> ContributorCategories = new();

    public Dictionary<string, List<CategoryInfo>> PersonalCategories = new();

    public int CurrentBalance = 0;

    // stalker-changes-en: buyback system
    /// <summary>
    /// Maximum number of buyback entries tracked per player.
    /// </summary>
    [DataField]
    public int BuybackMaxItems = 20;

    /// <summary>
    /// Price multiplier for buyback items (1.2 = 20% markup over sell price).
    /// </summary>
    [DataField]
    public float BuybackPriceMultiplier = 1.2f;

    /// <summary>
    /// Server-only: tracks buyback items per player.
    /// Not networked -- only the server needs to know the full buyback state.
    /// The client receives buyback items as a regular shop category.
    /// </summary>
    public Dictionary<NetUserId, List<STBuybackEntry>> BuybackItems = new();
}
