using Content.Shared.Whitelist;
using Robust.Shared.GameStates;

namespace Content.Shared._Stalker.ZoneAnomaly.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ZoneAnomalyComponent : Component
{
    public bool Charged => State == ZoneAnomalyState.Idle;

    [DataField, AutoNetworkedField]
    public bool Detected = true;

    [DataField, AutoNetworkedField]
    public int DetectedLevel = 0;

    [DataField, AutoNetworkedField]
    public ZoneAnomalyState State = ZoneAnomalyState.Idle;

    //stalker-en-change-start: deleted all AutoNetworkedField cause those fields are Server only
    [DataField]
    public TimeSpan PreparingDelay = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan PreparingTime;

    [DataField]
    public TimeSpan ActivationDelay = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan ActivationTime;

    [DataField("chargeTime")]
    public TimeSpan ChargingDelay = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan ChargingTime;

    [DataField]
    public HashSet<EntityUid> Triggers = new();

    [DataField]
    public HashSet<EntityUid> InAnomaly = new();

    [DataField]
    public EntityWhitelist CollisionWhitelist = new();

    [DataField]
    //stalker-en-change-end
    public EntityWhitelist CollisionBlacklist = new();
}

public sealed partial class ZoneAnomalyStartCollideArgs : EventArgs
{
    public readonly EntityUid Anomaly;
    public readonly EntityUid OtherEntity;

    public bool Activate;

    public ZoneAnomalyStartCollideArgs(EntityUid anomaly, EntityUid otherEntity)
    {
        Anomaly = anomaly;
        OtherEntity = otherEntity;
    }
}

public sealed partial class ZoneAnomalyEndCollideArgs : EventArgs
{
    public readonly EntityUid Anomaly;
    public readonly EntityUid OtherEntity;
    public readonly bool IgnoreWhitelist;

    public bool Activate;

    public ZoneAnomalyEndCollideArgs(EntityUid anomaly, EntityUid otherEntity, bool ignoreWhitelist)
    {
        Anomaly = anomaly;
        OtherEntity = otherEntity;
        IgnoreWhitelist = ignoreWhitelist;
    }
}

[ByRefEvent]
public readonly record struct ZoneAnomalyChangedState(EntityUid Anomaly, ZoneAnomalyState Current, ZoneAnomalyState Previous);

[ByRefEvent]
public readonly record struct ZoneAnomalyActivateEvent(EntityUid Anomaly, HashSet<EntityUid> Triggers);

[ByRefEvent]
public readonly record struct ZoneAnomalyEntityAddEvent(EntityUid Anomaly, EntityUid Entity);

[ByRefEvent]
public readonly record struct ZoneAnomalyEntityRemoveEvent(EntityUid Anomaly, EntityUid Entity);

