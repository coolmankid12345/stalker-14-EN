using Content.Server._Stalker.AddCustomComponent;
using Content.Server._Stalker.StationEvents.Components;
using Content.Shared.Alert;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;

namespace Content.Server._Stalker.AddOrDelOnCollideSafeZone;

/// <summary>
/// This handles...
/// </summary>
public sealed class AddOrDelOnCollideSafeZoneSystem : EntitySystem
{
    [Dependency] private readonly AlertsSystem _alertsSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AddOrDelOnCollideSafeZoneComponent, StartCollideEvent>(OnCollide);
    }

    private void OnCollide(EntityUid uid, AddOrDelOnCollideSafeZoneComponent component, ref StartCollideEvent args)
    {
        // stalker-en change start: use BlowoutTargetComponent instead of ActorComponent so disconnected players are still protected
        if (!HasComp<BlowoutTargetComponent>(args.OtherEntity))
            return;

        _prototype.TryIndex<AlertPrototype>("StalkerSafeZone", out var stalkerSafeZoneAlert);
        if (stalkerSafeZoneAlert == null)
            return;
        if (component.MustAdd == true)
        {
            Logger.Debug("component.MustAdd==true");
            EnsureComp<StalkerSafeZoneComponent>(args.OtherEntity);
            _alertsSystem.ShowAlert(args.OtherEntity, stalkerSafeZoneAlert);
        }
        else
        {
            Logger.Debug("component.MustAdd==false");
            RemComp<StalkerSafeZoneComponent>(args.OtherEntity);
            _alertsSystem.ClearAlert(args.OtherEntity, stalkerSafeZoneAlert);
        }
        // stalker-en change end
    }
}
