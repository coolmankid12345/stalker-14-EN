using Content.Shared.Hands.Components;
using Content.Shared.Inventory.ArtifactSlots; // Stalker-Changes

namespace Content.Shared.Inventory;

public partial class InventorySystem
{
    [Dependency] private readonly SharedArtifactSlotSystem _artifactSlots = default!; // Stalker-Changes

    public override void Initialize()
    {
        base.Initialize();
        InitializeEquip();
        InitializeRelay();
        InitializeSlots();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        ShutdownSlots();
    }
}
