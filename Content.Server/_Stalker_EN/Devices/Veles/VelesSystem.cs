using Content.Server._Stalker.ZoneArtifact.Components.Detector;
using Content.Server.Popups;
using Content.Shared._Stalker.ZoneAnomaly.Components;
using Content.Shared._Stalker_EN.Devices.Veles;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
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
    [Dependency] private readonly SharedMoverController _mover = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Context menu verbs
        SubscribeLocalEvent<VelesComponent, GetVerbsEvent<ActivationVerb>>(OnGetVerbs);

        // UI events
        SubscribeLocalEvent<VelesComponent, BeforeActivatableUIOpenEvent>(OnBeforeActivatableUIOpen);

        // UI messages from buttons
        SubscribeLocalEvent<VelesComponent, VelesToggleAnomalyDetectorMessage>(OnToggleAnomalyDetectorMessage);
        SubscribeLocalEvent<VelesComponent, VelesToggleArtifactScannerMessage>(OnToggleArtifactScannerMessage);
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

        // Artifact Scanner toggle
        args.Verbs.Add(new ActivationVerb
        {
            Text = Loc.GetString("veles-verb-toggle-artifact-scanner"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/dot.svg.192dpi.png")),
            Act = () => ToggleArtifactScanner(entity, user)
        });
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
        SendUIUpdate(entity);
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
            var uiUser = GetUser(entity);
            if (uiUser != null)
                UpdateRadar(entity, uiUser.Value);
        }
        else
        {
            SendUIUpdate(entity);
        }
    }

    private void UpdateAppearance(Entity<VelesComponent> entity)
    {
        var anomalyOn = TryComp<ZoneAnomalyDetectorComponent>(entity, out var d) && d.Enabled;
        var scannerOn = entity.Comp.Enabled;
        _appearance.SetData(entity, ZoneAnomalyDetectorVisuals.Enabled, anomalyOn || scannerOn);
    }

    #endregion

    #region UI Update

    private void OnBeforeActivatableUIOpen(Entity<VelesComponent> entity, ref BeforeActivatableUIOpenEvent args)
    {
        // Send initial state when UI opens - radar update if scanner enabled, otherwise just state
        if (entity.Comp.Enabled)
            UpdateRadar(entity, args.User);
        else
            SendUIUpdate(entity);
    }

    private void SendUIUpdate(Entity<VelesComponent> entity)
    {
        var anomalyEnabled = TryComp<ZoneAnomalyDetectorComponent>(entity, out var d) && d.Enabled;
        var blips = new List<VelesBlip>();

        var state = new VelesBoundUIState(blips, entity.Comp.DetectionRange, anomalyEnabled, entity.Comp.Enabled);
        _ui.SetUiState(entity.Owner, VelesUiKey.Key, state);
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
        var blips = new List<VelesBlip>();

        var xformQuery = GetEntityQuery<TransformComponent>();
        if (!xformQuery.TryGetComponent(user, out var userXform))
            return;

        var userMapCoords = _transform.GetMapCoordinates(userXform);
        var userWorldPos = _transform.GetWorldPosition(userXform, xformQuery);

        // Get the player's facing direction in world-space
        Angle userFacingAngle;
        if (TryComp<InputMoverComponent>(user, out var mover))
        {
            // Player with movement input - get actual facing direction
            userFacingAngle = _mover.GetParentGridAngle(mover);
        }
        else
        {
            // Fallback for entities without movement (NPCs, etc.)
            userFacingAngle = _transform.GetWorldRotation(userXform, xformQuery);
        }

        var entities = _entityLookup.GetEntitiesInRange<ZoneArtifactDetectorTargetComponent>(
            userMapCoords, entity.Comp.DetectionRange);

        foreach (var target in entities)
        {
            if (!target.Comp.Detectable)
                continue;

            if (target.Comp.DetectedLevel > entity.Comp.Level)
                continue;

            if (!InMap(target.Owner, xformQuery))
                continue;

            var targetXform = xformQuery.GetComponent(target);
            var targetWorldPos = _transform.GetWorldPosition(targetXform, xformQuery);

            var diff = targetWorldPos - userWorldPos;
            var distance = diff.Length();

            // Calculate angle relative to player's facing direction
            var targetAngle = Angle.FromWorldVec(diff);

            // Relative angle: target direction minus player facing direction
            var relativeAngle = (float)(targetAngle.Theta - userFacingAngle.Theta);

            // Normalize to -PI to PI
            while (relativeAngle > MathF.PI)
                relativeAngle -= MathF.PI * 2;
            while (relativeAngle < -MathF.PI)
                relativeAngle += MathF.PI * 2;

            // Only include blips within the 180-degree arc (-PI/2 to PI/2)
            if (relativeAngle < -MathF.PI / 2 || relativeAngle > MathF.PI / 2)
                continue;

            blips.Add(new VelesBlip(relativeAngle, distance, target.Comp.DetectedLevel));
        }

        var anomalyEnabled = TryComp<ZoneAnomalyDetectorComponent>(entity, out var d) && d.Enabled;
        var state = new VelesBoundUIState(blips, entity.Comp.DetectionRange, anomalyEnabled, entity.Comp.Enabled);
        _ui.SetUiState(entity.Owner, VelesUiKey.Key, state);
    }

    private bool InMap(EntityUid entityUid, EntityQuery<TransformComponent> xformQuery)
    {
        var parent = xformQuery.GetComponent(entityUid).ParentUid;
        return HasComp<MapComponent>(parent) || HasComp<MapGridComponent>(parent);
    }

    #endregion
}
