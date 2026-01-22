using Content.Server.Popups;
using Content.Shared._Stalker.ZoneAnomaly.Components;
using Content.Shared._Stalker_EN.Devices.Radar;
using Content.Shared._Stalker_EN.Devices.Radar.Components;
using Content.Shared.Hands;
using Content.Shared.UserInterface;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Stalker_EN.Devices.Radar.EntitySystems;

public sealed class RadarDisplaySystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    // Reusable list to avoid allocations per update
    private readonly List<RadarBlip> _blipBuffer = new();

    public override void Initialize()
    {
        base.Initialize();

        // Context menu verbs (anomaly detector only)
        SubscribeLocalEvent<RadarDisplayComponent, GetVerbsEvent<ActivationVerb>>(OnGetVerbs);

        // UI events
        SubscribeLocalEvent<RadarDisplayComponent, BeforeActivatableUIOpenEvent>(OnBeforeActivatableUIOpen);
        SubscribeLocalEvent<RadarDisplayComponent, BoundUIClosedEvent>(OnBoundUIClosed);

        // UI messages from buttons
        SubscribeLocalEvent<RadarDisplayComponent, RadarToggleAnomalyDetectorMessage>(OnToggleAnomalyDetectorMessage);
        SubscribeLocalEvent<RadarDisplayComponent, RadarToggleArtifactScannerMessage>(OnToggleRadarMessage);

        // Item events - disable when dropped/stored
        SubscribeLocalEvent<RadarDisplayComponent, GotUnequippedHandEvent>(OnGotUnequippedHand);
    }

    #region Context Menu Verbs

    private void OnGetVerbs(Entity<RadarDisplayComponent> entity, ref GetVerbsEvent<ActivationVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;

        // Anomaly Detector toggle
        if (TryComp<ZoneAnomalyDetectorComponent>(entity, out var detector))
        {
            args.Verbs.Add(new ActivationVerb
            {
                Text = Loc.GetString("artifact-radar-verb-toggle-anomaly-detector"),
                Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/dot.svg.192dpi.png")),
                Act = () => ToggleAnomalyDetector(entity, user)
            });
        }
    }

    #endregion

    #region UI Message Handlers

    private void OnToggleAnomalyDetectorMessage(Entity<RadarDisplayComponent> entity, ref RadarToggleAnomalyDetectorMessage args)
    {
        ToggleAnomalyDetector(entity, args.Actor);
    }

    private void OnToggleRadarMessage(Entity<RadarDisplayComponent> entity, ref RadarToggleArtifactScannerMessage args)
    {
        ToggleRadar(entity, args.Actor);
    }

    #endregion

    #region Toggle Methods

    private void ToggleAnomalyDetector(Entity<RadarDisplayComponent> entity, EntityUid user)
    {
        if (!TryComp<ZoneAnomalyDetectorComponent>(entity, out var detector))
            return;

        detector.Enabled = !detector.Enabled;

        var msg = detector.Enabled ? "artifact-radar-anomaly-detector-on" : "artifact-radar-anomaly-detector-off";
        _popup.PopupEntity(Loc.GetString(msg), entity, user);

        if (detector.Enabled)
            detector.NextBeepTime = _timing.CurTime;

        UpdateAppearance(entity);
        SendUIUpdate(entity, user);
    }

    private void ToggleRadar(Entity<RadarDisplayComponent> entity, EntityUid user)
    {
        entity.Comp.Enabled = !entity.Comp.Enabled;

        var msg = entity.Comp.Enabled ? "artifact-radar-artifact-scanner-on" : "artifact-radar-artifact-scanner-off";
        _popup.PopupEntity(Loc.GetString(msg), entity, user);

        if (entity.Comp.Enabled)
            entity.Comp.NextUpdateTime = _timing.CurTime;

        UpdateAppearance(entity);

        // Send radar update if enabled and UI is open
        if (entity.Comp.Enabled && _ui.IsUiOpen(entity.Owner, RadarDisplayUiKey.Key))
        {
            UpdateRadar(entity, user);
        }
        else
        {
            SendUIUpdate(entity, user);
        }
    }

    private void UpdateAppearance(Entity<RadarDisplayComponent> entity)
    {
        var anomalyOn = TryComp<ZoneAnomalyDetectorComponent>(entity, out var d) && d.Enabled;
        var radarOn = entity.Comp.Enabled;
        _appearance.SetData(entity, ZoneAnomalyDetectorVisuals.Enabled, anomalyOn || radarOn);
    }

    private void OnGotUnequippedHand(Entity<RadarDisplayComponent> entity, ref GotUnequippedHandEvent args)
    {
        // Disable radar display when unequipped, but keep anomaly detector (beeping) enabled
        // The beeping system doesn't require the item to be in hand - it just checks position and plays sound
        entity.Comp.Enabled = false;

        UpdateAppearance(entity);
    }

    #endregion

    #region UI Update

    private void OnBeforeActivatableUIOpen(Entity<RadarDisplayComponent> entity, ref BeforeActivatableUIOpenEvent args)
    {
        // Auto-enable radar when UI opens for better UX
        if (!entity.Comp.Enabled)
        {
            entity.Comp.Enabled = true;
            entity.Comp.NextUpdateTime = _timing.CurTime;
            UpdateAppearance(entity);
        }

        UpdateRadar(entity, args.User);
    }

    private void OnBoundUIClosed(Entity<RadarDisplayComponent> entity, ref BoundUIClosedEvent args)
    {
        if (args.UiKey is not RadarDisplayUiKey)
            return;

        // Disable radar when UI closes (auto-enables on next open)
        entity.Comp.Enabled = false;
        UpdateAppearance(entity);
    }

    private void SendUIUpdate(Entity<RadarDisplayComponent> entity, EntityUid? user = null)
    {
        var hasAnomalyDetector = TryComp<ZoneAnomalyDetectorComponent>(entity, out var detector);
        var anomalyEnabled = hasAnomalyDetector && detector!.Enabled;
        _blipBuffer.Clear();

        float? closestAnomalyDistance = null;
        if (anomalyEnabled && user != null)
            closestAnomalyDistance = GetClosestAnomalyDistance(user.Value, detector!);

        // Check for radar capability components
        var hasArtifactDetection = HasComp<ArtifactRadarTargetSourceComponent>(entity);
        var hasAnomalyDetection = HasComp<AnomalyRadarTargetSourceComponent>(entity);

        var deviceName = Name(entity);
        var state = new RadarDisplayBoundUIState(
            _blipBuffer,
            entity.Comp.DisplayRange,
            entity.Comp.Enabled,
            hasAnomalyDetector,
            anomalyEnabled,
            deviceName,
            hasArtifactDetection,
            hasAnomalyDetection,
            closestAnomalyDistance);
        _ui.SetUiState(entity.Owner, RadarDisplayUiKey.Key, state);
    }

    private float? GetClosestAnomalyDistance(EntityUid user, ZoneAnomalyDetectorComponent detector)
    {
        var xformQuery = GetEntityQuery<TransformComponent>();
        if (!xformQuery.TryGetComponent(user, out var userXform))
            return null;

        var userMapCoords = _transform.GetMapCoordinates(userXform);
        var userWorldPos = _transform.GetWorldPosition(userXform, xformQuery);

        float? closestDistance = null;
        foreach (var ent in _entityLookup.GetEntitiesInRange<ZoneAnomalyComponent>(userMapCoords, detector.Distance))
        {
            if (!ent.Comp.Detected || ent.Comp.DetectedLevel > detector.Level)
                continue;

            var dist = (userWorldPos - _transform.GetWorldPosition(ent, xformQuery)).Length();
            if (dist < (closestDistance ?? float.MaxValue))
                closestDistance = dist;
        }

        return closestDistance;
    }

    #endregion

    #region Radar Update Loop

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<RadarDisplayComponent>();
        while (query.MoveNext(out var uid, out var radar))
        {
            if (!radar.Enabled)
                continue;

            if (_timing.CurTime < radar.NextUpdateTime)
                continue;

            // Only update if UI is open
            if (!_ui.IsUiOpen(uid, RadarDisplayUiKey.Key))
                continue;

            var user = GetUser((uid, radar));
            if (user == null)
                continue;

            UpdateRadar((uid, radar), user.Value);
            radar.NextUpdateTime = _timing.CurTime + radar.UpdateInterval;
        }
    }

    private EntityUid? GetUser(Entity<RadarDisplayComponent> entity)
    {
        foreach (var actor in _ui.GetActors(entity.Owner, RadarDisplayUiKey.Key))
        {
            return actor;
        }

        return null;
    }

    private void UpdateRadar(Entity<RadarDisplayComponent> entity, EntityUid user)
    {
        _blipBuffer.Clear();

        var xformQuery = GetEntityQuery<TransformComponent>();
        if (!xformQuery.TryGetComponent(user, out var userXform))
            return;

        var userMapCoords = _transform.GetMapCoordinates(userXform);
        var userWorldPos = _transform.GetWorldPosition(userXform, xformQuery);
        var userGridUid = userXform.GridUid;

        // Raise event to collect blips from target sources
        var ev = new RadarTargetSourceUpdateEvent(
            user,
            userMapCoords,
            userWorldPos,
            userGridUid,
            _blipBuffer);
        RaiseLocalEvent(entity, ref ev);

        var hasAnomalyDetector = TryComp<ZoneAnomalyDetectorComponent>(entity, out var detector);
        var anomalyEnabled = hasAnomalyDetector && detector!.Enabled;

        float? closestAnomalyDistance = null;
        if (anomalyEnabled)
            closestAnomalyDistance = GetClosestAnomalyDistance(user, detector!);

        // Check for radar capability components
        var hasArtifactDetection = HasComp<ArtifactRadarTargetSourceComponent>(entity);
        var hasAnomalyDetection = HasComp<AnomalyRadarTargetSourceComponent>(entity);

        var deviceName = Name(entity);
        var state = new RadarDisplayBoundUIState(
            _blipBuffer,
            entity.Comp.DisplayRange,
            entity.Comp.Enabled,
            hasAnomalyDetector,
            anomalyEnabled,
            deviceName,
            hasArtifactDetection,
            hasAnomalyDetection,
            closestAnomalyDistance);
        _ui.SetUiState(entity.Owner, RadarDisplayUiKey.Key, state);
    }

    #endregion
}
