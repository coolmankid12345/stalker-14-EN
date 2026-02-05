using Content.Shared.Store;
using Robust.Shared.Serialization;

namespace Content.Shared._Stalker_EN.Shop;

[Serializable, NetSerializable]
public sealed class STShopBulkBuyMessage : BoundUserInterfaceMessage
{
    public ListingData Listing;
    public int Balance;
    public int Count;

    public STShopBulkBuyMessage(ListingData listing, int balance, int count)
    {
        Listing = listing;
        Balance = balance;
        Count = count;
    }
}
