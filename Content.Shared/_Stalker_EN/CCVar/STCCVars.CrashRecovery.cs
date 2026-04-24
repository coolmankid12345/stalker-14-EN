using Robust.Shared.Configuration;

namespace Content.Shared._Stalker_EN.CCVar;

public sealed partial class STCCVars
{
    /// <summary>
    /// Interval in seconds between automatic crash recovery snapshots of player equipment.
    /// Set to 0 to disable periodic saves.
    /// </summary>
    public static readonly CVarDef<float> CrashRecoverySaveInterval =
        CVarDef.Create("crash_recovery.save_interval", 300f, CVar.SERVERONLY);

    /// <summary>
    /// Whether the crash recovery system is enabled.
    /// </summary>
    public static readonly CVarDef<bool> CrashRecoveryEnabled =
        CVarDef.Create("crash_recovery.enabled", true, CVar.SERVERONLY);
}
