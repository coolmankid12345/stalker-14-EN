using Content.Shared._Stalker.Shop;
using Content.Shared._Stalker.Shop.Prototypes;
using Content.Shared._Stalker_EN.Shop.Buyback;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking;
using Content.Shared.Store;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Stalker.Shop;

/// <summary>
/// Handles the buyback system: when players sell items, they can repurchase them
/// from a "Buyback" category at a configurable markup.
/// </summary>
public sealed partial class ShopSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    private const string BuybackIdPrefix = "st-buyback-";
    private const string BuybackCategoryLocId = "st-shop-buyback-category";

    private void InitializeBuyback()
    {
        SubscribeLocalEvent<ShopComponent, STBuybackPurchaseMessage>(OnBuybackPurchase);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnBuybackRoundCleanup);
    }

    private void AddBuybackEntry(
        EntityUid shop,
        EntityUid seller,
        ShopComponent component,
        string prototypeId,
        string name,
        string description,
        int perItemSellPrice,
        int count)
    {
        if (!TryComp<ActorComponent>(seller, out var actor))
            return;

        var userId = actor.PlayerSession.UserId;

        if (!component.BuybackItems.TryGetValue(userId, out var entries))
        {
            entries = new List<STBuybackEntry>();
            component.BuybackItems[userId] = entries;
        }

        var buybackPrice = (int) Math.Ceiling(perItemSellPrice * component.BuybackPriceMultiplier);
        var now = _timing.CurTime;

        for (var i = 0; i < count; i++)
        {
            var entry = new STBuybackEntry(
                Guid.NewGuid().ToString(),
                prototypeId,
                name,
                description,
                perItemSellPrice,
                buybackPrice,
                now);

            entries.Add(entry);

            while (entries.Count > component.BuybackMaxItems)
            {
                entries.RemoveAt(0);
            }
        }
    }

    private CategoryInfo? GetBuybackCategory(EntityUid user, ShopComponent component)
    {
        if (!TryComp<ActorComponent>(user, out var actor))
            return null;

        var userId = actor.PlayerSession.UserId;

        if (!component.BuybackItems.TryGetValue(userId, out var entries) || entries.Count == 0)
            return null;

        var category = new CategoryInfo
        {
            Name = BuybackCategoryLocId,
            Priority = 999,
        };

        foreach (var entry in entries)
        {
            var listing = new ListingData(
                name: entry.Name,
                discountCategory: null,
                description: entry.Description,
                conditions: null,
                icon: null,
                priority: 0,
                productEntity: entry.PrototypeId,
                productAction: null,
                productUpgradeId: null,
                productActionEntity: null,
                productEvent: null,
                raiseProductEventOnUser: false,
                purchaseAmount: 0,
                id: BuybackIdPrefix + entry.Id,
                categories: new HashSet<ProtoId<StoreCategoryPrototype>>(),
                originalCost: new Dictionary<ProtoId<CurrencyPrototype>, FixedPoint2>
                {
                    [component.MoneyId] = entry.BuybackPrice,
                },
                restockTime: TimeSpan.Zero,
                dataDiscountDownTo: new Dictionary<ProtoId<CurrencyPrototype>, FixedPoint2>(),
                disableRefund: true,
                count: 1);

            category.ListingItems.Add(listing);
        }

        return category;
    }

    private void OnBuybackPurchase(EntityUid uid, ShopComponent component, STBuybackPurchaseMessage msg)
    {
        if (msg.Actor is not { Valid: true } buyer)
            return;

        if (!TryComp<ActorComponent>(buyer, out var actor))
            return;

        var userId = actor.PlayerSession.UserId;

        if (!component.BuybackItems.TryGetValue(userId, out var entries))
            return;

        STBuybackEntry? targetEntry = null;
        var targetIndex = -1;
        for (var i = 0; i < entries.Count; i++)
        {
            if (entries[i].Id == msg.BuybackEntryId)
            {
                targetEntry = entries[i];
                targetIndex = i;
                break;
            }
        }

        if (targetEntry == null || targetIndex < 0)
            return;

        var balance = GetMoneyFromList(GetContainersElements(buyer), component);
        if (balance < targetEntry.BuybackPrice)
            return;

        entries.RemoveAt(targetIndex);
        SubtractBalance(buyer, component, targetEntry.BuybackPrice);

        var product = Spawn(targetEntry.PrototypeId, Transform(buyer).Coordinates);
        _hands.PickupOrDrop(buyer, product);

        var newBalance = GetMoneyFromList(GetContainersElements(buyer), component);
        component.CurrentBalance = newBalance;
        UpdateShopUI(buyer, uid, newBalance, component);
    }

    private void OnBuybackRoundCleanup(RoundRestartCleanupEvent ev)
    {
        var query = EntityQueryEnumerator<ShopComponent>();
        while (query.MoveNext(out _, out var component))
        {
            component.BuybackItems.Clear();
        }
    }
}
