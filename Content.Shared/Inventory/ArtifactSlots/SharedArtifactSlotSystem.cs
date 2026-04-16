using System.Linq;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory.Events;

namespace Content.Shared.Inventory.ArtifactSlots;

/// <summary>
/// Manages dynamic artifact slot availability based on equipped items
/// that have <see cref="GrantsArtifactSlotsComponent"/>.
/// </summary>
public sealed class SharedArtifactSlotSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    private const string ArtifactSlotPrefix = "artifact";

    public override void Initialize()
    {
        SubscribeLocalEvent<GrantsArtifactSlotsComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<GrantsArtifactSlotsComponent, GotUnequippedEvent>(OnUnequipped);
    }

    private void OnEquipped(Entity<GrantsArtifactSlotsComponent> ent, ref GotEquippedEvent args)
    {
        if (!TryComp<InventoryComponent>(args.Equipee, out var inv))
            return;

        RecalculateActiveSlots(args.Equipee, inv);
    }

    private void OnUnequipped(Entity<GrantsArtifactSlotsComponent> ent, ref GotUnequippedEvent args)
    {
        if (!TryComp<InventoryComponent>(args.Equipee, out var inv))
            return;

        RecalculateActiveSlots(args.Equipee, inv);
    }

    /// <summary>
    /// Recalculates the active artifact slot count for a mob based on all equipped items.
    /// </summary>
    private void RecalculateActiveSlots(EntityUid uid, InventoryComponent inv)
    {
        var maxSlots = 0;
        var hasGranter = false;

        if (_inventory.TryGetContainerSlotEnumerator(uid, out var enumerator))
        {
            while (enumerator.NextItem(out var item, out var slotDef))
            {
                // Skip artifact slots themselves
                if ((slotDef.SlotFlags & SlotFlags.ARTIFACT) != 0)
                    continue;

                if (!TryComp<GrantsArtifactSlotsComponent>(item, out var grants))
                    continue;

                if (grants.Slots > maxSlots)
                {
                    maxSlots = grants.Slots;
                    hasGranter = true;
                }
            }
        }

        var activeCount = hasGranter ? maxSlots : 0;
        SetActiveSlots(uid, inv, activeCount);
    }

    /// <summary>
    /// Sets the active artifact slot count and drops items from deactivated slots.
    /// </summary>
    private void SetActiveSlots(EntityUid uid, InventoryComponent inv, int activeCount)
    {
        var artifactSlots = EnsureComp<ArtifactSlotsComponent>(uid);
        var oldCount = artifactSlots.ActiveCount;
        artifactSlots.ActiveCount = activeCount;
        Dirty(uid, artifactSlots);

        // Drop items from slots that are being deactivated
        if (activeCount < oldCount)
        {
            DropFromDeactivatedSlots(uid, inv, activeCount);
        }
    }

    /// <summary>
    /// Drops contents from artifact slots that exceed the active count.
    /// Tries to pick up items into hands first, otherwise drops them on the floor.
    /// </summary>
    private void DropFromDeactivatedSlots(EntityUid uid, InventoryComponent inv, int activeCount)
    {
        var sortedSlots = GetArtifactSlotsSorted(inv);

        for (int i = activeCount; i < sortedSlots.Count; i++)
        {
            var slotName = sortedSlots[i];
            var containerIdx = Array.FindIndex(inv.Containers, c => c.ID == slotName);
            if (containerIdx < 0)
                continue;

            var container = inv.Containers[containerIdx];
            foreach (var contained in container.ContainedEntities.ToList())
            {
                // Try to pick up into hand first
                if (_hands.TryPickupAnyHand(uid, contained))
                    continue;

                // Otherwise drop next to the entity
                _transform.DropNextTo(contained, uid);
            }
        }
    }

    /// <summary>
    /// Returns artifact slot names sorted by their numeric suffix (artifact1, artifact2, ...).
    /// </summary>
    public List<string> GetArtifactSlotsSorted(InventoryComponent inv)
    {
        var result = new List<string>();

        foreach (var slot in inv.Slots)
        {
            if ((slot.SlotFlags & SlotFlags.ARTIFACT) != 0)
                result.Add(slot.Name);
        }

        result.Sort((a, b) =>
        {
            var ai = int.TryParse(a.AsSpan(ArtifactSlotPrefix.Length), out var av) ? av : int.MaxValue;
            var bi = int.TryParse(b.AsSpan(ArtifactSlotPrefix.Length), out var bv) ? bv : int.MaxValue;
            return ai.CompareTo(bi);
        });

        return result;
    }

    /// <summary>
    /// Gets the current active artifact slot count for a mob.
    /// Returns 0 if no ArtifactSlotsComponent exists.
    /// </summary>
    public int GetActiveCount(EntityUid uid, ArtifactSlotsComponent? artifactSlots = null)
    {
        if (!Resolve(uid, ref artifactSlots, false))
            return 0;

        return artifactSlots.ActiveCount;
    }

    /// <summary>
    /// Checks whether a specific artifact slot is currently active on the entity.
    /// Returns false if the slot is not an artifact slot or if its index >= ActiveCount.
    /// </summary>
    public bool IsArtifactSlotActive(EntityUid uid, string slotName, InventoryComponent inv, ArtifactSlotsComponent? artifactSlots = null)
    {
        if (!Resolve(uid, ref artifactSlots, false))
            return false;

        if (artifactSlots.ActiveCount <= 0)
            return false;

        var sortedSlots = GetArtifactSlotsSorted(inv);
        var index = sortedSlots.IndexOf(slotName);
        if (index < 0)
            return false;

        return index < artifactSlots.ActiveCount;
    }
}
