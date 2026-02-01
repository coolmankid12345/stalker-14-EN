using Robust.Shared.Configuration;

namespace Content.Shared._Stalker_EN.CCVar;

// CVars for loadout system

public sealed partial class STCCVars
{
    /// <summary>
    /// Rate limit period in seconds for loadout operations.
    /// </summary>
    public static readonly CVarDef<float> LoadoutRateLimitPeriod =
        CVarDef.Create("loadout.rate_limit_period", 5f, CVar.SERVERONLY);

    /// <summary>
    /// Maximum number of loadout operations allowed within the rate limit period.
    /// </summary>
    public static readonly CVarDef<int> LoadoutRateLimitCount =
        CVarDef.Create("loadout.rate_limit_count", 3, CVar.SERVERONLY);
}
