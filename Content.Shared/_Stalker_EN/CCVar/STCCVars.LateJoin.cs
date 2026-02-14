using Robust.Shared.Configuration;

namespace Content.Shared._Stalker_EN.CCVar;

// CVars for late-join behavior

public sealed partial class STCCVars
{
    /// <summary>
    ///     If true, disables the station announcement when a player late-joins.
    /// </summary>
    public static readonly CVarDef<bool> DisableLateJoinAnnouncement =
        CVarDef.Create("stalkeren.latejoin.disable_announcement", true, CVar.SERVERONLY);
}
