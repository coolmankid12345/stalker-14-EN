using Content.Shared.ActionBlocker;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Player;

namespace Content.Shared._Stalker_EN.MagazineEject;

/// <summary>
/// Handles the R key magazine eject keybind. Ejects the magazine from the gun
/// currently held in the player's active hand.
/// </summary>
public sealed class STEjectMagazineSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;

    private const string MagazineSlot = "gun_magazine";

    public override void Initialize()
    {
        base.Initialize();

        CommandBinds.Builder
            .Bind(ContentKeyFunctions.STEjectMagazine,
                InputCmdHandler.FromDelegate(HandleEjectMagazine, handle: false, outsidePrediction: false))
            .Register<STEjectMagazineSystem>();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        CommandBinds.Unregister<STEjectMagazineSystem>();
    }

    private void HandleEjectMagazine(ICommonSession? session)
    {
        if (session?.AttachedEntity is not { Valid: true } user || !Exists(user))
            return;

        if (!_hands.TryGetActiveItem(user, out var held))
            return;

        if (!_actionBlocker.CanInteract(user, null))
            return;

        if (!_itemSlots.TryGetSlot(held.Value, MagazineSlot, out var slot))
            return;

        if (!slot.HasItem)
            return;

        _itemSlots.TryEjectToHands(held.Value, slot, user, excludeUserAudio: true);
    }
}
