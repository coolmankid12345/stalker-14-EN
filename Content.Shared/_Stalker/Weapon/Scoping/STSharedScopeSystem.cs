using System.Numerics;
using Content.Shared._Stalker.Weapon.Module.Effects;
using Content.Shared.Actions;
using Content.Shared.Camera;
using Content.Shared.DoAfter;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Toggleable;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Containers;

namespace Content.Shared._Stalker.Weapon.Scoping;

/// <remarks>
/// Portions of this file are derived from the RMC-14 project, specifically from
/// https://github.com/RMC-14/RMC-14/tree/481a21c95148f5a7bff6ed1609324c836663ca30/Content.Shared/_RMC14/Scoping.
/// These files have been modified for use in this project.
/// The original code is licensed under the MIT License:
/// </remarks>
public abstract partial class STSharedScopeSystem : EntitySystem
{
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedContentEyeSystem _contentEye = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;

    public override void Initialize()
    {
        InitializeUser();

        SubscribeLocalEvent<ScopeComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ScopeComponent, ComponentRemove>(OnShutdown);
        SubscribeLocalEvent<ScopeComponent, EntityTerminatingEvent>(OnScopeEntityTerminating);
        SubscribeLocalEvent<ScopeComponent, GotUnequippedHandEvent>(OnUnequip);
        SubscribeLocalEvent<ScopeComponent, HandDeselectedEvent>(OnDeselectHand);
        SubscribeLocalEvent<ScopeComponent, ItemUnwieldedEvent>(OnUnwielded);
        SubscribeLocalEvent<ScopeComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<ScopeComponent, ToggleActionEvent>(OnToggleAction);
        SubscribeLocalEvent<ScopeComponent, ActivateInWorldEvent>(OnActivateInWorld);
        SubscribeLocalEvent<ScopeComponent, GunShotEvent>(OnGunShot);
        SubscribeLocalEvent<ScopeComponent, ScopeDoAfterEvent>(OnScopeDoAfter);

        SubscribeLocalEvent<GunScopingComponent, GotUnequippedHandEvent>(OnGunUnequip);
        SubscribeLocalEvent<GunScopingComponent, HandDeselectedEvent>(OnGunDeselectHand);
        SubscribeLocalEvent<GunScopingComponent, ItemUnwieldedEvent>(OnGunUnwielded);
        SubscribeLocalEvent<GunScopingComponent, GunShotEvent>(OnGunGunShot);
    }

    public void TrySet(Entity<ScopeComponent?> entity, STWeaponModuleScopeEffect? effect)
    {
        if (effect is not { } scopeEffect)
        {
            RemCompDeferred<ScopeComponent>(entity);
            return;
        }

        var isNewComponent = !TryComp<ScopeComponent>(entity, out var comp);

        if (isNewComponent)
            comp = EnsureComp<ScopeComponent>(entity);

        // comp is guaranteed non-null after above logic
        comp!.Zoom = scopeEffect.Zoom;
        comp.AllowMovement = scopeEffect.AllowMovement;
        comp.Offset = scopeEffect.Offset;
        comp.Delay = scopeEffect.Delay;
        comp.RequireWielding = scopeEffect.RequireWielding;
        comp.UseInHand = scopeEffect.UseInHand;

        // Ensure action is created when component is dynamically added
        // (MapInitEvent doesn't fire for existing entities)
        if (isNewComponent)
        {
            _actionContainer.EnsureAction(entity.Owner, ref comp.ScopingToggleActionEntity, comp.ScopingToggleAction);

            // If weapon is currently held, grant action to holder immediately
            // (GetItemActionsEvent won't fire since item is already equipped)
            if (comp.ScopingToggleActionEntity is { } actionEntity &&
                _container.TryGetContainingContainer((entity.Owner, null), out var container) &&
                _hands.IsHolding(container.Owner, entity.Owner))
            {
                _actionsSystem.AddAction(container.Owner, actionEntity, entity.Owner);
            }
        }

        Dirty(entity.Owner, comp);
    }

    private void OnMapInit(Entity<ScopeComponent> ent, ref MapInitEvent args)
    {
        _actionContainer.EnsureAction(ent.Owner, ref ent.Comp.ScopingToggleActionEntity, ent.Comp.ScopingToggleAction);
        Dirty(ent.Owner, ent.Comp);
    }

