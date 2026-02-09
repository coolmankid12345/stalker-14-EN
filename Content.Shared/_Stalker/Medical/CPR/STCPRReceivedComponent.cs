namespace Content.Shared._Stalker.Medical.CPR;

[RegisterComponent]
public sealed partial class STCPRReceivedComponent : Component
{
    [ViewVariables]
    public TimeSpan LastCPRTime;
}
