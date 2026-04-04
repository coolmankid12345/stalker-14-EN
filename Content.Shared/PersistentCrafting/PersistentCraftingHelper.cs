using System.Text;

namespace Content.Shared._Stalker.PersistentCrafting;

public static class PersistentCraftingHelper
{
    public static string? GetDisplayPrototypeId(PersistentCraftRecipePrototype recipe)
    {
        if (!string.IsNullOrWhiteSpace(recipe.DisplayProto))
            return recipe.DisplayProto;

        return recipe.Results.Count > 0
            ? recipe.Results[0].Proto
            : null;
    }

    public static bool IsAutoUnlockedNode(PersistentCraftNodePrototype node)
    {
        return node.Cost <= 0;
    }

    public static int GetPointReward(PersistentCraftRecipePrototype recipe)
    {
        return Math.Max(0, recipe.PointReward);
    }

    public static string GetTierDisplayLabel(int tier)
    {
        return tier > 0 ? ToRoman(tier) : tier.ToString();
    }

    private static readonly (int Value, string Symbol)[] RomanNumerals =
    {
        (1000, "M"),
        (900,  "CM"),
        (500,  "D"),
        (400,  "CD"),
        (100,  "C"),
        (90,   "XC"),
        (50,   "L"),
        (40,   "XL"),
        (10,   "X"),
        (9,    "IX"),
        (5,    "V"),
        (4,    "IV"),
        (1,    "I"),
    };

    private static string ToRoman(int number)
    {
        var result = new StringBuilder();
        foreach (var (value, symbol) in RomanNumerals)
        {
            while (number >= value)
            {
                result.Append(symbol);
                number -= value;
            }
        }

        return result.ToString();
    }
}
