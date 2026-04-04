using System;

namespace Content.Shared._Stalker.PersistentCrafting;

public static class PersistentCraftRecipeRules
{
    public static float GetEffectiveCraftTime(PersistentCraftRecipePrototype recipe)
    {
        return MathF.Max(0.25f, recipe.CraftTime);
    }

    public static int GetEffectiveIngredientAmount(
        PersistentCraftRecipePrototype recipe,
        PersistentCraftIngredient ingredient)
    {
        // Параметр recipe зарезервирован для будущих модификаторов (например, скидки от навыков).
        // ReSharper disable once UnusedParameter.Global
        return Math.Max(1, ingredient.Amount);
    }
}
