using Robust.Shared.Serialization;

namespace Content.Shared._Stalker_EN.Shop.Buyback;

/// <summary>
/// BUI message sent from client to server when a player wants to repurchase
/// an item from the buyback list.
/// </summary>
[Serializable, NetSerializable]
public sealed class STBuybackPurchaseMessage : BoundUserInterfaceMessage
{
    /// <summary>
    /// The unique ID of the buyback entry to repurchase.
    /// </summary>
    public string BuybackEntryId;

    /// <summary>
    /// The client's current balance at time of request.
    /// </summary>
    public int Balance;

    public STBuybackPurchaseMessage(string buybackEntryId, int balance)
    {
        BuybackEntryId = buybackEntryId;
        Balance = balance;
    }
}
