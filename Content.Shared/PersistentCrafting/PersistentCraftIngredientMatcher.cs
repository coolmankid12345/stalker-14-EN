using Content.Shared.Stacks;
using Robust.Shared.GameObjects;

namespace Content.Shared._Stalker.PersistentCrafting;

public sealed class PersistentCraftIngredientMatcher
{
    private readonly IEntityManager _entityManager;

    public PersistentCraftIngredientMatcher(IEntityManager entityManager)
    {
        _entityManager = entityManager;
    }

    public int GetUsableAmount(EntityUid entity)
    {
        return _entityManager.TryGetComponent(entity, out StackComponent? stack) ? stack.Count : 1;
    }
}
