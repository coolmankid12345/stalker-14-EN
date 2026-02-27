using Content.Shared._Stalker_EN.DogTag;
using Content.Shared.GameTicking;
using Content.Shared.Inventory;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Server._Stalker_EN.DogTag;

/// <summary>
/// Stamps character identity information onto dog tags when players spawn.
/// Listens to <see cref="PlayerSpawnCompleteEvent"/> and writes the character's
/// name and age from their profile onto any dog tag in the dogtag slot.
/// </summary>
public sealed class STDogTagInfoSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly TagSystem _tags = default!;

    private const string DogtagSlot = "dogtag";
    private static readonly ProtoId<TagPrototype> DogtagTag = "Dogtag";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        if (!_inventory.TryGetSlotEntity(args.Mob, DogtagSlot, out var dogtagEntity))
            return;

        if (!_tags.HasTag(dogtagEntity.Value, DogtagTag))
            return;

        var info = EnsureComp<STDogTagInfoComponent>(dogtagEntity.Value);
        info.OwnerName = args.Profile.Name;
        info.OwnerAge = args.Profile.Age;
        Dirty(dogtagEntity.Value, info);
    }
}
