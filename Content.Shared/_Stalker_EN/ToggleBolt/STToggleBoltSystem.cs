using Content.Shared.ActionBlocker;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Input;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Input.Binding;
using Robust.Shared.Player;

namespace Content.Shared._Stalker_EN.ToggleBolt;

/// <summary>
/// Handles the T key toggle bolt keybind. Toggles the bolt open/closed on the gun
/// currently held in the player's active hand.
/// </summary>
public sealed class STToggleBoltSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;

    public override void Initialize()
    {
        base.Initialize();

        CommandBinds.Builder
            .Bind(ContentKeyFunctions.STToggleBolt,
                InputCmdHandler.FromDelegate(HandleToggleBolt, handle: false, outsidePrediction: false))
            .Register<STToggleBoltSystem>();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        CommandBinds.Unregister<STToggleBoltSystem>();
    }

    private void HandleToggleBolt(ICommonSession? session)
    {
        if (session?.AttachedEntity is not { Valid: true } user || !Exists(user))
            return;

        if (!_hands.TryGetActiveItem(user, out var held))
            return;

        if (!_actionBlocker.CanInteract(user, null))
            return;

        if (!TryComp<ChamberMagazineAmmoProviderComponent>(held.Value, out var chamber))
            return;

        if (chamber.BoltClosed == null)
            return;

        _gun.ToggleBolt(held.Value, chamber, user);
    }
}
