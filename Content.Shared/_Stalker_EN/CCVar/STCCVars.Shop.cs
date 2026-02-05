using Robust.Shared.Configuration;

namespace Content.Shared._Stalker_EN.CCVar;

public sealed partial class STCCVars
{
    /// <summary>
    /// Comma-separated list of item prototype IDs for which the sell confirmation popup is suppressed.
    /// Persisted client-side across sessions.
    /// </summary>
    public static readonly CVarDef<string> ShopSuppressedSellConfirmations =
        CVarDef.Create("st.shop.suppressed_sell_confirmations", "", CVar.CLIENTONLY | CVar.ARCHIVE);
}
