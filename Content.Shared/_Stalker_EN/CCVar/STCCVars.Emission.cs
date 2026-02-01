using Robust.Shared.Configuration;

namespace Content.Shared._Stalker_EN.CCVar;

// CVars for emissions

public sealed partial class STCCVars
{
    /// <summary>
    /// If true, then emissions will only ever be one color (primary color).
    /// </summary>
    public static readonly CVarDef<bool> EmissionSimpleVisuals =
        CVarDef.Create("stalker.emission.simplevisuals", false, CVar.CLIENTONLY);
}
