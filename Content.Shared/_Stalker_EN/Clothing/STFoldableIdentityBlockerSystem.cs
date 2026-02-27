using Content.Shared.Foldable;
using Content.Shared.IdentityManagement;
using Content.Shared.IdentityManagement.Components;
using Content.Shared.Inventory;

namespace Content.Shared._Stalker_EN.Clothing;

/// <summary>
/// Toggles <see cref="IdentityBlockerComponent.Enabled"/> when a foldable clothing item
/// is folded or unfolded. This fixes balaclavas which start with identity blocking disabled
/// (since they're worn on HEAD when folded) but need it enabled when unfolded to the MASK slot.
/// </summary>
public sealed class STFoldableIdentityBlockerSystem : EntitySystem
{
    [Dependency] private readonly IdentitySystem _identity = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<IdentityBlockerComponent, FoldedEvent>(OnFolded);
    }

    private void OnFolded(Entity<IdentityBlockerComponent> ent, ref FoldedEvent args)
    {
        // When folded (e.g., balaclava on HEAD) → disable identity blocking.
        // When unfolded (e.g., balaclava on MASK) → enable identity blocking.
        ent.Comp.Enabled = !args.IsFolded;
        Dirty(ent);

        // If the item is currently equipped, update the wearer's identity.
        if (_inventory.TryGetContainingSlot(ent.Owner, out _))
        {
            var wearer = Transform(ent).ParentUid;
            _identity.QueueIdentityUpdate(wearer);
        }
    }
}
