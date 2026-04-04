using System.Numerics;
using Content.Shared._RMC14.Weapons.Ranged.Prediction;
using Content.Shared.Administration.Logs;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Effects;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Projectiles;

public abstract partial class SharedProjectileSystem : EntitySystem
{
    public const string ProjectileFixture = "projectile";

    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly SharedColorFlashEffectSystem _color = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    [Dependency] private readonly SharedGunSystem _guns = default!;

    private readonly ISawmill _sawmill;

    public SharedProjectileSystem()
    {
        _sawmill = Logger.GetSawmill("projectile");
    }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ProjectileComponent, PreventCollideEvent>(PreventCollision);
        SubscribeLocalEvent<EmbeddableProjectileComponent, ProjectileHitEvent>(OnEmbedProjectileHit);
        SubscribeLocalEvent<EmbeddableProjectileComponent, ThrowDoHitEvent>(OnEmbedThrowDoHit);
        SubscribeLocalEvent<EmbeddableProjectileComponent, ActivateInWorldEvent>(OnEmbedActivate);
        SubscribeLocalEvent<EmbeddableProjectileComponent, RemoveEmbeddedProjectileEvent>(OnEmbedRemove);
        SubscribeLocalEvent<EmbeddableProjectileComponent, ComponentShutdown>(OnEmbeddableCompShutdown);