    private void OnShutdown(Entity<ScopeComponent> ent, ref ComponentRemove args)
    {
        // If someone is using the scope, stop them first
        if (ent.Comp.User is { } user)
        {
            Unscope(ent);
            _actionsSystem.RemoveProvidedActions(user, ent.Owner);
        }

        // ALWAYS delete the action entity when component is removed
        // This prevents orphaned actions that cause issues on re-attach
        if (ent.Comp.ScopingToggleActionEntity is { } actionEntity)
        {
            Del(actionEntity);
            ent.Comp.ScopingToggleActionEntity = null;
        }
    }

    private void OnScopeEntityTerminating(Entity<ScopeComponent> ent, ref EntityTerminatingEvent args)
    {
        Unscope(ent);
    }

    private void OnUnequip(Entity<ScopeComponent> ent, ref GotUnequippedHandEvent args)
    {
        Unscope(ent);
    }

    private void OnDeselectHand(Entity<ScopeComponent> ent, ref HandDeselectedEvent args)
    {
        Unscope(ent);
    }

    private void OnUnwielded(Entity<ScopeComponent> ent, ref ItemUnwieldedEvent args)
    {
        if (ent.Comp.RequireWielding)
            Unscope(ent);
    }

    private void OnGetActions(Entity<ScopeComponent> ent, ref GetItemActionsEvent args)
    {
        args.AddAction(ref ent.Comp.ScopingToggleActionEntity, ent.Comp.ScopingToggleAction);
    }

    private void OnToggleAction(Entity<ScopeComponent> ent, ref ToggleActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        ToggleScoping(ent, args.Performer);
    }

    private void OnActivateInWorld(Entity<ScopeComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !ent.Comp.UseInHand)
            return;

