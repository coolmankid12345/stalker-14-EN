using System.Linq;
using Content.Server.GameTicking;
using Content.Shared._Stalker.Teleport;
using Content.Shared.Access.Systems;
using Content.Shared.Teleportation.Components;
using Content.Shared.Teleportation.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;

namespace Content.Server._Stalker.Teleports.TimedPortal;

public sealed class TimedPortalSystem : SharedTeleportSystem
{
    [Dependency] private readonly MapSystem _mapSystem = default!;
    [Dependency] private readonly LinkedEntitySystem _link = default!;
    [Dependency] private readonly AccessReaderSystem _accessReaderSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ILogManager _logManager = default!;

    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = _logManager.GetSawmill("TimedPortal");

        SubscribeLocalEvent<TimedPortalComponent, StartCollideEvent>(OnStartCollide);
        SubscribeLocalEvent<TimedPortalComponent, EndCollideEvent>(OnEndCollide);
        SubscribeLocalEvent<PostGameMapLoad>(OnPostGameMapLoad);
        SubscribeLocalEvent<TimedPortalComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(EntityUid uid, TimedPortalComponent component, MapInitEvent args)
    {
        UpdatePortalState(uid, component);
    }

    private void OnPostGameMapLoad(PostGameMapLoad args)
    {
        UpdateAllPortals();
    }

    /// <summary>
    /// Updates the status of all portals on the map
    /// </summary>
    private void UpdateAllPortals()
    {
        var query = EntityQueryEnumerator<TimedPortalComponent>();
        while (query.MoveNext(out var uid, out var portal))
        {
            UpdatePortalState(uid, portal);
        }
    }

    /// <summary>
    /// Updates the status of a specific portal based on the current UTC time
    /// </summary>
    private void UpdatePortalState(EntityUid uid, TimedPortalComponent portal)
    {
        var utcNow = DateTime.UtcNow;

        var isActive = IsPortalActive(portal, utcNow);

        if (!isActive)
        {
            RemoveAllLinks(uid);
        }
        else
        {
            UpdatePortalLinks(uid, portal);
        }
    }

    /// <summary>
    /// Removes all portal links
    /// </summary>
    private void RemoveAllLinks(EntityUid uid)
    {
        if (!TryComp<LinkedEntityComponent>(uid, out var link))
            return;

        var linkedEntities = link.LinkedEntities.ToList();

        foreach (var linkedEntity in linkedEntities)
        {
            _link.TryUnlink(uid, linkedEntity);
        }
    }

    /// <summary>
    /// Checks whether the portal is active at the specified UTC time
    /// </summary>
    private bool IsPortalActive(TimedPortalComponent portal, DateTime utcTime)
    {
        if (portal.ActiveDays != null && portal.ActiveDays.Count > 0)
        {
            var currentDay = utcTime.DayOfWeek;
            if (!portal.ActiveDays.Contains(currentDay))
                return false;
        }

        if (!portal.ActiveTimeStart.HasValue || !portal.ActiveTimeEnd.HasValue)
            return true;

        var currentTimeOfDay = utcTime.TimeOfDay;

        if (portal.ActiveTimeStart.Value <= portal.ActiveTimeEnd.Value)
        {
            if (currentTimeOfDay < portal.ActiveTimeStart.Value ||
                currentTimeOfDay > portal.ActiveTimeEnd.Value)
                return false;
        }
        else
        {
            // In case the period goes beyond midnight
            if (currentTimeOfDay < portal.ActiveTimeStart.Value &&
                currentTimeOfDay > portal.ActiveTimeEnd.Value)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Updates the portal's links with other portals
    /// </summary>
    private void UpdatePortalLinks(EntityUid uid, TimedPortalComponent portal)
    {
        RemoveAllLinks(uid);

        var maps = _mapSystem.GetAllMapIds();

        foreach (var map in maps)
        {
            if (!_mapSystem.TryGetMap(map, out var mapUid))
                return;

            if (portal.LinkOnlyToOtherMaps && Transform(uid).MapID == map)
                continue;

            var enumerator = Transform(mapUid.Value).ChildEnumerator;
            while (enumerator.MoveNext(out var otherPortalUid))
            {
                if (!TryComp<TimedPortalComponent>(otherPortalUid, out var otherPortal))
                    continue;

                if (otherPortalUid == uid)
                    continue;

                if (portal.PortalName != otherPortal.PortalName)
                    continue;

                if (!IsPortalActive(otherPortal, DateTime.UtcNow))
                    continue;

                _link.TryLink(uid, otherPortalUid);
                break;
            }
        }
    }

    private void OnStartCollide(EntityUid uid, TimedPortalComponent component, ref StartCollideEvent args)
    {
        if (!IsPortalActive(component, DateTime.UtcNow))
            return;

        if (!component.AllowAll)
        {
            if (!_accessReaderSystem.IsAllowed(args.OtherEntity, args.OurEntity))
                return;
        }

        if (component.IsCollisionDisabled)
            return;

        var subject = args.OtherEntity;

        if (TryComp<PortalTimeoutComponent>(subject, out var timeoutComponent) &&
            component.CooldownEnabled &&
            timeoutComponent.Cooldown.HasValue)
        {
            if (timeoutComponent.Cooldown > _timing.CurTime)
                return;
        }
        else if (HasComp<PortalTimeoutComponent>(subject))
            return;

        if (!TryComp<LinkedEntityComponent>(uid, out var link) || !link.LinkedEntities.Any())
        {
            UpdatePortalLinks(uid, component);

            if (!TryComp(uid, out link) || !link.LinkedEntities.Any())
            {
                _sawmill.Warning($"Portal {component.PortalName} has no active linked portals at this time");
                return;
            }
        }

        var target = link.LinkedEntities.FirstOrDefault();
        if (target == default)
        {
            _sawmill.Error($"Portal {component.PortalName} target is empty");
            return;
        }

        if (HasComp<TimedPortalComponent>(target))
        {
            var timeout = EnsureComp<PortalTimeoutComponent>(subject);

            if (component.CooldownEnabled)
                timeout.Cooldown = _timing.CurTime + TimeSpan.FromSeconds(component.CooldownTime);

            timeout.EnteredPortal = uid;
            Dirty(subject, timeout);
        }

        var xform = Transform(target);
        TeleportEntity(subject, xform.Coordinates);
    }

    private void OnEndCollide(EntityUid uid, TimedPortalComponent component, ref EndCollideEvent args)
    {
        var subject = args.OtherEntity;

        if (!TryComp<PortalTimeoutComponent>(subject, out var timeout) || timeout.EnteredPortal != uid)
            return;

        if (timeout.Cooldown.HasValue && timeout.Cooldown <= _timing.CurTime)
        {
            RemCompDeferred<PortalTimeoutComponent>(subject);
        }
        else if (!timeout.Cooldown.HasValue)
        {
            RemCompDeferred<PortalTimeoutComponent>(subject);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<TimedPortalComponent>();
        while (query.MoveNext(out var uid, out var portal))
        {
            if (_timing.CurTime < portal.NextStateCheck)
                continue;

            portal.NextStateCheck = _timing.CurTime + TimeSpan.FromSeconds(1);
            UpdatePortalState(uid, portal);
        }
    }
}
