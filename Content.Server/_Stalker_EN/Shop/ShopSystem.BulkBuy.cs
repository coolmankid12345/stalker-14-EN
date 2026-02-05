using Content.Shared._Stalker.Shop;
using Content.Shared._Stalker_EN.Shop;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Server._Stalker.Shop;

/// <summary>
/// Partial extension of ShopSystem that handles bulk purchasing.
/// Items opt in via <see cref="STBulkBuyableComponent"/> on their prototype.
/// </summary>
public sealed partial class ShopSystem
{
    private const int MaxBulkBuyHardCap = 50;

    private void InitializeBulkBuy()
    {
        SubscribeLocalEvent<ShopComponent, STShopBulkBuyMessage>(OnBulkBuyListing);
    }

    private void OnBulkBuyListing(EntityUid uid, ShopComponent component, STShopBulkBuyMessage msg)
    {
        if (msg.Actor is not { Valid: true } buyer)
            return;

        if (msg.Count < 1)
            return;

        var listing = msg.Listing;
        if (listing.ProductEntity == null)
            return;

        if (!_proto.TryIndex<EntityPrototype>(listing.ProductEntity, out var proto))
            return;

        // Only allow bulk buy for items that explicitly opt in
        if (!proto.TryGetComponent<STBulkBuyableComponent>(out var bulkComp, _entity.ComponentFactory))
            return;

        if (!listing.OriginalCost.TryGetValue(component.MoneyId, out var unitPrice))
            return;

        if (!CheckPermit(buyer, component))
        {
            if (component.Permit.HasValue)
            {
                _proto.TryIndex(component.Permit.Value, out var permitPrototype);
                _popup.PopupEntity(Loc.GetString("st-shop-requires-permit", ("permit", permitPrototype?.Name ?? "unknown")), uid);
            }
            return;
        }

        var count = Math.Min(msg.Count, Math.Min(bulkComp.MaxQuantity, MaxBulkBuyHardCap));
        var totalCost = unitPrice.Int() * count;
        var balance = component.CurrentBalance;

        if (totalCost > balance)
            return;

        SubtractBalance(buyer, component, totalCost);
        balance -= totalCost;

        var coords = Transform(buyer).Coordinates;
        for (var i = 0; i < count; i++)
        {
            var product = Spawn(listing.ProductEntity, coords);
            _hands.PickupOrDrop(buyer, product);
        }

        listing.PurchaseAmount += count;
        component.CurrentBalance = balance;
        UpdateShopUI(buyer, uid, component.CurrentBalance, component);
    }
}
