using Content.Shared._Stalker.Shop;
using Content.Shared._Stalker.Shop.Prototypes;
using Content.Shared._Stalker_EN.Shop;
using Content.Shared._Stalker_EN.Shop.Buyback; // stalker-changes-en: buyback system
using JetBrains.Annotations;

namespace Content.Client._Stalker.Shop.Ui;

/// <summary>
/// Stalker shops BUI to handle events raising and send data to server.
/// </summary>
[UsedImplicitly]
public sealed class ShopBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private ShopMenu? _menu;

    public ShopBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _menu = new ShopMenu();
        _menu.OpenCentered();

        _menu.OnClose += () =>
        {
            SendMessage(new ShopClosedMessage());
            Close();
        };

        _menu.OnCategoryButtonPressed += (_, category) =>
        {
            _menu.CurrentCategory = category;
            SendMessage(new ShopRequestUpdateInterfaceMessage());
        };


        _menu.OnListingButtonPressed += (_, listing, sell, balance, count) =>
        {
            // stalker-changes-en: route buyback purchases to the dedicated message
            if (!sell && listing.ID != null && listing.ID.StartsWith("st-buyback-"))
            {
                var buybackId = listing.ID.Substring("st-buyback-".Length);
                SendMessage(new STBuybackPurchaseMessage(buybackId, balance));
                return;
            }

            switch (sell)
            {
                case false:
                    // stalker-14-en: bulk buy sends a separate message with quantity
                    if (count != null && count > 1)
                        SendMessage(new STShopBulkBuyMessage(listing, balance, count.Value));
                    else
                        SendMessage(new ShopRequestBuyMessage(listing, balance));
                    break;

                default:
                    if (count == null)
                        return;
                    SendMessage(new ShopRequestSellMessage(listing, balance, count.Value));
                    break;
            }
        };
        _menu.OnRefreshButtonPressed += () =>
        {
            SendMessage(new ShopRequestUpdateInterfaceMessage());
        };
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_menu is not { } menu)
            return;

        // Place for menu updates
        switch (state)
        {
            // TODO: It makes sense to update either only the balance, or only the category, etc.
            //
            // I just looked over the code, and i think its unnecessary,
            // because of buying/selling requires to update both, balance and listings like
            // reducing amount of item or removing listing from category at all
            case ShopUpdateState msg:
                // cringe
                var categories = new List<CategoryInfo>();
                categories.AddRange(msg.Categories);
                if (msg.SponsorCategories != null)
                    categories.AddRange(msg.SponsorCategories);
                if (msg.ContribCategories != null)
                    categories.AddRange(msg.ContribCategories);
                if (msg.PersonalCategories != null)
                    categories.AddRange(msg.PersonalCategories);

                menu.UpdateBalance(msg.Balance, msg.MoneyId, msg.LocMoneyId);
                menu.PopulateStoreCategoryButtons(categories, msg.UserItems);
                menu.UpdateListing(categories, msg.UserItems);
                break;
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        _menu?.Close();
        _menu?.Dispose();
    }
}