        SubscribeLocalEvent<EmbeddedContainerComponent, EntityTerminatingEvent>(OnEmbeddableTermination);
    }


    protected void OnStartCollide(EntityUid uid, ProjectileComponent component, ref StartCollideEvent args)
    {

        if (args.OurFixtureId != ProjectileFixture || !args.OtherFixture.Hard
                                                   || component.ProjectileSpent || component is { Weapon: null, OnlyCollideWhenShot: true })
            return;

        // With gun prediction active, the server handles hits exclusively via
        // PredictedProjectileHitEvent. Suppress direct physics collisions server-side.
        if (_net.IsServer && _guns.GunPrediction)
            return;

        ProjectileCollide((uid, component, args.OurBody), args.OtherEntity);
    }

    public void ProjectileCollide(Entity<ProjectileComponent, PhysicsComponent> projectile, EntityUid target, bool predicted = false)
{
    var (uid, component, _) = projectile;
    if (projectile.Comp1.ProjectileSpent)
    {
        if (_net.IsServer && component.DeleteOnCollide)
            Del(uid);
        return;
    }
    // With gun prediction active, the server only processes damage through the
    // PredictedProjectileHitEvent path (predicted=true). Suppress direct physics
    // collisions on the server to prevent double-damage.
    if (_net.IsServer && _guns.GunPrediction && !predicted)
        return;

    var attemptEv = new ProjectileReflectAttemptEvent(uid, component, false);
    RaiseLocalEvent(target, ref attemptEv);
    if (attemptEv.Cancelled)
    {
        SetShooter(uid, component, target);
        return;
    }

    var ev = new ProjectileHitEvent(component.Damage * _damageableSystem.UniversalProjectileDamageModifier, target, component.Shooter);
    _sawmill.Info($"[ProjectileCollide] Raising ProjectileHitEvent uid={uid} target={target} IsServer={_net.IsServer}");
    RaiseLocalEvent(uid, ref ev);
    if (ev.Handled)
        return;

    var coordinates = Transform(projectile).Coordinates;
    var otherName = ToPrettyString(target);

    var modifiedDamage = _net.IsServer
        ? _damageableSystem.ChangeDamage(target,
            ev.Damage,
            false,  // DO NOT ignore resistances — let the armor system handle it normally
            origin: component.Shooter)
        : new DamageSpecifier(ev.Damage);

    var deleted = Deleted(target);

    if (_net.IsClient)
    {
        var modifyEvent = new DamageModifyEvent(ev.Damage, component.Shooter, new List<EntityUid> { uid });
        RaiseLocalEvent(target, modifyEvent);
        modifiedDamage = modifyEvent.Damage;
    }

    var filter = Filter.Pvs(coordinates, entityMan: EntityManager);
    if (_guns.GunPrediction)
    {
        if (TryComp(projectile, out PredictedProjectileServerComponent? serverProjectile) &&
            serverProjectile.Shooter is { } shooter)
        {
            filter = filter.RemovePlayer(shooter);
        }
    }

    if (modifiedDamage != null)
    {
        EntityUid? source = null;
        if (EntityManager.EntityExists(component.Shooter))
            source = component.Shooter;
        else if (EntityManager.EntityExists(component.Weapon))
            source = component.Weapon;

        var finalTotal = modifiedDamage.GetTotal();

        if (source != null)
        {
            if (modifiedDamage.AnyPositive() && !deleted)
                _color.RaiseEffect(Color.Red, new List<EntityUid> { target }, filter);

            _adminLogger.Add(LogType.BulletHit,
                HasComp<ActorComponent>(target) ? LogImpact.Medium : LogImpact.Low,
                $"Projectile {ToPrettyString(uid):projectile} shot by {ToPrettyString(source.Value):source} hit {otherName:target} and dealt {finalTotal:damage} damage");
        }
    }

    if (!deleted && filter.Count > 0)
        _guns.PlayImpactSound(target, modifiedDamage, component.SoundHit, component.ForceSound);

    component.ProjectileSpent = true;
    Dirty(uid, component);

    if ((_net.IsServer || IsClientSide(uid)) && component.ImpactEffect != null)
    {
        var impactEffectEv = new ImpactEffectEvent(component.ImpactEffect, GetNetCoordinates(coordinates));
        if (_net.IsServer)
            RaiseNetworkEvent(impactEffectEv, filter);
        else
            RaiseLocalEvent(impactEffectEv);
    }

    if (!predicted && component.DeleteOnCollide && (_net.IsServer || IsClientSide(uid)))
    {
        // Only delete on client if NOT using gun prediction.
        // With prediction active, the server handles deletion via PredictedProjectileHitComponent,
        // so the client must not delete early or the server never gets to process the hit.
        if (_net.IsServer || !_guns.GunPrediction)
            Del(uid);
    }
    else if (_net.IsServer && component.DeleteOnCollide)
    {
        var predictedComp = EnsureComp<PredictedProjectileHitComponent>(uid);
        if (predictedComp.Origin != default)
        {
            var targetCoords = _transform.GetMoverCoordinates(target);
            if (predictedComp.Origin.TryDistance(EntityManager, _transform, targetCoords, out var distance))
                predictedComp.Distance = distance;
        }
        Dirty(uid, predictedComp);
    }
}

    private void OnEmbedActivate(Entity<EmbeddableProjectileComponent> embeddable, ref ActivateInWorldEvent args)
    {
        if (embeddable.Comp.RemovalTime == null)
            return;

        if (args.Handled || !args.Complex || !TryComp<PhysicsComponent>(embeddable, out var physics) ||
            physics.BodyType != BodyType.Static)
            return;

        args.Handled = true;

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager,
            args.User,
            embeddable.Comp.RemovalTime.Value,
            new RemoveEmbeddedProjectileEvent(),
            eventTarget: embeddable,
            target: embeddable));
    }

    private void OnEmbedRemove(Entity<EmbeddableProjectileComponent> embeddable, ref RemoveEmbeddedProjectileEvent args)
    {
        if (args.Cancelled)
            return;

        EmbedDetach(embeddable, embeddable.Comp, args.User);

        _hands.TryPickupAnyHand(args.User, embeddable);
    }

    private void OnEmbeddableCompShutdown(Entity<EmbeddableProjectileComponent> embeddable, ref ComponentShutdown arg)
    {
        EmbedDetach(embeddable, embeddable.Comp);
    }

    private void OnEmbedThrowDoHit(Entity<EmbeddableProjectileComponent> embeddable, ref ThrowDoHitEvent args)
    {
        if (!embeddable.Comp.EmbedOnThrow)
            return;

        EmbedAttach(embeddable, args.Target, null, embeddable.Comp);
    }

    private void OnEmbedProjectileHit(Entity<EmbeddableProjectileComponent> embeddable, ref ProjectileHitEvent args)
    {
        EmbedAttach(embeddable, args.Target, args.Shooter, embeddable.Comp);

        if (TryComp(embeddable, out ProjectileComponent? projectile) &&
            projectile.Shooter is { } shooter &&
            projectile.Weapon is { } weapon)
        {
            var ev = new ProjectileEmbedEvent(shooter, weapon, args.Target);
            RaiseLocalEvent(embeddable, ref ev);
        }
    }

    private void EmbedAttach(EntityUid uid, EntityUid target, EntityUid? user, EmbeddableProjectileComponent component)
    {
        TryComp<PhysicsComponent>(uid, out var physics);
        _physics.SetLinearVelocity(uid, Vector2.Zero, body: physics);
        _physics.SetBodyType(uid, BodyType.Static, body: physics);
        var xform = Transform(uid);
        _transform.SetParent(uid, xform, target);

        if (component.Offset != Vector2.Zero)
        {
            var rotation = xform.LocalRotation;
            if (TryComp<ThrowingAngleComponent>(uid, out var throwingAngleComp))
                rotation += throwingAngleComp.Angle;
            _transform.SetLocalPosition(uid, xform.LocalPosition + rotation.RotateVec(component.Offset), xform);
        }

        _audio.PlayPredicted(component.Sound, uid, null);
        component.EmbeddedIntoUid = target;
        var ev = new EmbedEvent(user, target);
        RaiseLocalEvent(uid, ref ev);
        Dirty(uid, component);

        EnsureComp<EmbeddedContainerComponent>(target, out var embeddedContainer);

        DebugTools.AssertEqual(embeddedContainer.EmbeddedObjects.Contains(uid), false);
        embeddedContainer.EmbeddedObjects.Add(uid);
    }

    public void EmbedDetach(EntityUid uid, EmbeddableProjectileComponent? component, EntityUid? user = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (component.EmbeddedIntoUid is not null)
        {
            if (TryComp<EmbeddedContainerComponent>(component.EmbeddedIntoUid.Value, out var embeddedContainer))
            {
                embeddedContainer.EmbeddedObjects.Remove(uid);
                Dirty(component.EmbeddedIntoUid.Value, embeddedContainer);
                if (embeddedContainer.EmbeddedObjects.Count == 0)
                    RemCompDeferred<EmbeddedContainerComponent>(component.EmbeddedIntoUid.Value);
            }
        }

        if (component.DeleteOnRemove && _net.IsServer)
        {
            QueueDel(uid);
            return;
        }

        var xform = Transform(uid);
        if (TerminatingOrDeleted(xform.GridUid) && TerminatingOrDeleted(xform.MapUid))
            return;
        TryComp<PhysicsComponent>(uid, out var physics);
        _physics.SetBodyType(uid, BodyType.Dynamic, body: physics, xform: xform);
        _transform.AttachToGridOrMap(uid, xform);
        component.EmbeddedIntoUid = null;
        Dirty(uid, component);

        if (TryComp<ProjectileComponent>(uid, out var projectile))
        {
            projectile.Shooter = null;
            projectile.Weapon = null;
            projectile.ProjectileSpent = false;
            Dirty(uid, projectile);
        }

        if (user != null)
        {
            var landEv = new LandEvent(user, true);
            RaiseLocalEvent(uid, ref landEv);
        }

        _physics.WakeBody(uid, body: physics);
    }

    private void OnEmbeddableTermination(Entity<EmbeddedContainerComponent> container, ref EntityTerminatingEvent args)
    {
        DetachAllEmbedded(container);
    }

    public void DetachAllEmbedded(Entity<EmbeddedContainerComponent> container)
    {
        foreach (var embedded in container.Comp.EmbeddedObjects)
        {
            if (!TryComp<EmbeddableProjectileComponent>(embedded, out var embeddedComp))
                continue;

            EmbedDetach(embedded, embeddedComp);
        }
    }

    private void PreventCollision(EntityUid uid, ProjectileComponent component, ref PreventCollideEvent args)
    {
        if (component.IgnoreShooter && (args.OtherEntity == component.Shooter || args.OtherEntity == component.Weapon))
        {
            args.Cancelled = true;
        }
    }

    public void SetShooter(EntityUid id, ProjectileComponent component, EntityUid? shooterId = null)
    {
        if (component.Shooter == shooterId || shooterId == null)
            return;

        component.Shooter = shooterId;
        Dirty(id, component);
    }

    [Serializable, NetSerializable]
    private sealed partial class RemoveEmbeddedProjectileEvent : DoAfterEvent
    {
        public override DoAfterEvent Clone() => this;
    }

    #region Stalker-EN-Changes: Thrown knives fixes
    public void RemoveEmbeddedChildren(EntityUid uid)
    {
        var enumerator = Transform(uid).ChildEnumerator;

        while (enumerator.MoveNext(out var child))
        {
            if (TryComp<EmbeddableProjectileComponent>(child, out var embed))
                EmbedDetach(child, embed);
        }
    }

    public void EmbedDetach(EntityUid uid, EmbeddableProjectileComponent? component)
    {
        if (!Resolve(uid, ref component))
            return;

        if (component.EmbeddedIntoUid != null &&
            TryComp<EmbeddedContainerComponent>(component.EmbeddedIntoUid.Value, out var embeddedContainer))
        {
            embeddedContainer.EmbeddedObjects.Remove(uid);
            Dirty(component.EmbeddedIntoUid.Value, embeddedContainer);
            if (embeddedContainer.EmbeddedObjects.Count == 0)
                RemCompDeferred<EmbeddedContainerComponent>(component.EmbeddedIntoUid.Value);
        }

        if (component.DeleteOnRemove)
        {
            PredictedQueueDel(uid);
            return;
        }

        var xform = Transform(uid);
        if (TerminatingOrDeleted(xform.GridUid) && TerminatingOrDeleted(xform.MapUid))
            return;

        TryComp<PhysicsComponent>(uid, out var physics);
        if (physics != null)
            _physics.SetBodyType(uid, BodyType.Dynamic, body: physics, xform: xform);
        _transform.AttachToGridOrMap(uid, xform);
        component.EmbeddedIntoUid = null;
        Dirty(uid, component);

        var landEv = new LandEvent(null, true);
        RaiseLocalEvent(uid, ref landEv);
        if (physics != null)
            _physics.WakeBody(uid, body: physics);
    }
    #endregion
}

[Serializable, NetSerializable]
public sealed class ImpactEffectEvent : EntityEventArgs
{
    public string Prototype;
    public NetCoordinates Coordinates;

    public ImpactEffectEvent(string prototype, NetCoordinates coordinates)
    {
        Prototype = prototype;
        Coordinates = coordinates;
    }
}

[ByRefEvent]
public record struct ProjectileReflectAttemptEvent(EntityUid ProjUid, ProjectileComponent Component, bool Cancelled) : IInventoryRelayEvent
{
    public SlotFlags TargetSlots => SlotFlags.WITHOUT_POCKET;
}

[ByRefEvent]
public record struct ProjectileHitEvent(DamageSpecifier Damage, EntityUid Target, EntityUid? Shooter = null, bool Handled = false);
