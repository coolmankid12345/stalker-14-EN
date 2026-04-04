using Content.Shared.Actions;
using Robust.Shared.Serialization;

namespace Content.Shared._Stalker.PersistentCrafting;

public sealed partial class OpenPersistentCraftMenuActionEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed class OpenPersistentCraftMenuEvent : EntityEventArgs
{
}

[Serializable, NetSerializable]
public sealed class RequestPersistentCraftRecipeEvent : EntityEventArgs
{
    public string RecipeId { get; }

    public RequestPersistentCraftRecipeEvent(string recipeId)
    {
        RecipeId = recipeId;
    }
}
