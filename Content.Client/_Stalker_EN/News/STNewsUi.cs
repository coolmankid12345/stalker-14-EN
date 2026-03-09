using Content.Client.UserInterface.Fragments;
using Content.Shared._Stalker_EN.News;
using Content.Shared.CartridgeLoader;
using Robust.Client.UserInterface;

namespace Content.Client._Stalker_EN.News;

/// <summary>
/// UIFragment adapter for the Stalker News PDA cartridge program.
/// </summary>
public sealed partial class STNewsUi : UIFragment
{
    private STNewsUiFragment? _fragment;

    public override Control GetUIFragmentRoot()
    {
        return _fragment!;
    }

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        _fragment = new STNewsUiFragment();
        _fragment.OnPublishArticle += (title, content, color) =>
        {
            SendMessage(new STNewsPublishEvent(title, content, color), userInterface);
        };
        _fragment.OnRequestArticle += articleId =>
        {
            SendMessage(new STNewsRequestArticleEvent(articleId), userInterface);
        };
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not STNewsUiState newsState)
            return;

        _fragment?.UpdateState(newsState);
    }

    private static void SendMessage(CartridgeMessageEvent msg, BoundUserInterface ui)
    {
        var message = new CartridgeUiMessage(msg);
        ui.SendMessage(message);
    }
}
