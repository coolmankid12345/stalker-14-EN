using Content.Server.Administration.Logs;
using Content.Server.Destructible;
using Content.Server.Effects;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared._Stalker.Weapons.Ranged;
using Content.Shared.Armor;
using Content.Shared.Camera;
using Content.Shared.Inventory;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.FixedPoint;
using Content.Shared.Projectiles;
using Robust.Shared.Network;
using Robust.Shared.Physics.Events;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Projectiles;

public sealed class ProjectileSystem : SharedProjectileSystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly ColorFlashEffectSystem _color = default!;
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    [Dependency] private readonly DestructibleSystem _destructibleSystem = default!;
    [Dependency] private readonly GunSystem _guns = default!;
    [Dependency] private readonly SharedCameraRecoilSystem _sharedCameraRecoil = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ProjectileComponent, ProjectileHitEvent>(OnProjectileHit);
        SubscribeLocalEvent<ProjectileComponent, StartCollideEvent>(OnStartCollide);
    }

    private void OnProjectileHit(EntityUid uid, ProjectileComponent component, ref ProjectileHitEvent args)
    {
        if (!_net.IsServer)
            return;

        var target = args.Target;
        if (Deleted(target))
            return;

        // CRITICAL: Mark the event as handled so SharedProjectileSystem.ProjectileCollide
        // skips its own ChangeDamage call. Without this, damage is applied twice:
        // once here and once in ProjectileCollide after RaiseLocalEvent returns.
        args.Handled = true;

        TryComp<DamageableComponent>(target, out var damageableComp);

        if (TryComp<STArmorPenetrationComponent>(uid, out var penComp))
        {
            // NIJ penetration system: temporarily place STIncomingPenetrationComponent
            // on the target so SharedArmorSystem reads it during the DamageModifyEvent
            // inventory relay. Removed immediately after TryChangeDamage completes.
            var incoming = EnsureComp<STIncomingPenetrationComponent>(target);
            incoming.PenetrationClass = penComp.PenetrationClass;

            _damageableSystem.TryChangeDamage(
                (target, damageableComp),
                args.Damage,
                out var damage,
                ignoreResistances: false,
                origin: component.Shooter);

            RemComp<STIncomingPenetrationComponent>(target);

            if (Exists(component.Shooter) && damage != null)
            {
                if (!Deleted(target))
                    _color.RaiseEffect(Color.Red, new List<EntityUid> { target },
                        Filter.Pvs(target, entityManager: EntityManager));

                _adminLogger.Add(LogType.BulletHit,
                    HasComp<ActorComponent>(target) ? LogImpact.Medium : LogImpact.Low,
                    $"Projectile {ToPrettyString(uid):projectile} shot by {ToPrettyString(component.Shooter!.Value):user} hit {ToPrettyString(target):target} and dealt {damage:damage} damage");
            }
        }
        else
        {
            // Legacy binary bypass for projectiles without STArmorPenetrationComponent.
            var ignoreResistance = false;
            List<EntityUid> ignore = new();
            string[] slots = {
                "outerClothing", "head", "cloak", "eyes", "ears", "mask",
                "jumpsuit", "neck", "back", "belt", "gloves", "shoes", "id", "legs", "torso"
            };

            foreach (var slot in slots)
            {
                if (_inventory.TryGetSlotEntity(target, slot, out var entity) &&
                    TryComp<ArmorComponent>(entity, out var armorComp) &&
                    armorComp.ArmorClass.HasValue &&
                    component.ProjectileClass >= armorComp.ArmorClass.Value)
                    ignore.Add(entity.Value);
            }

            if (TryComp<DamageableComponent>(target, out var damageable) &&
                damageable.DamageModifierSetId != null &&
                _prototype.TryIndex(damageable.DamageModifierSetId, out var modSet))
                ignoreResistance = component.ProjectileClass >= modSet.Class;

            if (_damageableSystem.TryChangeDamage(
                    (target, damageableComp),
                    args.Damage,
                    out var damage,
                    component.IgnoreResistances || ignoreResistance,
                    origin: component.Shooter,
                    ignoreResistors: ignore) &&
                Exists(component.Shooter))
            {
                if (!Deleted(target))
                    _color.RaiseEffect(Color.Red, new List<EntityUid> { target },
                        Filter.Pvs(target, entityManager: EntityManager));

                _adminLogger.Add(LogType.BulletHit,
                    HasComp<ActorComponent>(target) ? LogImpact.Medium : LogImpact.Low,
                    $"Projectile {ToPrettyString(uid):projectile} shot by {ToPrettyString(component.Shooter!.Value):user} hit {ToPrettyString(target):target} and dealt {damage:damage} damage");
            }
        }

        component.ProjectileSpent = true;
        Dirty(uid, component);
    }

    private void OnStartCollide(EntityUid uid, ProjectileComponent component, ref StartCollideEvent args)
    {
        // With gun prediction active, damage is handled entirely via ProjectileHitEvent
        // raised by the prediction system → OnProjectileHit above handles all damage.
        if (_guns.GunPrediction)
            return;

        if (args.OurFixtureId != ProjectileFixture || !args.OtherFixture.Hard
            || component.ProjectileSpent || component is { Weapon: null, OnlyCollideWhenShot: true })
            return;

        var target = args.OtherEntity;

        var attemptEv = new ProjectileReflectAttemptEvent(uid, component, false);
        RaiseLocalEvent(target, ref attemptEv);
        if (attemptEv.Cancelled)
        {
            SetShooter(uid, component, target);
            return;
        }

        // Raise ProjectileHitEvent — OnProjectileHit above handles all damage
        // and sets args.Handled = true, so ProjectileCollide skips its own ChangeDamage.
        var ev = new ProjectileHitEvent(component.Damage * _damageableSystem.UniversalProjectileDamageModifier, target, component.Shooter);
        RaiseLocalEvent(uid, ref ev);

        var damageRequired = _destructibleSystem.DestroyedAt(target);
        TryComp<DamageableComponent>(target, out var damageableComponent);
        if (damageableComponent != null)
        {
            damageRequired -= damageableComponent.TotalDamage;
            damageRequired = FixedPoint2.Max(damageRequired, FixedPoint2.Zero);
        }

        var deleted = Deleted(target);
        var appliedDamage = ev.Damage;

        if (component.PenetrationThreshold != 0)
        {
            if (component.PenetrationDamageTypeRequirement != null)
            {
                var stopPenetration = false;
                foreach (var requiredDamageType in component.PenetrationDamageTypeRequirement)
                {
                    if (!appliedDamage.DamageDict.Keys.Contains(requiredDamageType))
                    {
                        stopPenetration = true;
                        break;
                    }
                }
                if (stopPenetration)
                    component.ProjectileSpent = true;
            }

            if (appliedDamage.GetTotal() < damageRequired)
                component.ProjectileSpent = true;

            if (!component.ProjectileSpent)
            {
                component.PenetrationAmount += damageRequired;
                if (component.PenetrationAmount >= component.PenetrationThreshold)
                    component.ProjectileSpent = true;
            }
        }
        else
        {
            component.ProjectileSpent = true;
        }

        if (!deleted)
        {
            _guns.PlayImpactSound(target, appliedDamage, component.SoundHit, component.ForceSound);

            if (!args.OurBody.LinearVelocity.IsLengthZero())
                _sharedCameraRecoil.KickCamera(target, args.OurBody.LinearVelocity.Normalized());
        }

        if (component.DeleteOnCollide && component.ProjectileSpent)
            QueueDel(uid);

        if (component.ImpactEffect != null && TryComp(uid, out TransformComponent? xform))
        {
            RaiseNetworkEvent(new ImpactEffectEvent(component.ImpactEffect, GetNetCoordinates(xform.Coordinates)),
                Filter.Pvs(xform.Coordinates, entityMan: EntityManager));
        }
    }
}
