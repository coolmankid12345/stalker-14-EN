using Content.Client.UserInterface.ControlExtensions;
using Content.Client._Stalker_EN.UI.Controls;
using Robust.Client.UserInterface;
using Robust.Shared.Utility;

namespace Content.Client._Stalker_EN.PdaMessenger;

/// <summary>
/// Markup tag handler for clickable offer references in messenger messages.
/// Renders <c>[offerlink=3 prefix=MB#][/offerlink]</c> as a blue clickable "[MB#3]" label.
/// Supports any board prefix (MB#, TB#, etc.).
/// Click handling is delegated to the nearest parent implementing <see cref="IOfferLinkClickHandler"/>.
/// </summary>
public sealed class OfferLinkTag : PdaLinkTag
{
    /// <summary>Fallback prefix used when no prefix attribute is present (backwards compatibility).</summary>
    private const string FallbackPrefix = "MB#";

    public override string Name => "offerlink";

    protected override string GetLabel(MarkupNode node, long id)
    {
        var prefix = FallbackPrefix;
        if (node.Attributes.TryGetValue("prefix", out var prefixValue)
            && prefixValue.TryGetString(out var prefixStr))
        {
            prefix = prefixStr;
        }

        return $"[{prefix}{id}]";
    }

    protected override void OnClick(Control source, long id)
    {
        if (source.TryGetParentHandler<IOfferLinkClickHandler>(out var handler))
            handler.HandleOfferLinkClick((uint) id);
    }
}

/// <summary>
/// Interface for controls that handle offer link clicks from <see cref="OfferLinkTag"/>.
/// </summary>
public interface IOfferLinkClickHandler
{
    void HandleOfferLinkClick(uint offerId);
}
