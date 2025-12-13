namespace Content.Server._Stalker.Teleports.TimedPortal;

/// <summary>
/// Component for scheduled portals
/// </summary>
[RegisterComponent]
public sealed partial class TimedPortalComponent : Component
{
    /// <summary>
    /// The unique name of the portal to link
    /// </summary>
    [DataField(required: true)]
    public string PortalName = string.Empty;

    /// <summary>
    /// Days of the week when the portal is active (if empty, it's always active)
    /// </summary>
    [DataField]
    public HashSet<DayOfWeek>? ActiveDays;

    /// <summary>
    /// Start time of activity (UTC)
    /// </summary>
    [DataField]
    public TimeSpan? ActiveTimeStart;

    /// <summary>
    /// End time of activity (UTC)
    /// </summary>
    [DataField]
    public TimeSpan? ActiveTimeEnd;

    /// <summary>
    /// Link only to portals on other maps
    /// </summary>
    [DataField]
    public bool LinkOnlyToOtherMaps = true;

    /// <summary>
    /// If true, allows everyone to use the portal
    /// </summary>
    [DataField]
    public bool AllowAll;

    /// <summary>
    /// If true, disables the portal from teleporting
    /// </summary>
    [DataField]
    public bool IsCollisionDisabled;

    /// <summary>
    /// Is cooldown enabled?
    /// </summary>
    [DataField]
    public bool CooldownEnabled;

    /// <summary>
    /// Cooldown time in seconds
    /// </summary>
    [DataField]
    public float CooldownTime;

    /// <summary>
    /// Time before next check of the state of the portal
    /// </summary>
    public TimeSpan NextStateCheck = TimeSpan.Zero;
}
