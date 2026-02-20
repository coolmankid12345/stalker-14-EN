using Content.Shared._Stalker.Teleport;
using Content.Shared._Stalker_EN.PvpZone;
using Content.Shared.Alert;
using Content.Shared.GameTicking;
using Robust.Shared.Map;
using Robust.Shared.Physics.Events;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Stalker_EN.PvpZone;

/// <summary>
/// Manages PvP zone indicators for players. Shows a colored alert icon based on the
/// current zone type (Green/Gray/Yellow/Red/Black/Faction).
/// Zone is determined by: area override trigger > map default > Yellow fallback.
/// </summary>
public sealed class STPvpZoneSystem : EntitySystem
{
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;

    private static readonly Dictionary<STPvpZoneType, ProtoId<AlertPrototype>> ZoneAlerts = new()
    {
        { STPvpZoneType.Green, "STPvpZoneGreen" },
        { STPvpZoneType.Gray, "STPvpZoneGray" },
        { STPvpZoneType.Yellow, "STPvpZoneYellow" },
        { STPvpZoneType.Red, "STPvpZoneRed" },
        { STPvpZoneType.Black, "STPvpZoneBlack" },
        { STPvpZoneType.Faction, "STPvpZoneFaction" },
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STPvpZoneTriggerComponent, StartCollideEvent>(OnTriggerCollide);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawn);
        SubscribeLocalEvent<EntParentChangedMessage>(OnParentChanged);
        SubscribeLocalEvent<AfterEntityTeleportedEvent>(OnAfterTeleport);
        SubscribeLocalEvent<STPlayerZoneComponent, PlayerAttachedEvent>(OnPlayerAttached);
    }

    private void OnTriggerCollide(EntityUid uid, STPvpZoneTriggerComponent trigger, ref StartCollideEvent args)
    {
        if (!TryComp<STPlayerZoneComponent>(args.OtherEntity, out var playerZone))
            return;

        if (trigger.IsEntering)
        {
            playerZone.OverrideZone = trigger.Zone;
        }
        else
        {
            playerZone.OverrideZone = null;
        }

        UpdateZoneAlert((args.OtherEntity, playerZone));
    }

    private void OnPlayerSpawn(PlayerSpawnCompleteEvent args)
    {
        var comp = EnsureComp<STPlayerZoneComponent>(args.Mob);
        UpdateZoneAlert((args.Mob, comp));
    }

    private void OnParentChanged(ref EntParentChangedMessage args)
    {
        if (!TryComp<STPlayerZoneComponent>(args.Entity, out var playerZone))
            return;

        // Clear area override when changing maps — triggers are map-local
        if (args.OldParent != null
            && TryComp<TransformComponent>(args.Entity, out var xform)
            && Transform(args.OldParent.Value).MapID != xform.MapID)
        {
            playerZone.OverrideZone = null;
        }

        UpdateZoneAlert((args.Entity, playerZone));
    }

    private void OnAfterTeleport(ref AfterEntityTeleportedEvent args)
    {
        if (!TryComp<STPlayerZoneComponent>(args.EntityUid, out var playerZone))
            return;

        // Clear area override when teleporting between maps
        if (args.Origin != args.Destination)
        {
            playerZone.OverrideZone = null;
        }

        UpdateZoneAlert((args.EntityUid, playerZone));
    }

    private void OnPlayerAttached(Entity<STPlayerZoneComponent> entity, ref PlayerAttachedEvent args)
    {
        UpdateZoneAlert(entity);
    }

    private void UpdateZoneAlert(Entity<STPlayerZoneComponent> entity)
    {
        var effectiveZone = entity.Comp.OverrideZone ?? ResolveMapDefault(entity) ?? STPvpZoneType.Yellow;
        entity.Comp.CurrentZone = effectiveZone;
        Dirty(entity);

        if (ZoneAlerts.TryGetValue(effectiveZone, out var alertId))
        {
            _alerts.ShowAlert(entity.Owner, alertId);
        }
    }

    private STPvpZoneType? ResolveMapDefault(EntityUid entity)
    {
        var xform = Transform(entity);

        if (xform.MapID == MapId.Nullspace)
            return null;

        var mapEntity = _mapManager.GetMapEntityId(xform.MapID);

        if (TryComp<STMapDefaultZoneComponent>(mapEntity, out var mapZone))
            return mapZone.Zone;

        return null;
    }
}
