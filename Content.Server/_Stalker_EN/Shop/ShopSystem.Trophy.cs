using Content.Shared._Stalker_EN.Trophy;
using Robust.Shared.Prototypes;

namespace Content.Server._Stalker.Shop;

/// <summary>
/// Partial extension for trophy sell-price resolution.
/// Walks the prototype parent chain so variant trophies inherit their
/// base item's per-shop price, then applies <see cref="STTrophyComponent.PriceMultiplier"/>.
/// </summary>
public sealed partial class ShopSystem
{
    /// <summary>
    /// Resolves a sell price by checking the shop's SellingItems for the item's
    /// prototype ID, then walking up the parent prototype chain if no direct match.
    /// Applies <see cref="STTrophyComponent.PriceMultiplier"/> when present.
    /// </summary>
    private bool TryResolveSellPrice(
        EntityUid item,
        string itemProtoId,
        Dictionary<string, int> sellingItems,
        out int price)
    {
        // Direct match
        if (sellingItems.TryGetValue(itemProtoId, out price))
        {
            ApplyTrophyMultiplier(item, ref price);
            return true;
        }

        // Walk parent chain to find a listed ancestor
        if (!_proto.TryIndex<EntityPrototype>(itemProtoId, out var current))
        {
            price = 0;
            return false;
        }

        while (current.Parents is { Length: > 0 })
        {
            var parentId = current.Parents[0];
            if (sellingItems.TryGetValue(parentId, out price))
            {
                ApplyTrophyMultiplier(item, ref price);
                return true;
            }

            if (!_proto.TryIndex<EntityPrototype>(parentId, out current))
                break;
        }

        price = 0;
        return false;
    }

    private void ApplyTrophyMultiplier(EntityUid item, ref int price)
    {
        if (TryComp<STTrophyComponent>(item, out var trophy) &&
            trophy.PriceMultiplier != 1f)
        {
            price = (int) Math.Round(price * trophy.PriceMultiplier);
        }
    }
}