        args.Handled = true;
        ToggleScoping(ent, args.User);
    }

    private void OnGunShot(Entity<ScopeComponent> ent, ref GunShotEvent args)
    {
        var dir = Transform(args.User).LocalRotation.GetCardinalDir();
        if (ent.Comp.ScopingDirection != dir)
            Unscope(ent);
    }

    private void OnScopeDoAfter(Entity<ScopeComponent> ent, ref ScopeDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (args.Cancelled)
        {
            DeleteRelay(ent, args.User);
            return;
        }

        var user = args.User;
        if (!CanScopePopup(ent, user))
        {
            DeleteRelay(ent, args.User);
            return;
        }

        Scope(ent, user, args.Direction);
    }

    private void OnGunUnequip(Entity<GunScopingComponent> ent, ref GotUnequippedHandEvent args)
    {
        UnscopeGun(ent);
    }

    private void OnGunDeselectHand(Entity<GunScopingComponent> ent, ref HandDeselectedEvent args)
    {
        UnscopeGun(ent);
    }

    private void OnGunUnwielded(Entity<GunScopingComponent> ent, ref ItemUnwieldedEvent args)
    {
        UnscopeGun(ent);
    }

    private void OnGunGunShot(Entity<GunScopingComponent> ent, ref GunShotEvent args)
    {
        var dir = Transform(args.User).LocalRotation.GetCardinalDir();
        if (TryComp(ent.Comp.Scope, out ScopeComponent? scope) && scope.ScopingDirection != dir)
            UnscopeGun(ent);
    }

    private bool CanScopePopup(Entity<ScopeComponent> scope, EntityUid user)
    {
        var ent = scope.Owner;
        if (!_hands.TryGetActiveItem(user, out var heldItem) || heldItem != scope.Owner)
        {
            var msgError = Loc.GetString("st-action-popup-scoping-user-must-hold", ("scope", ent));
            _popup.PopupEntity(msgError, user);
            return false;
        }

        if (_pulling.IsPulled(user))
        {
            var msgError = Loc.GetString("st-action-popup-scoping-user-must-not-pulled", ("scope", ent));
            _popup.PopupEntity(msgError, user);
            return false;
        }

        if (_container.IsEntityInContainer(user))
        {
            var msgError = Loc.GetString("st-action-popup-scoping-user-must-not-contained", ("scope", ent));
            _popup.PopupEntity(msgError, user);
            return false;
        }

        if (scope.Comp.RequireWielding &&
            TryComp(ent, out WieldableComponent? wieldable) &&
            !wieldable.Wielded)
        {
            var msgError = Loc.GetString("st-action-popup-scoping-user-must-wield", ("scope", ent));
            _popup.PopupEntity(msgError, user);
            return false;
        }

        return true;
    }

    protected virtual Direction? StartScoping(Entity<ScopeComponent> scope, EntityUid user)
    {
        if (!CanScopePopup(scope, user))
            return null;

        // TODO RMC14 make this work properly with rotations
        var xform = Transform(user);
        var cardinalDir = xform.LocalRotation.GetCardinalDir();
        var ev = new ScopeDoAfterEvent(cardinalDir);
        var doAfter = new DoAfterArgs(EntityManager, user, scope.Comp.Delay, ev, scope, null, scope)
        {
            BreakOnMove = !scope.Comp.AllowMovement
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            return cardinalDir;

        return null;
    }

    private void Scope(Entity<ScopeComponent> scope, EntityUid user, Direction direction)
    {
        if (TryComp(user, out ScopingComponent? scoping))
            UserStopScoping((user, scoping));

        scope.Comp.User = user;
        scope.Comp.ScopingDirection = direction;

        Dirty(scope);

        scoping = EnsureComp<ScopingComponent>(user);
        scoping.Scope = scope;
        scoping.AllowMovement = scope.Comp.AllowMovement;
        Dirty(user, scoping);

        // MoveInputEvent only fires on input state change, so if the player begins
        // holding movement keys near the end of the DoAfter, the event fires before
        // ScopingComponent exists and the movement goes undetected.
        if (!scoping.AllowMovement &&
            TryComp<InputMoverComponent>(user, out var mover) &&
            (mover.HeldMoveButtons & MoveButtons.AnyDirection) != MoveButtons.None)
        {
            UserStopScoping((user, scoping));
            return;
        }

        var targetOffset = GetScopeOffset(scope, direction);
        scoping.EyeOffset = targetOffset;

        //var msgUser = Loc.GetString("st-action-popup-scoping-user", ("scope", scope.Owner));
        //_popup.PopupEntity(msgUser, user);

        _actionsSystem.SetToggled(scope.Comp.ScopingToggleActionEntity, true);
        _contentEye.SetZoom(user, Vector2.One * scope.Comp.Zoom, true);
        UpdateOffset(user);
    }

    protected virtual bool Unscope(Entity<ScopeComponent> scope)
    {
        if (scope.Comp.User is not { } user)
            return false;

        RemCompDeferred<ScopingComponent>(user);

        scope.Comp.User = null;
        scope.Comp.ScopingDirection = null;
        Dirty(scope);

        var msgUser = Loc.GetString("st-action-popup-scoping-stopping-user", ("scope", scope.Owner));
        _popup.PopupClient(msgUser, user, user);

        _actionsSystem.SetToggled(scope.Comp.ScopingToggleActionEntity, false);
        _contentEye.ResetZoom(user);
        return true;
    }

    private void UnscopeGun(Entity<GunScopingComponent> gun)
    {
        if (TryComp(gun.Comp.Scope, out ScopeComponent? scope))
            Unscope((gun.Comp.Scope.Value, scope));
    }

    private void ToggleScoping(Entity<ScopeComponent> scope, EntityUid user)
    {
        if (HasComp<ScopingComponent>(user))
        {
            Unscope(scope);

            if (TryComp(user, out ScopingComponent? scoping))
                UserStopScoping((user, scoping));

            return;
        }

        StartScoping(scope, user);
    }

    protected Vector2 GetScopeOffset(Entity<ScopeComponent> scope, Direction direction)
    {
        return direction.ToVec() * ((scope.Comp.Offset * scope.Comp.Zoom - 1) / 2);
    }

    protected virtual void DeleteRelay(Entity<ScopeComponent> scope, EntityUid? user)
    {
    }

    private void UpdateOffset(EntityUid user)
    {
        var ev = new GetEyeOffsetEvent();
        RaiseLocalEvent(user, ref ev);
        _eye.SetOffset(user, ev.Offset);
    }
}
