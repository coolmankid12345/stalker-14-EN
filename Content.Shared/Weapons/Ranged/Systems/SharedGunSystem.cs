using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Shared._DZ.FarGunshot;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Random;
using Content.Shared._RMC14.Weapons.Ranged.Prediction;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.Administration.Logs;
using Content.Shared.Audio;
using Content.Shared.Camera;
using Content.Shared.CombatMode;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.Effects;
using Content.Shared.Examine;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Tag;
using Content.Shared.Throwing;
using Content.Shared.Timing;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Reflect;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared.Weapons.Ranged.Systems;

public abstract partial class SharedGunSystem : EntitySystem
{
    [Dependency] private   readonly ActionBlockerSystem _actionBlockerSystem = default!;
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] protected readonly IMapManager MapManager = default!;
    [Dependency] private   readonly INetManager _netManager = default!;
    [Dependency] protected readonly IPrototypeManager ProtoManager = default!;
    [Dependency] protected readonly IRobustRandom Random = default!;
    [Dependency] protected readonly ISharedAdminLogManager Logs = default!;
    [Dependency] private   readonly IConfigurationManager _config = default!;
    [Dependency] protected readonly DamageableSystem Damageable = default!;
    [Dependency] protected readonly ExamineSystemShared Examine = default!;
    [Dependency] private   readonly SharedHandsSystem _hands = default!;
    [Dependency] private   readonly ItemSlotsSystem _slots = default!;
    [Dependency] private   readonly RechargeBasicEntityAmmoSystem _recharge = default!;
    [Dependency] protected readonly SharedActionsSystem Actions = default!;
    [Dependency] protected readonly SharedAppearanceSystem Appearance = default!;
    [Dependency] protected readonly SharedAudioSystem Audio = default!;
    [Dependency] private   readonly SharedCameraRecoilSystem _recoil = default!;
    [Dependency] private   readonly SharedCombatModeSystem _combatMode = default!;
    [Dependency] protected readonly SharedContainerSystem Containers = default!;
    [Dependency] private   readonly SharedMapSystem _map = default!;
    [Dependency] protected readonly SharedPointLightSystem Lights = default!;
    [Dependency] protected readonly SharedPopupSystem PopupSystem = default!;
    [Dependency] protected readonly SharedPhysicsSystem Physics = default!;
    [Dependency] protected readonly SharedProjectileSystem Projectiles = default!;
    [Dependency] protected readonly SharedTransformSystem TransformSystem = default!;
    [Dependency] protected readonly TagSystem TagSystem = default!;
    [Dependency] protected readonly ThrowingSystem ThrowingSystem = default!;
    [Dependency] private   readonly UseDelaySystem _useDelay = default!;
    [Dependency] private   readonly EntityWhitelistSystem _whitelistSystem = default!;
    // stalker-changes-start: dependencies for prototype-based hitscan (HitscanPrototype)
    [Dependency] private   readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private   readonly SharedColorFlashEffectSystem _color = default!;
    // stalker-changes-end

    private static readonly ProtoId<TagPrototype> TrashTag = "Trash";

    private const float InteractNextFire = 0.3f;
    private const double SafetyNextFire = 0.5;
    private const float EjectOffset = 0.4f;
    protected const string AmmoExamineColor = "yellow";
    protected const string FireRateExamineColor = "yellow";
    public const string ModeExamineColor = "cyan";
    private const float DamagePitchVariation = 0.05f;

    public bool GunPrediction { get; private set; }

    public override void Initialize()
    {
        Subs.CVar(_config, RMCCVars.RMCGunPrediction, v => GunPrediction = v, true);

        SubscribeAllEvent<RequestShootEvent>(OnShootRequest);
        SubscribeAllEvent<RequestStopShootEvent>(OnStopShootRequest);
        SubscribeLocalEvent<GunComponent, MeleeHitEvent>(OnGunMelee);

        // Ammo providers
        InitializeBallistic();
        InitializeBattery();
        InitializeCartridge();
        InitializeChamberMagazine();
        InitializeMagazine();
        InitializeRevolver();
        InitializeBasicEntity();
        InitializeClothing();
        InitializeContainer();
        InitializeSolution();

        // Interactions
        SubscribeLocalEvent<GunComponent, GetVerbsEvent<AlternativeVerb>>(OnAltVerb);
        SubscribeLocalEvent<GunComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<GunComponent, CycleModeEvent>(OnCycleMode);
        SubscribeLocalEvent<GunComponent, HandSelectedEvent>(OnGunSelected);
        SubscribeLocalEvent<GunComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<GunComponent> gun, ref MapInitEvent args)
    {
#if DEBUG
        if (gun.Comp.NextFire > Timing.CurTime)
            Log.Warning($"Initializing a map that contains an entity that is on cooldown. Entity: {ToPrettyString(gun)}");

        DebugTools.Assert((gun.Comp.AvailableModes & gun.Comp.SelectedMode) != 0x0);
#endif

        RefreshModifiers((gun, gun));
    }

    private void OnGunMelee(EntityUid uid, GunComponent component, MeleeHitEvent args)
    {
        if (!TryComp<MeleeWeaponComponent>(uid, out var melee))
            return;

        if (melee.NextAttack > component.NextFire)
        {
            component.NextFire = melee.NextAttack;
            DirtyField(uid, component, nameof(GunComponent.NextFire));
        }
    }

    private void OnShootRequest(RequestShootEvent msg, EntitySessionEventArgs args)
    {
        var user = args.SenderSession.AttachedEntity;

        if (user == null ||
            !_combatMode.IsInCombatMode(user) ||
            !TryGetGun(user.Value, out var ent, out var gun))
        {
            return;
        }

        if (ent != GetEntity(msg.Gun))
            return;

        gun.ShootCoordinates = GetCoordinates(msg.Coordinates);
        gun.Target = GetEntity(msg.Target);
        AttemptShoot(user.Value, ent, gun);
    }

    private void OnStopShootRequest(RequestStopShootEvent ev, EntitySessionEventArgs args)
    {
        var gunUid = GetEntity(ev.Gun);

        if (args.SenderSession.AttachedEntity == null ||
            !TryComp<GunComponent>(gunUid, out var gun) ||
            !TryGetGun(args.SenderSession.AttachedEntity.Value, out _, out var userGun))
        {
            return;
        }

        if (userGun != gun)
            return;

        StopShooting(gunUid, gun);
    }

    public bool CanShoot(GunComponent component)
    {
        if (component.NextFire > Timing.CurTime)
            return false;

        return true;
    }

    public bool TryGetGun(EntityUid entity, out EntityUid gunEntity, [NotNullWhen(true)] out GunComponent? gunComp)
    {
        gunEntity = default;
        gunComp = null;

        if (_hands.GetActiveItem(entity) is { } held &&
            TryComp(held, out GunComponent? gun))
        {
            gunEntity = held;
            gunComp = gun;
            return true;
        }

        // Last resort is check if the entity itself is a gun.
        if (TryComp(entity, out gun))
        {
            gunEntity = entity;
            gunComp = gun;
            return true;
        }

        return false;
    }

    private void StopShooting(EntityUid uid, GunComponent gun)
    {
        if (gun.ShotCounter == 0)
            return;

        gun.ShotCounter = 0;
        gun.ShootCoordinates = null;
        gun.Target = null;
        DirtyField(uid, gun, nameof(GunComponent.ShotCounter));
    }

    #region AttemptShoot overloads

    /// <summary>
    /// Attempts to shoot at the target coordinates. Resets the shot counter after every shot.
    /// </summary>
    public bool AttemptShoot(EntityUid user, EntityUid gunUid, GunComponent gun, EntityCoordinates toCoordinates, EntityUid? target = null)
    {
        gun.ShootCoordinates = toCoordinates;
        gun.Target = target;
        var result = AttemptShoot(user, gunUid, gun);
        gun.ShotCounter = 0;
        DirtyField(gunUid, gun, nameof(GunComponent.ShotCounter));
        return result != null;
    }

    /// <summary>
    /// Shoots by assuming the gun is the user at default coordinates.
    /// </summary>
    public List<EntityUid>? AttemptShoot(EntityUid gunUid, GunComponent gun)
    {
        var coordinates = new EntityCoordinates(gunUid, gun.DefaultDirection);
        gun.ShootCoordinates = coordinates;
        var result = AttemptShoot(gunUid, gunUid, gun);
        gun.ShotCounter = 0;
        return result;
    }

    /// <summary>
    /// Prediction-compatible AttemptShoot overload that accepts coordinates and target inline.
    /// Used by callers that have coordinates/target to pass alongside prediction data.
    /// </summary>
    public List<EntityUid>? AttemptShoot(
        EntityUid user,
        EntityUid gunUid,
        GunComponent gun,
        EntityCoordinates toCoordinates,
        EntityUid? target,
        List<int>? predictedProjectiles,
        ICommonSession? userSession)
    {
        gun.ShootCoordinates = toCoordinates;
        gun.Target = target;
        return AttemptShoot(user, gunUid, gun, predictedProjectiles, userSession);
    }

    #endregion

    /// <summary>
    /// Unified AttemptShoot method with optional prediction support. Returns spawned projectile
    /// EntityUids on success, or null if the shot was blocked or had no ammo.
    /// </summary>
    public List<EntityUid>? AttemptShoot(
        EntityUid user,
        EntityUid gunUid,
        GunComponent gun,
        List<int>? predictedProjectiles = null,
        ICommonSession? userSession = null)
    {
        if (gun.FireRateModified <= 0f ||
            !_actionBlockerSystem.CanAttack(user))
        {
            return null;
        }

        var toCoordinates = gun.ShootCoordinates;

        if (toCoordinates == null)
            return null;

        var curTime = Timing.CurTime;

        // check if anything wants to prevent shooting
        var prevention = new ShotAttemptedEvent
        {
            User = user,
            Used = (gunUid, gun)
        };
        RaiseLocalEvent(gunUid, ref prevention, true); // stalker-changes
        if (prevention.Cancelled)
            return null;

        RaiseLocalEvent(user, ref prevention);
        if (prevention.Cancelled)
            return null;

        // Need to do this to play the clicking sound for empty automatic weapons
        // but not play anything for burst fire.
        if (gun.NextFire > curTime)
            return null;

        var fireRate = TimeSpan.FromSeconds(1f / gun.FireRateModified);

        if (gun.SelectedMode == SelectiveFire.Burst || gun.BurstActivated)
            fireRate = TimeSpan.FromSeconds(1f / gun.BurstFireRate);

        // First shot
        // Previously we checked shotcounter but in some cases all the bullets got dumped at once
        // curTime - fireRate is insufficient because if you time it just right you can get a 3rd shot out slightly quicker.
        if (gun.NextFire < curTime - fireRate || gun.ShotCounter == 0 && gun.NextFire < curTime)
            gun.NextFire = curTime;

        var shots = 0;
        var lastFire = gun.NextFire;

        while (gun.NextFire <= curTime)
        {
            gun.NextFire += fireRate;
            shots++;
        }

        // NextFire has been touched regardless so need to dirty the gun.
        DirtyField(gunUid, gun, nameof(GunComponent.NextFire));

        // Get how many shots we're actually allowed to make, due to clip size or otherwise.
        // Don't do this in the loop so we still reset NextFire.
        if (!gun.BurstActivated)
        {
            switch (gun.SelectedMode)
            {
                case SelectiveFire.SemiAuto:
                    shots = Math.Min(shots, 1 - gun.ShotCounter);
                    break;
                case SelectiveFire.Burst:
                    shots = Math.Min(shots, gun.ShotsPerBurstModified - gun.ShotCounter);
                    break;
                case SelectiveFire.FullAuto:
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"No implemented shooting behavior for {gun.SelectedMode}!");
            }
        }
        else
        {
            shots = Math.Min(shots, gun.ShotsPerBurstModified - gun.ShotCounter);
        }

        var attemptEv = new AttemptShootEvent(user, null);
        RaiseLocalEvent(gunUid, ref attemptEv);

        if (attemptEv.Cancelled)
        {
            if (attemptEv.Message != null)
            {
                PopupSystem.PopupClient(attemptEv.Message, gunUid, user);
            }
            gun.BurstActivated = false;
            gun.BurstShotsCount = 0;
            gun.NextFire = TimeSpan.FromSeconds(Math.Max(lastFire.TotalSeconds + SafetyNextFire, gun.NextFire.TotalSeconds));
            return null;
        }

        var fromCoordinates = Transform(user).Coordinates;
        var ev = new TakeAmmoEvent(shots, new List<(EntityUid? Entity, IShootable Shootable)>(), fromCoordinates, user);

        // Listen it just makes the other code around it easier if shots == 0 to do this.
        if (shots > 0)
            RaiseLocalEvent(gunUid, ev);

        DebugTools.Assert(ev.Ammo.Count <= shots);
        DebugTools.Assert(shots >= 0);
        UpdateAmmoCount(gunUid);

        // Even if we don't actually shoot update the ShotCounter. This is to avoid spamming empty sounds
        // where the gun may be SemiAuto or Burst.
        gun.ShotCounter += shots;
        DirtyField(gunUid, gun, nameof(GunComponent.ShotCounter));

        if (ev.Ammo.Count <= 0)
        {
            var emptyGunShotEvent = new OnEmptyGunShotEvent(user);
            RaiseLocalEvent(gunUid, ref emptyGunShotEvent);

            gun.BurstActivated = false;
            gun.BurstShotsCount = 0;
            gun.NextFire += TimeSpan.FromSeconds(gun.BurstCooldown);

            // If they're firing an existing clip then don't play anything.
            if (shots > 0)
            {
                PopupSystem.PopupCursor(ev.Reason ?? Loc.GetString("gun-magazine-fired-empty"));

                // Don't spam safety sounds at gun fire rate, play it at a reduced rate.
                // May cause prediction issues? Needs more tweaking
                gun.NextFire = TimeSpan.FromSeconds(Math.Max(lastFire.TotalSeconds + SafetyNextFire, gun.NextFire.TotalSeconds));
                Audio.PlayPredicted(gun.SoundEmpty, gunUid, user);
                return null;
            }

            return null;
        }

        // Handle burstfire
        if (gun.SelectedMode == SelectiveFire.Burst)
        {
            gun.BurstActivated = true;
        }
        if (gun.BurstActivated)
        {
            gun.BurstShotsCount += shots;
            if (gun.BurstShotsCount >= gun.ShotsPerBurstModified)
            {
                gun.NextFire += TimeSpan.FromSeconds(gun.BurstCooldown);
                gun.BurstActivated = false;
                gun.BurstShotsCount = 0;
            }
        }

        // Shoot confirmed - sounds also played here in case it's invalid (e.g. cartridge already spent).
        List<EntityUid>? projectiles = null;
        var userImpulse = false;
        if (Timing.IsFirstTimePredicted)
        {
            projectiles = Shoot(gunUid, gun, ev.Ammo, fromCoordinates, toCoordinates.Value, out userImpulse, user, throwItems: attemptEv.ThrowItems, predictedProjectiles, userSession);
        }

        var shotEv = new GunShotEvent(user, ev.Ammo, fromCoordinates, toCoordinates.Value);
        RaiseLocalEvent(gunUid, ref shotEv);

        if (userImpulse && TryComp<PhysicsComponent>(user, out var userPhysics))
        {
            var shooterEv = new ShooterImpulseEvent();
            RaiseLocalEvent(user, ref shooterEv);

            if (shooterEv.Push)
                CauseImpulse(fromCoordinates, toCoordinates.Value, user, userPhysics);
        }

        DirtyField(gunUid, gun, nameof(GunComponent.BurstActivated));
        Dirty(gunUid, gun);

        // Clean up client-side ammo entities that weren't used as predicted projectiles
        foreach (var (ent, _) in ev.Ammo)
        {
            if (ent == null)
                continue;

            if (IsClientSide(ent.Value) &&
                (HasComp<GunIgnorePredictionComponent>(gunUid) || projectiles == null || !projectiles.Contains(ent.Value)))
                Del(ent);
        }

        return projectiles;
    }

    public void Shoot(
        EntityUid gunUid,
        GunComponent gun,
        EntityUid ammo,
        EntityCoordinates fromCoordinates,
        EntityCoordinates toCoordinates,
        out bool userImpulse,
        EntityUid? user = null,
        bool throwItems = false)
    {
        var shootable = EnsureShootable(ammo);
        Shoot(gunUid, gun, new List<(EntityUid? Entity, IShootable Shootable)>(1) { (ammo, shootable) }, fromCoordinates, toCoordinates, out userImpulse, user, throwItems);
    }

    /// <summary>
    /// Unified Shoot implementation. Handles cartridge, ammo, and hitscan ammo types with
    /// inline prediction support. Runs on both client and server.
    /// </summary>
    private List<EntityUid> Shoot(
        EntityUid gunUid,
        GunComponent gun,
        List<(EntityUid? Entity, IShootable Shootable)> ammo,
        EntityCoordinates fromCoordinates,
        EntityCoordinates toCoordinates,
        out bool userImpulse,
        EntityUid? user = null,
        bool throwItems = false,
        List<int>? predictedProjectiles = null,
        ICommonSession? userSession = null)
    {
        userImpulse = true;

        if (user != null)
        {
            var selfEvent = new SelfBeforeGunShotEvent(user.Value, (gunUid, gun), ammo);
            RaiseLocalEvent(user.Value, selfEvent);
            if (selfEvent.Cancelled)
            {
                userImpulse = false;
                return new List<EntityUid>();
            }
        }

        var fromMap = TransformSystem.ToMapCoordinates(fromCoordinates);
        var toMap = TransformSystem.ToMapCoordinates(toCoordinates).Position;
        var mapDirection = toMap - fromMap.Position;
        var mapAngle = mapDirection.ToAngle();
        var angle = GetRecoilAngle(Timing.CurTime, gun, mapDirection.ToAngle());

        // If applicable, this ensures the projectile is parented to grid on spawn, instead of the map.
        var fromEnt = MapManager.TryFindGridAt(fromMap, out var gridUid, out _)
            ? TransformSystem.WithEntityId(fromCoordinates, gridUid)
            : new EntityCoordinates(_map.GetMapOrInvalid(fromMap.MapId), fromMap.Position);

        toMap = fromMap.Position + angle.ToVec() * mapDirection.Length();
        mapDirection = toMap - fromMap.Position;
        var gunVelocity = Physics.GetMapLinearVelocity(fromEnt);

        // I must be high because this was getting tripped even when true.
        // DebugTools.Assert(direction != Vector2.Zero);
        var shotProjectiles = new List<EntityUid>(ammo.Count);

        void MarkPredicted(EntityUid projectile, int index)
        {
            if (!GunPrediction)
                return;

            if (predictedProjectiles == null || userSession == null)
                return;

            if (predictedProjectiles.TryGetValue(index, out var predicted))
            {
                var comp = new PredictedProjectileServerComponent
                {
                    Shooter = userSession,
                    ClientId = predicted,
                    ClientEnt = user,
                };
                AddComp(projectile, comp, true);
                Dirty(projectile, comp);
            }
        }

        foreach (var (ent, shootable) in ammo)
        {
            // pneumatic cannon doesn't shoot bullets it just throws them, ignore ammo handling
            if (throwItems && ent != null)
            {
                Recoil(user, mapDirection, gun.CameraRecoilScalarModified);
                ShootOrThrow(ent.Value, mapDirection, gunVelocity, gun, gunUid, user);
                continue;
            }

            switch (shootable)
            {
                case CartridgeAmmoComponent cartridge:
                    if (!cartridge.Spent)
                    {
                        if (_netManager.IsServer || GunPrediction)
                        {
                            var uid = Spawn(cartridge.Prototype, fromEnt);
                            CreateAndFireProjectiles(uid, cartridge);

                            if (_netManager.IsClient && HasComp<GunIgnorePredictionComponent>(gunUid))
                            {
                                predictedProjectiles?.RemoveAll(i => i == uid.Id);
                                QueueDel(uid);
                            }

                            // stalker-changes-start
                            if (_netManager.IsServer)
                            {
                                var farSoundEvent = new FargunshotEvent(gunUid.Id);
                                RaiseLocalEvent(gunUid, farSoundEvent);
                            }
                            // stalker-changes-end

                            RaiseLocalEvent(ent!.Value, new AmmoShotEvent()
                            {
                                FiredProjectiles = shotProjectiles,
                            });

                            SetCartridgeSpent(ent.Value, cartridge, true);

                            if (cartridge.DeleteOnSpawn &&
                                (_netManager.IsServer || IsClientSide(ent.Value)))
                            {
                                Del(ent.Value);
                            }
                        }
                        else
                        {
                            MuzzleFlash(gunUid, cartridge, mapDirection.ToAngle(), user);
                            Audio.PlayPredicted(gun.SoundGunshotModified, gunUid, user);
                        }
                    }
                    else
                    {
                        userImpulse = false;
                        Audio.PlayPredicted(gun.SoundEmpty, gunUid, user);
                    }

                    Recoil(user, mapDirection, gun.CameraRecoilScalarModified);

                    // Something like ballistic might want to leave it in the container still
                    if (!cartridge.DeleteOnSpawn && !Containers.IsEntityInContainer(ent!.Value))
                        EjectCartridge(ent.Value, angle);

                    if (IsClientSide(ent!.Value))
                        Del(ent.Value);
                    else
                        Dirty(ent!.Value, cartridge);
                    break;
                case AmmoComponent newAmmo:
                    if (_netManager.IsServer || GunPrediction)
                    {
                        if (ent != null)
                            CreateAndFireProjectiles(ent.Value, newAmmo);
                    }
                    else
                    {
                        MuzzleFlash(gunUid, newAmmo, mapDirection.ToAngle(), user);
                        Audio.PlayPredicted(gun.SoundGunshotModified, gunUid, user);
                    }

                    Recoil(user, mapDirection, gun.CameraRecoilScalarModified);

                    if (_netManager.IsClient)
                        RemoveShootable(ent!.Value);
                    if (ent != null)
                        MarkPredicted(ent.Value, 0);
                    break;
                // stalker-changes-start: Hitscan ammo uses event-based raycast system
                case HitscanAmmoComponent:
                    if (ent == null)
                        break;

                    if (_netManager.IsServer)
                    {
                        var hitscanEv = new HitscanTraceEvent
                        {
                            FromCoordinates = fromCoordinates,
                            ShotDirection = mapDirection.Normalized(),
                            Gun = gunUid,
                            Shooter = user,
                            Target = gun.Target,
                        };
                        RaiseLocalEvent(ent.Value, ref hitscanEv);

                        Del(ent);
                    }

                    Audio.PlayPredicted(gun.SoundGunshotModified, gunUid, user);
                    Recoil(user, mapDirection, gun.CameraRecoilScalarModified);
                    break;
                // stalker-changes-end
                // stalker-changes-start: prototype-based hitscan (ported from RMC-14)
                case HitscanPrototype hitscan:
                    EntityUid? lastHit = null;

                    var from = fromMap;
                    // Can't use map coords above because FireEffects expects entity coords.
                    var fromEffect = fromCoordinates;
                    var dir = mapDirection.Normalized();

                    // In the situation when user == null, the cannon fires on its own (via signals).
                    var lastUser = user ?? gunUid;

                    if (hitscan.Reflective != ReflectType.None)
                    {
                        for (var reflectAttempt = 0; reflectAttempt < 3; reflectAttempt++)
                        {
                            var ray = new CollisionRay(from.Position, dir, hitscan.CollisionMask);
                            var rayCastResults = Physics.IntersectRay(from.MapId, ray, hitscan.MaxLength, lastUser, false);

                            // Collect results without LINQ allocations.
                            var hitResults = new List<RayCastResults>();
                            foreach (var r in rayCastResults)
                                hitResults.Add(r);

                            if (hitResults.Count == 0)
                                break;

                            var result = hitResults[0];

                            // Check if laser is shot from in a container
                            if (!Containers.IsEntityOrParentInContainer(lastUser))
                            {
                                // Checks if the laser should pass over unless targeted by its user
                                foreach (var collide in hitResults)
                                {
                                    if (collide.HitEntity != gun.Target &&
                                        CompOrNull<RequireProjectileTargetComponent>(collide.HitEntity)?.Active == true)
                                    {
                                        continue;
                                    }

                                    result = collide;
                                    break;
                                }
                            }

                            var hit = result.HitEntity;
                            lastHit = hit;

                            FireEffects(fromEffect, result.Distance, dir.Normalized().ToAngle(), hitscan, hit);

                            var ev = new HitScanReflectAttemptEvent(user, gunUid, hitscan.Reflective, dir, false);
                            RaiseLocalEvent(hit, ref ev);

                            if (!ev.Reflected)
                                break;

                            fromEffect = Transform(hit).Coordinates;
                            from = fromEffect.ToMap(EntityManager, TransformSystem);
                            dir = ev.Direction;
                            lastUser = hit;
                        }
                    }

                    if (lastHit != null)
                    {
                        var hitEntity = lastHit.Value;
                        if (hitscan.StaminaDamage > 0f)
                            _stamina.TakeStaminaDamage(hitEntity, hitscan.StaminaDamage, source: user);

                        var hitName = ToPrettyString(hitEntity);

                        if (hitscan.Damage != null)
                        {
                            var scaledDamage = hitscan.Damage * Damageable.UniversalHitscanDamageModifier;
                            Damageable.TryChangeDamage(hitEntity, scaledDamage, out var appliedDamage, origin: user);

                            if (!Deleted(hitEntity))
                            {
                                if (appliedDamage.AnyPositive())
                                {
                                    _color.RaiseEffect(Color.Red, new List<EntityUid>() { hitEntity }, Filter.Pvs(hitEntity, entityManager: EntityManager));
                                }

                                PlayImpactSound(hitEntity, appliedDamage, hitscan.Sound, hitscan.ForceSound);
                            }

                            if (user != null)
                            {
                                Logs.Add(LogType.HitScanHit,
                                    $"{ToPrettyString(user.Value):user} hit {hitName:target} using hitscan and dealt {appliedDamage.GetTotal():damage} damage");
                            }
                            else
                            {
                                Logs.Add(LogType.HitScanHit,
                                    $"{hitName:target} hit by hitscan dealing {appliedDamage.GetTotal():damage} damage");
                            }
                        }
                    }
                    else
                    {
                        FireEffects(fromEffect, hitscan.MaxLength, dir.ToAngle(), hitscan);
                    }

                    Audio.PlayPredicted(gun.SoundGunshotModified, gunUid, user);
                    Recoil(user, mapDirection, gun.CameraRecoilScalarModified);
                    break;
                // stalker-changes-end
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        RaiseLocalEvent(gunUid, new AmmoShotEvent()
        {
            FiredProjectiles = shotProjectiles,
        });

        void CreateAndFireProjectiles(EntityUid ammoEnt, AmmoComponent ammoComp)
        {
            predictedProjectiles ??= new List<int>();
            MarkPredicted(ammoEnt, 0);
            if (TryComp<ProjectileSpreadComponent>(ammoEnt, out var ammoSpreadComp))
            {
                var spreadEvent = new GunGetAmmoSpreadEvent(ammoSpreadComp.Spread);
                RaiseLocalEvent(gunUid, ref spreadEvent);

                var angles = LinearSpread(mapAngle - spreadEvent.Spread / 2,
                    mapAngle + spreadEvent.Spread / 2, ammoSpreadComp.Count);

                ShootOrThrow(ammoEnt, angles[0].ToVec(), gunVelocity, gun, gunUid, user);
                shotProjectiles.Add(ammoEnt);

                for (var i = 1; i < ammoSpreadComp.Count; i++)
                {
                    var newuid = Spawn(ammoSpreadComp.Proto, fromEnt);
                    ShootOrThrow(newuid, angles[i].ToVec(), gunVelocity, gun, gunUid, user);
                    shotProjectiles.Add(newuid);
                    MarkPredicted(newuid, i);
                }
            }
            else
            {
                ShootOrThrow(ammoEnt, mapDirection, gunVelocity, gun, gunUid, user);
                shotProjectiles.Add(ammoEnt);
            }

            MuzzleFlash(gunUid, ammoComp, mapDirection.ToAngle(), user);
            Audio.PlayPredicted(gun.SoundGunshotModified, gunUid, user);
        }

        if (_netManager.IsServer)
            Logs.Add(LogType.HitScanHit, LogImpact.Low, $"{ToPrettyString(user)} shot {ToPrettyString(gunUid)} with {shotProjectiles.Count} projectiles aiming at {TransformSystem.ToMapCoordinates(toCoordinates)}.");
        return shotProjectiles;
    }

    // stalker-changes-start
    private void ShootOrThrow(EntityUid uid, Vector2 mapDirection, Vector2 gunVelocity, GunComponent gun, EntityUid gunUid, EntityUid? user)
    {
        if (gun.Target is { } target && !TerminatingOrDeleted(target))
        {
            var targeted = EnsureComp<TargetedProjectileComponent>(uid);
            targeted.Target = target;
            Dirty(uid, targeted);
        }

        if (!HasComp<ProjectileComponent>(uid))
        {
            RemoveShootable(uid);
            // TODO: Someone can probably yeet this a billion miles so need to pre-validate input somewhere up the call stack.
            ThrowingSystem.TryThrow(uid, mapDirection, gun.ProjectileSpeedModified, user, recoil: false, doSpin: false);
            return;
        }
        ShootProjectile(uid, mapDirection, gunVelocity, gunUid, user, gun.ProjectileSpeedModified);
    }
    // stalker-changes-end

    private Angle GetRecoilAngle(TimeSpan curTime, GunComponent component, Angle direction)
    {
        var timeSinceLastFire = (curTime - component.LastFire).TotalSeconds;
        var newTheta = MathHelper.Clamp(component.CurrentAngle.Theta + component.AngleIncreaseModified.Theta - component.AngleDecayModified.Theta * timeSinceLastFire, component.MinAngleModified.Theta, component.MaxAngleModified.Theta);
        component.CurrentAngle = new Angle(newTheta);
        component.LastFire = component.NextFire;

        // Convert it so angle can go either side.
        // Use deterministic RNG seeded from tick + entity ID for prediction compatibility
        long tick = Timing.CurTick.Value;
        tick = tick << 32;
        tick = tick | (uint) GetNetEntity(component.Owner).Id;
        var random = new Xoroshiro64S(tick).NextFloat(-0.5f, 0.5f);
        var spread = component.CurrentAngle.Theta * random;
        var angle = new Angle(direction.Theta + component.CurrentAngle.Theta * random);
        DebugTools.Assert(spread <= component.MaxAngleModified.Theta);
        return angle;
    }

    /// <summary>
    /// Gets a linear spread of angles between start and end.
    /// </summary>
    /// <param name="start">Start angle in degrees</param>
    /// <param name="end">End angle in degrees</param>
    /// <param name="intervals">How many shots there are</param>
    private Angle[] LinearSpread(Angle start, Angle end, int intervals)
    {
        var angles = new Angle[intervals];
        DebugTools.Assert(intervals > 1);

        for (var i = 0; i <= intervals - 1; i++)
        {
            angles[i] = new Angle(start + (end - start) * i / (intervals - 1));
        }

        return angles;
    }

    private void Recoil(EntityUid? user, Vector2 recoil, float recoilScalar)
    {
        if (_netManager.IsServer)
            return;

        if (!Timing.IsFirstTimePredicted || user == null || recoil == Vector2.Zero || recoilScalar == 0)
            return;

        _recoil.KickCamera(user.Value, recoil.Normalized() * 0.5f * recoilScalar);
    }

    #region Hitscan effects

    private void FireEffects(EntityCoordinates fromCoordinates, float distance, Angle mapDirection, HitscanPrototype hitscan, EntityUid? hitEntity = null)
    {
        // Lord
        // Forgive me for the shitcode I am about to do
        // Effects tempt me not
        var sprites = new List<(NetCoordinates coordinates, Angle angle, SpriteSpecifier sprite, float scale)>();
        var gridUid = fromCoordinates.GetGridUid(EntityManager);
        var angle = mapDirection;

        // We'll get the effects relative to the grid / map of the firer
        // Look you could probably optimise this a bit with redundant transforms at this point.
        var xformQuery = GetEntityQuery<TransformComponent>();

        if (xformQuery.TryGetComponent(gridUid, out var gridXform))
        {
            var (_, gridRot, gridInvMatrix) = TransformSystem.GetWorldPositionRotationInvMatrix(gridXform, xformQuery);

            fromCoordinates = new EntityCoordinates(gridUid.Value,
                Vector2.Transform(TransformSystem.ToMapCoordinates(fromCoordinates).Position, gridInvMatrix));

            // Use the fallback angle I guess?
            angle -= gridRot;
        }

        if (distance >= 1f)
        {
            if (hitscan.MuzzleFlash != null)
            {
                var coords = fromCoordinates.Offset(angle.ToVec().Normalized() / 2);
                var netCoords = GetNetCoordinates(coords);

                sprites.Add((netCoords, angle, hitscan.MuzzleFlash, 1f));
            }

            if (hitscan.TravelFlash != null)
            {
                var coords = fromCoordinates.Offset(angle.ToVec() * (distance + 0.5f) / 2);
                var netCoords = GetNetCoordinates(coords);

                sprites.Add((netCoords, angle, hitscan.TravelFlash, distance - 1.5f));
            }
        }

        if (hitscan.ImpactFlash != null)
        {
            var coords = fromCoordinates.Offset(angle.ToVec() * distance);
            var netCoords = GetNetCoordinates(coords);

            sprites.Add((netCoords, angle.FlipPositive(), hitscan.ImpactFlash, 1f));
        }

        if (_netManager.IsServer && sprites.Count > 0)
        {
            RaiseNetworkEvent(new HitscanEvent
            {
                Sprites = sprites,
            }, Filter.Pvs(fromCoordinates, entityMan: EntityManager));
        }
    }

    #endregion

    public virtual void ShootProjectile(EntityUid uid, Vector2 direction, Vector2 gunVelocity, EntityUid? gunUid, EntityUid? user = null, float speed = 20f)
    {
        var physics = EnsureComp<PhysicsComponent>(uid);
        Physics.SetBodyStatus(uid, physics, BodyStatus.InAir);

        var targetMapVelocity = gunVelocity + direction.Normalized() * speed;
        var currentMapVelocity = Physics.GetMapLinearVelocity(uid, physics);
        var finalLinear = physics.LinearVelocity + targetMapVelocity - currentMapVelocity;
        Physics.SetLinearVelocity(uid, finalLinear, body: physics);

        var projectile = EnsureComp<ProjectileComponent>(uid);
        projectile.Weapon = gunUid;
        var shooter = user ?? gunUid;
        if (shooter != null)
            Projectiles.SetShooter(uid, projectile, shooter.Value);

        TransformSystem.SetWorldRotationNoLerp(uid, direction.ToWorldAngle() + projectile.Angle);
    }

    protected abstract void Popup(string message, EntityUid? uid, EntityUid? user);

    /// <summary>
    /// Call this whenever the ammo count for a gun changes.
    /// </summary>
    protected virtual void UpdateAmmoCount(EntityUid uid, bool prediction = true) {}

    protected void SetCartridgeSpent(EntityUid uid, CartridgeAmmoComponent cartridge, bool spent)
    {
        if (cartridge.Spent != spent)
            DirtyField(uid, cartridge, nameof(CartridgeAmmoComponent.Spent));

        cartridge.Spent = spent;
        Appearance.SetData(uid, AmmoVisuals.Spent, spent);

        if (!cartridge.MarkSpentAsTrash)
            return;

        if (spent)
            TagSystem.AddTag(uid, TrashTag);
        else
            TagSystem.RemoveTag(uid, TrashTag);
    }

    /// <summary>
    /// Drops a single cartridge / shell
    /// </summary>
    protected void EjectCartridge(
        EntityUid entity,
        Angle? angle = null,
        bool playSound = true)
    {
        // TODO: Sound limit version.
        var offsetPos = Random.NextVector2(EjectOffset);
        var xform = Transform(entity);

        var coordinates = xform.Coordinates;
        coordinates = coordinates.Offset(offsetPos);

        TransformSystem.SetLocalRotation(entity, Random.NextAngle(), xform);
        TransformSystem.SetCoordinates(entity, xform, coordinates);

        // decides direction the casing ejects and only when not cycling
        if (angle != null)
        {
            Angle ejectAngle = angle.Value;
            ejectAngle += 3.7f; // 212 degrees; casings should eject slightly to the right and behind of a gun
            ThrowingSystem.TryThrow(entity, ejectAngle.ToVec().Normalized() / 100, 5f);
        }
        if (playSound && TryComp<CartridgeAmmoComponent>(entity, out var cartridge))
        {
            Audio.PlayPvs(cartridge.EjectSound, entity, AudioParams.Default.WithVariation(SharedContentAudioSystem.DefaultVariation).WithVolume(-1f));
        }
    }

    protected IShootable EnsureShootable(EntityUid uid)
    {
        if (TryComp<CartridgeAmmoComponent>(uid, out var cartridge))
            return cartridge;

        if (TryComp<HitscanAmmoComponent>(uid, out var hitscanAmmo))
            return hitscanAmmo;

        return EnsureComp<AmmoComponent>(uid);
    }

    protected void RemoveShootable(EntityUid uid)
    {
        RemCompDeferred<CartridgeAmmoComponent>(uid);
        RemCompDeferred<AmmoComponent>(uid);
    }

    protected void MuzzleFlash(EntityUid gun, AmmoComponent component, Angle worldAngle, EntityUid? user = null)
    {
        var attemptEv = new GunMuzzleFlashAttemptEvent();
        RaiseLocalEvent(gun, ref attemptEv);
        if (attemptEv.Cancelled)
            return;

        var sprite = component.MuzzleFlash;

        if (sprite == null)
            return;

        var ev = new MuzzleFlashEvent(GetNetEntity(gun), sprite, worldAngle);
        CreateEffect(gun, ev, user);
    }

    public void CauseImpulse(EntityCoordinates fromCoordinates, EntityCoordinates toCoordinates, EntityUid user, PhysicsComponent userPhysics)
    {
        var fromMap = TransformSystem.ToMapCoordinates(fromCoordinates).Position;
        var toMap = TransformSystem.ToMapCoordinates(toCoordinates).Position;
        var shotDirection = (toMap - fromMap).Normalized();

        const float impulseStrength = 25.0f;
        var impulseVector =  shotDirection * impulseStrength;
        Physics.ApplyLinearImpulse(user, -impulseVector, body: userPhysics);
    }

    public void RefreshModifiers(Entity<GunComponent?> gun)
    {
        if (!Resolve(gun, ref gun.Comp))
            return;

        var comp = gun.Comp;
        var ev = new GunRefreshModifiersEvent(
            (gun, comp),
            comp.SoundGunshot,
            comp.CameraRecoilScalar,
            comp.AngleIncrease,
            comp.AngleDecay,
            comp.MaxAngle,
            comp.MinAngle,
            comp.ShotsPerBurst,
            comp.FireRate,
            comp.ProjectileSpeed
        );

        RaiseLocalEvent(gun, ref ev);

        if (comp.SoundGunshotModified != ev.SoundGunshot)
        {
            comp.SoundGunshotModified = ev.SoundGunshot;
            DirtyField(gun, nameof(GunComponent.SoundGunshotModified));
        }

        if (!MathHelper.CloseTo(comp.CameraRecoilScalarModified, ev.CameraRecoilScalar))
        {
            comp.CameraRecoilScalarModified = ev.CameraRecoilScalar;
            DirtyField(gun, nameof(GunComponent.CameraRecoilScalarModified));
        }

        if (!comp.AngleIncreaseModified.EqualsApprox(ev.AngleIncrease))
        {
            comp.AngleIncreaseModified = ev.AngleIncrease;
            DirtyField(gun, nameof(GunComponent.AngleIncreaseModified));
        }

        if (!comp.AngleDecayModified.EqualsApprox(ev.AngleDecay))
        {
            comp.AngleDecayModified = ev.AngleDecay;
            DirtyField(gun, nameof(GunComponent.AngleDecayModified));
        }

        if (!comp.MaxAngleModified.EqualsApprox(ev.MaxAngle))
        {
            comp.MaxAngleModified = ev.MaxAngle;
            DirtyField(gun, nameof(GunComponent.MaxAngleModified));
        }

        if (!comp.MinAngleModified.EqualsApprox(ev.MinAngle))
        {
            comp.MinAngleModified = ev.MinAngle;
            DirtyField(gun, nameof(GunComponent.MinAngleModified));
        }

        if (comp.ShotsPerBurstModified != ev.ShotsPerBurst)
        {
            comp.ShotsPerBurstModified = ev.ShotsPerBurst;
            DirtyField(gun, nameof(GunComponent.ShotsPerBurstModified));
        }

        if (!MathHelper.CloseTo(comp.FireRateModified, ev.FireRate))
        {
            comp.FireRateModified = ev.FireRate;
            DirtyField(gun, nameof(GunComponent.FireRateModified));
        }

        if (!MathHelper.CloseTo(comp.ProjectileSpeedModified, ev.ProjectileSpeed))
        {
            comp.ProjectileSpeedModified = ev.ProjectileSpeed;
            DirtyField(gun, nameof(GunComponent.ProjectileSpeedModified));
        }
    }

    protected abstract void CreateEffect(EntityUid gunUid, MuzzleFlashEvent message, EntityUid? user = null);

    public abstract void PlayImpactSound(EntityUid otherEntity, DamageSpecifier? modifiedDamage, SoundSpecifier? weaponSound, bool forceWeaponSound);

    /// <summary>
    /// Used for animated effects on the client.
    /// </summary>
    [Serializable, NetSerializable]
    public sealed class HitscanEvent : EntityEventArgs
    {
        public List<(NetCoordinates coordinates, Angle angle, SpriteSpecifier Sprite, float Distance)> Sprites = new();
    }

    /// <summary>
    /// Get the ammo count for a given EntityUid. Can be a firearm or magazine.
    /// </summary>
    public int GetAmmoCount(EntityUid uid)
    {
        var ammoEv = new GetAmmoCountEvent();
        RaiseLocalEvent(uid, ref ammoEv);
        return ammoEv.Count;
    }

    /// <summary>
    /// Get the ammo capacity for a given EntityUid. Can be a firearm or magazine.
    /// </summary>
    public int GetAmmoCapacity(EntityUid uid)
    {
        var ammoEv = new GetAmmoCountEvent();
        RaiseLocalEvent(uid, ref ammoEv);
        return ammoEv.Capacity;
    }

    public override void Update(float frameTime)
    {
        UpdateBattery(frameTime);
    }

    // Stalker-Changes-Start
    public void SetAvailableModes(EntityUid uid, SelectiveFire newModes, GunComponent? gun = null)
    {
        if (!Resolve(uid, ref gun))
            return;

        // if selected mode not supported by new available modes
        if ((gun.SelectedMode & newModes) == 0)
        {
            var newCurrentSelective =
                newModes == 0 ?
                    SelectiveFire.Invalid :
                    newModes ^ ((newModes - 1) & newModes); // last bit that is one

            SelectFire(uid, gun, newCurrentSelective);
        }

        gun.AvailableModes = newModes;
        Dirty(uid, gun);
    }
    // Stalker-Changes-End
}

/// <summary>
///     Raised directed on the gun before firing to see if the shot should go through.
/// </summary>
/// <remarks>
///     Handling this in server exclusively will lead to mispredicts.
/// </remarks>
/// <param name="User">The user that attempted to fire this gun.</param>
/// <param name="Cancelled">Set this to true if the shot should be cancelled.</param>
/// <param name="ThrowItems">Set this to true if the ammo shouldn't actually be fired, just thrown.</param>
[ByRefEvent]
public record struct AttemptShootEvent(EntityUid User, string? Message, bool Cancelled = false, bool ThrowItems = false);

/// <summary>
///     Raised directed on the gun after firing.
/// </summary>
/// <param name="User">The user that fired this gun.</param>
[ByRefEvent]
public record struct GunShotEvent(EntityUid User, List<(EntityUid? Uid, IShootable Shootable)> Ammo, EntityCoordinates FromCoordinates, EntityCoordinates ToCoordinates);

/// <summary>
/// Raised on an entity after firing a gun to see if any components or systems would allow this entity to be pushed
/// by the gun they're firing. If true, GunSystem will create an impulse on our entity.
/// </summary>
[ByRefEvent]
public record struct ShooterImpulseEvent()
{
    public bool Push;
};

public enum EffectLayers : byte
{
    Unshaded,
}

[Serializable, NetSerializable]
public enum AmmoVisuals : byte
{
    Spent,
    AmmoCount,
    AmmoMax,
    HasAmmo, // used for generic visualizers. c# stuff can just check ammocount != 0
    MagLoaded,
    BoltClosed,
    // stalker-changes
    MuzzleEquiped,
    ScopeEquiped,
    UnderbarrelEquiped,
    SelectiveFireEquiped
    // stalker-changes ends
}
