using Robust.Shared.Configuration;

namespace Content.Shared._Stalker_EN.CCVar;

// CVars for emissions

public sealed partial class STCCVars
{
    /// <summary>
    ///     If true, then emissions will only ever be one color (primary color).
    /// </summary>
    public static readonly CVarDef<bool> EmissionSimpleVisuals =
        CVarDef.Create("stalkeren.emission.simplevisuals", false, CVar.CLIENTONLY);

    // TODO get rid of this when roofs get added
    /// <summary>
    ///     Should emission lightning do raycast checks to estimate what places
    ///         are indoors? This may be slightly expensive for performance.
    /// </summary>
    public static readonly CVarDef<bool> EmissionLightningRaycast =
        CVarDef.Create("stalkeren.emission.lightning_raycasting", true, CVar.SERVERONLY);
}
