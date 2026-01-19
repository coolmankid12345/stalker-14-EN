using Content.Server._Stalker.ZoneArtifact.Components.Detector;
using Content.Server._Stalker.ZoneArtifact.Components.Spawner;
using Content.Server._Stalker.ZoneArtifact.Systems;
using Content.Server.Popups;
using Content.Shared._Stalker.ZoneAnomaly.Components;
using Content.Shared._Stalker_EN.Devices.ArtifactRadar;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.UserInterface;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Stalker_EN.Devices.ArtifactRadar;

public sealed class ArtifactRadarSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly ZoneArtifactSpawnerSystem _artifactSpawner = default!;

    // Reusable list to avoid allocations per update
    private readonly List<ArtifactRadarBlip> _blipBuffer = new();

    public override void Initialize()
    {
        base.Initialize();

        // Context menu verbs (anomaly detector only)
        SubscribeLocalEvent<ArtifactRadarComponent, GetVerbsEvent<ActivationVerb>>(OnGetVerbs);

        // UI events
        SubscribeLocalEvent<ArtifactRadarComponent, BeforeActivatableUIOpenEvent>(OnBeforeActivatableUIOpen);
        SubscribeLocalEvent<ArtifactRadarComponent, BoundUIClosedEvent>(OnBoundUIClosed);

        // UI messages from buttons
        SubscribeLocalEvent<ArtifactRadarComponent, ArtifactRadarToggleAnomalyDetectorMessage>(OnToggleAnomalyDetectorMessage);
        SubscribeLocalEvent<ArtifactRadarComponent, ArtifactRadarToggleArtifactScannerMessage>(OnToggleArtifactScannerMessage);

        // Item events - disable when dropped/stored
        SubscribeLocalEvent<ArtifactRadarComponent, GotUnequippedHandEvent>(OnGotUnequippedHand);
    }

    #region Context Menu Verbs

    private void OnGetVerbs(Entity<ArtifactRadarComponent> entity, ref GetVerbsEvent<ActivationVerb> args)
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

    private void OnToggleAnomalyDetectorMessage(Entity<ArtifactRadarComponent> entity, ref ArtifactRadarToggleAnomalyDetectorMessage args)
    {
        ToggleAnomalyDetector(entity, args.Actor);
    }

    private void OnToggleArtifactScannerMessage(Entity<ArtifactRadarComponent> entity, ref ArtifactRadarToggleArtifactScannerMessage args)
    {
        ToggleArtifactScanner(entity, args.Actor);
    }

    #endregion

    #region Toggle Methods

    private void ToggleAnomalyDetector(Entity<ArtifactRadarComponent> entity, EntityUid user)
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

    private void ToggleArtifactScanner(Entity<ArtifactRadarComponent> entity, EntityUid user)
    {
        entity.Comp.Enabled = !entity.Comp.Enabled;

        var msg = entity.Comp.Enabled ? "artifact-radar-artifact-scanner-on" : "artifact-radar-artifact-scanner-off";
        _popup.PopupEntity(Loc.GetString(msg), entity, user);

        if (entity.Comp.Enabled)
            entity.Comp.NextUpdateTime = _timing.CurTime;

        UpdateAppearance(entity);

        // Send radar update if scanner enabled and UI is open
        if (entity.Comp.Enabled && _ui.IsUiOpen(entity.Owner, ArtifactRadarUiKey.Key))
        {
            UpdateRadar(entity, user);
        }
        else
        {
            SendUIUpdate(entity, user);
        }
    }

    private void UpdateAppearance(Entity<ArtifactRadarComponent> entity)
    {
        var anomalyOn = TryComp<ZoneAnomalyDetectorComponent>(entity, out var d) && d.Enabled;
        var scannerOn = entity.Comp.Enabled;
        _appearance.SetData(entity, ZoneAnomalyDetectorVisuals.Enabled, anomalyOn || scannerOn);
    }

    private void OnGotUnequippedHand(Entity<ArtifactRadarComponent> entity, ref GotUnequippedHandEvent args)
    {
        // Disable anomaly detector
        if (TryComp<ZoneAnomalyDetectorComponent>(entity, out var detector))
            detector.Enabled = false;

        // Disable artifact scanner
        entity.Comp.Enabled = false;

        UpdateAppearance(entity);
    }

    #endregion

    #region UI Update

    private void OnBeforeActivatableUIOpen(Entity<ArtifactRadarComponent> entity, ref BeforeActivatableUIOpenEvent args)
    {
        // Send initial state when UI opens - radar update if scanner enabled, otherwise just state
        if (entity.Comp.Enabled)
            UpdateRadar(entity, args.User);
        else
            SendUIUpdate(entity, args.User);
    }

    private void OnBoundUIClosed(Entity<ArtifactRadarComponent> entity, ref BoundUIClosedEvent args)
    {
        if (args.UiKey is not ArtifactRadarUiKey)
            return;

        // Disable artifact scanner when UI closes
        if (!entity.Comp.Enabled)
            return;

        entity.Comp.Enabled = false;
        UpdateAppearance(entity);
    }

    private void SendUIUpdate(Entity<ArtifactRadarComponent> entity, EntityUid? user = null)
    {
        var hasAnomalyDetector = TryComp<ZoneAnomalyDetectorComponent>(entity, out var detector);
        var anomalyEnabled = hasAnomalyDetector && detector!.Enabled;
        _blipBuffer.Clear();

        float? closestAnomalyDistance = null;
        if (anomalyEnabled && user != null)
            closestAnomalyDistance = GetClosestAnomalyDistance(entity, user.Value, detector!);

        var deviceName = Name(entity);
        var state = new ArtifactRadarBoundUIState(
            _blipBuffer,
            entity.Comp.DetectionRange,
            entity.Comp.Enabled,
            hasAnomalyDetector,
            anomalyEnabled,
            deviceName,
            closestAnomalyDistance);
        _ui.SetUiState(entity.Owner, ArtifactRadarUiKey.Key, state);
    }

    private float? GetClosestAnomalyDistance(Entity<ArtifactRadarComponent> entity, EntityUid user, ZoneAnomalyDetectorComponent detector)
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

        var query = EntityQueryEnumerator<ArtifactRadarComponent>();
        while (query.MoveNext(out var uid, out var radar))
        {
            if (!radar.Enabled)
                continue;

            if (_timing.CurTime < radar.NextUpdateTime)
                continue;

            // Only update if UI is open
            if (!_ui.IsUiOpen(uid, ArtifactRadarUiKey.Key))
                continue;

            var user = GetUser((uid, radar));
            if (user == null)
                continue;

            UpdateRadar((uid, radar), user.Value);
            radar.NextUpdateTime = _timing.CurTime + radar.UpdateInterval;
        }
    }

    private EntityUid? GetUser(Entity<ArtifactRadarComponent> entity)
    {
        foreach (var actor in _ui.GetActors(entity.Owner, ArtifactRadarUiKey.Key))
        {
            return actor;
        }

        return null;
    }

    private void UpdateRadar(Entity<ArtifactRadarComponent> entity, EntityUid user)
    {
        _blipBuffer.Clear();

        var xformQuery = GetEntityQuery<TransformComponent>();
        if (!xformQuery.TryGetComponent(user, out var userXform))
            return;

        var userMapCoords = _transform.GetMapCoordinates(userXform);
        var userWorldPos = _transform.GetWorldPosition(userXform, xformQuery);

        // Get the grid the user is on for consistent angle calculation
        var userGridUid = userXform.GridUid;
        MapGridComponent? userGrid = null;
        if (userGridUid != null)
            TryComp(userGridUid.Value, out userGrid);

        var entities = _entityLookup.GetEntitiesInRange<ZoneArtifactDetectorTargetComponent>(
            userMapCoords, entity.Comp.DetectionRange);

        foreach (var target in entities)
        {
            if (!target.Comp.Detectable)
                continue;

            if (target.Comp.DetectedLevel > entity.Comp.Level)
                continue;

            var targetXform = xformQuery.GetComponent(target);
            var targetWorldPos = _transform.GetWorldPosition(targetXform, xformQuery);

            var diff = targetWorldPos - userWorldPos;
            var distance = diff.Length();

            // Skip spawners that don't have artifacts ready (empty cooldown spawners)
            if (TryComp<ZoneArtifactSpawnerComponent>(target, out var spawner))
            {
                if (!_artifactSpawner.Ready((target, spawner)))
                    continue;

                // Spawn artifact if player is close enough (like regular detectors do)
                if (distance <= entity.Comp.ActivationDistance)
                {
                    _artifactSpawner.TrySpawn((target, spawner));
                    continue; // Spawner no longer ready - actual artifact will be found next update
                }
            }

            // Calculate angle in grid-local space for consistency across restarts
            float radarAngle;
            if (userGrid != null && userGridUid != null)
            {
                // Convert positions to grid-local coordinates
                var userLocalPos = _map.WorldToLocal(userGridUid.Value, userGrid, userWorldPos);
                var targetLocalPos = _map.WorldToLocal(userGridUid.Value, userGrid, targetWorldPos);
                var localDiff = targetLocalPos - userLocalPos;

                // Angle in grid-local space (consistent regardless of grid rotation)
                var localAngle = new Angle(localDiff);
                radarAngle = (float)(Math.PI / 2 - localAngle.Theta);
            }
            else
            {
                // Fallback to world-space if not on a grid
                var worldAngle = new Angle(diff);
                radarAngle = (float)(Math.PI / 2 - worldAngle.Theta);
            }

            // Normalize to -PI to PI range
            while (radarAngle > MathF.PI)
                radarAngle -= MathF.PI * 2;
            while (radarAngle < -MathF.PI)
                radarAngle += MathF.PI * 2;

            _blipBuffer.Add(new ArtifactRadarBlip(GetNetEntity(target), radarAngle, distance, target.Comp.DetectedLevel));
        }

        var hasAnomalyDetector = TryComp<ZoneAnomalyDetectorComponent>(entity, out var detector);
        var anomalyEnabled = hasAnomalyDetector && detector!.Enabled;

        float? closestAnomalyDistance = null;
        if (anomalyEnabled)
            closestAnomalyDistance = GetClosestAnomalyDistance(entity, user, detector!);

        var deviceName = Name(entity);
        var state = new ArtifactRadarBoundUIState(
            _blipBuffer,
            entity.Comp.DetectionRange,
            entity.Comp.Enabled,
            hasAnomalyDetector,
            anomalyEnabled,
            deviceName,
            closestAnomalyDistance);
        _ui.SetUiState(entity.Owner, ArtifactRadarUiKey.Key, state);
    }

    #endregion
}
