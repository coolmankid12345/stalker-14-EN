using Content.Server._Stalker.ZoneArtifact.Components.Detector;
using Content.Server._Stalker.ZoneArtifact.Components.Spawner;
using Content.Server._Stalker.ZoneArtifact.Systems;
using Content.Server.Popups;
using Content.Shared._Stalker.ZoneAnomaly.Components;
using Content.Shared._Stalker_EN.Devices.Veles;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.UserInterface;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Stalker_EN.Devices.Veles;

public sealed class VelesSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly ZoneArtifactSpawnerSystem _artifactSpawner = default!;

    // Reusable list to avoid allocations per update
    private readonly List<VelesBlip> _blipBuffer = new();

    public override void Initialize()
    {
        base.Initialize();

        // Context menu verbs (anomaly detector only)
        SubscribeLocalEvent<VelesComponent, GetVerbsEvent<ActivationVerb>>(OnGetVerbs);

        // UI events
        SubscribeLocalEvent<VelesComponent, BeforeActivatableUIOpenEvent>(OnBeforeActivatableUIOpen);
        SubscribeLocalEvent<VelesComponent, BoundUIClosedEvent>(OnBoundUIClosed);

        // UI messages from buttons
        SubscribeLocalEvent<VelesComponent, VelesToggleAnomalyDetectorMessage>(OnToggleAnomalyDetectorMessage);
        SubscribeLocalEvent<VelesComponent, VelesToggleArtifactScannerMessage>(OnToggleArtifactScannerMessage);

        // Item events - disable when dropped/stored
        SubscribeLocalEvent<VelesComponent, GotUnequippedHandEvent>(OnGotUnequippedHand);
    }

    #region Context Menu Verbs

    private void OnGetVerbs(Entity<VelesComponent> entity, ref GetVerbsEvent<ActivationVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;

        // Anomaly Detector toggle
        if (TryComp<ZoneAnomalyDetectorComponent>(entity, out var detector))
        {
            args.Verbs.Add(new ActivationVerb
            {
                Text = Loc.GetString("veles-verb-toggle-anomaly-detector"),
                Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/dot.svg.192dpi.png")),
                Act = () => ToggleAnomalyDetector(entity, user)
            });
        }
    }

    #endregion

    #region UI Message Handlers

    private void OnToggleAnomalyDetectorMessage(Entity<VelesComponent> entity, ref VelesToggleAnomalyDetectorMessage args)
    {
        ToggleAnomalyDetector(entity, args.Actor);
    }

    private void OnToggleArtifactScannerMessage(Entity<VelesComponent> entity, ref VelesToggleArtifactScannerMessage args)
    {
        ToggleArtifactScanner(entity, args.Actor);
    }

    #endregion

    #region Toggle Methods

    private void ToggleAnomalyDetector(Entity<VelesComponent> entity, EntityUid user)
    {
        if (!TryComp<ZoneAnomalyDetectorComponent>(entity, out var detector))
            return;

        detector.Enabled = !detector.Enabled;

        var msg = detector.Enabled ? "veles-anomaly-detector-on" : "veles-anomaly-detector-off";
        _popup.PopupEntity(Loc.GetString(msg), entity, user);

        if (detector.Enabled)
            detector.NextBeepTime = _timing.CurTime;

        UpdateAppearance(entity);
        SendUIUpdate(entity, user);
    }

    private void ToggleArtifactScanner(Entity<VelesComponent> entity, EntityUid user)
    {
        entity.Comp.Enabled = !entity.Comp.Enabled;

        var msg = entity.Comp.Enabled ? "veles-artifact-scanner-on" : "veles-artifact-scanner-off";
        _popup.PopupEntity(Loc.GetString(msg), entity, user);

        if (entity.Comp.Enabled)
            entity.Comp.NextUpdateTime = _timing.CurTime;

        UpdateAppearance(entity);

        // Send radar update if scanner enabled and UI is open
        if (entity.Comp.Enabled && _ui.IsUiOpen(entity.Owner, VelesUiKey.Key))
        {
            UpdateRadar(entity, user);
        }
        else
        {
            SendUIUpdate(entity, user);
        }
    }

    private void UpdateAppearance(Entity<VelesComponent> entity)
    {
        var anomalyOn = TryComp<ZoneAnomalyDetectorComponent>(entity, out var d) && d.Enabled;
        var scannerOn = entity.Comp.Enabled;
        _appearance.SetData(entity, ZoneAnomalyDetectorVisuals.Enabled, anomalyOn || scannerOn);
    }

    private void OnGotUnequippedHand(Entity<VelesComponent> entity, ref GotUnequippedHandEvent args)
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

    private void OnBeforeActivatableUIOpen(Entity<VelesComponent> entity, ref BeforeActivatableUIOpenEvent args)
    {
        // Send initial state when UI opens - radar update if scanner enabled, otherwise just state
        if (entity.Comp.Enabled)
            UpdateRadar(entity, args.User);
        else
            SendUIUpdate(entity, args.User);
    }

    private void OnBoundUIClosed(Entity<VelesComponent> entity, ref BoundUIClosedEvent args)
    {
        if (args.UiKey is not VelesUiKey)
            return;

        // Disable artifact scanner when UI closes
        if (!entity.Comp.Enabled)
            return;

        entity.Comp.Enabled = false;
        UpdateAppearance(entity);
    }

    private void SendUIUpdate(Entity<VelesComponent> entity, EntityUid? user = null)
    {
        var anomalyEnabled = TryComp<ZoneAnomalyDetectorComponent>(entity, out var d) && d.Enabled;
        _blipBuffer.Clear();

        float? closestAnomalyDistance = null;
        if (anomalyEnabled && user != null)
            closestAnomalyDistance = GetClosestAnomalyDistance(entity, user.Value, d!);

        var state = new VelesBoundUIState(_blipBuffer, entity.Comp.DetectionRange, anomalyEnabled, entity.Comp.Enabled, closestAnomalyDistance);
        _ui.SetUiState(entity.Owner, VelesUiKey.Key, state);
    }

    private float? GetClosestAnomalyDistance(Entity<VelesComponent> entity, EntityUid user, ZoneAnomalyDetectorComponent detector)
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

        var query = EntityQueryEnumerator<VelesComponent>();
        while (query.MoveNext(out var uid, out var veles))
        {
            if (!veles.Enabled)
                continue;

            if (_timing.CurTime < veles.NextUpdateTime)
                continue;

            // Only update if UI is open
            if (!_ui.IsUiOpen(uid, VelesUiKey.Key))
                continue;

            var user = GetUser((uid, veles));
            if (user == null)
                continue;

            UpdateRadar((uid, veles), user.Value);
            veles.NextUpdateTime = _timing.CurTime + veles.UpdateInterval;
        }
    }

    private EntityUid? GetUser(Entity<VelesComponent> entity)
    {
        foreach (var actor in _ui.GetActors(entity.Owner, VelesUiKey.Key))
        {
            return actor;
        }

        return null;
    }

    private void UpdateRadar(Entity<VelesComponent> entity, EntityUid user)
    {
        _blipBuffer.Clear();

        var xformQuery = GetEntityQuery<TransformComponent>();
        if (!xformQuery.TryGetComponent(user, out var userXform))
            return;

        var userMapCoords = _transform.GetMapCoordinates(userXform);
        var userWorldPos = _transform.GetWorldPosition(userXform, xformQuery);

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

            // Calculate absolute angle to target
            // Convert from math coords (0=east, counterclockwise) to radar coords (0=north, clockwise)
            var targetAngle = new Angle(diff);
            var radarAngle = (float)(Math.PI / 2 - targetAngle.Theta);

            // Normalize to -PI to PI
            while (radarAngle > MathF.PI)
                radarAngle -= MathF.PI * 2;
            while (radarAngle < -MathF.PI)
                radarAngle += MathF.PI * 2;

            _blipBuffer.Add(new VelesBlip(GetNetEntity(target), radarAngle, distance, target.Comp.DetectedLevel));
        }

        var anomalyEnabled = TryComp<ZoneAnomalyDetectorComponent>(entity, out var d) && d.Enabled;

        float? closestAnomalyDistance = null;
        if (anomalyEnabled)
            closestAnomalyDistance = GetClosestAnomalyDistance(entity, user, d!);

        var state = new VelesBoundUIState(_blipBuffer, entity.Comp.DetectionRange, anomalyEnabled, entity.Comp.Enabled, closestAnomalyDistance);
        _ui.SetUiState(entity.Owner, VelesUiKey.Key, state);
    }

    #endregion
}
