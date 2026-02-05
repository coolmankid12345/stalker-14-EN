using Robust.Shared.Serialization;

namespace Content.Shared._Stalker_EN.Shop.Buyback;

/// <summary>
/// Represents a single item available for buyback after being sold to a shop.
/// Stored per-player in the shop component's buyback dictionary.
/// </summary>
[Serializable, NetSerializable]
public sealed class STBuybackEntry
{
    /// <summary>
    /// Unique identifier for this buyback entry (GUID).
    /// </summary>
    public string Id;

    /// <summary>
    /// The entity prototype ID of the sold item.
    /// </summary>
    public string PrototypeId;

    /// <summary>
    /// Display name captured at the time of sale.
    /// </summary>
    public string Name;

    /// <summary>
    /// Description captured at the time of sale.
    /// </summary>
    public string Description;

    /// <summary>
    /// The price the player received when selling the item.
    /// </summary>
    public int OriginalSellPrice;

    /// <summary>
    /// The price to repurchase the item (OriginalSellPrice * markup, rounded up).
    /// </summary>
    public int BuybackPrice;

    /// <summary>
    /// Server time when the item was sold.
    /// </summary>
    public TimeSpan SoldAt;

    public STBuybackEntry(
        string id,
        string prototypeId,
        string name,
        string description,
        int originalSellPrice,
        int buybackPrice,
        TimeSpan soldAt)
    {
        Id = id;
        PrototypeId = prototypeId;
        Name = name;
        Description = description;
        OriginalSellPrice = originalSellPrice;
        BuybackPrice = buybackPrice;
        SoldAt = soldAt;
    }
}
