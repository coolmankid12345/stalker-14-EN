using Content.Client.UserInterface.ControlExtensions;
using Content.Client._Stalker_EN.UI.Controls;
using Robust.Client.UserInterface;
using Robust.Shared.Utility;

namespace Content.Client._Stalker_EN.News;

/// <summary>
/// Markup tag handler for clickable news article references.
/// Renders <c>[newslink=42][/newslink]</c> as a blue clickable "[NEWS#42]" label.
/// Click handling is delegated to the nearest parent implementing <see cref="INewsLinkClickHandler"/>.
/// </summary>
public sealed class NewsLinkTag : PdaLinkTag
{
    public override string Name => "newslink";

    protected override string GetLabel(MarkupNode node, long id)
        => Loc.GetString("st-news-link-label", ("id", (int) id));

    protected override void OnClick(Control source, long id)
    {
        if (source.TryGetParentHandler<INewsLinkClickHandler>(out var handler))
            handler.HandleNewsLinkClick((int) id);
    }
}

/// <summary>
/// Interface for controls that handle news link clicks from <see cref="NewsLinkTag"/>.
/// </summary>
public interface INewsLinkClickHandler
{
    void HandleNewsLinkClick(int articleId);
}
