using Content.Client.UserInterface.Fragments;
using Content.Shared._Stalker_EN.BulletinBoard;
using Content.Shared.CartridgeLoader;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Stalker_EN.BulletinBoard;

/// <summary>
/// UIFragment implementation for a generic bulletin board cartridge.
/// Manages two pages: main (with Primary/Secondary tabs) and post form.
/// </summary>
public sealed partial class STBulletinBoardUi : UIFragment
{
    private BoxContainer? _root;
    private STBulletinMainPage? _mainPage;
    private STBulletinPostPage? _postPage;

    public override Control GetUIFragmentRoot()
    {
        return _root!;
    }

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        _root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        _mainPage = new STBulletinMainPage();
        _postPage = new STBulletinPostPage();

        if (fragmentOwner.HasValue)
        {
            var entMan = IoCManager.Resolve<IEntityManager>();
            if (entMan.TryGetComponent<CartridgeComponent>(fragmentOwner.Value, out var cartridge)
                && cartridge.Icon is not null)
            {
                _mainPage.HeaderIcon.SetFromSpriteSpecifier(cartridge.Icon);
            }
        }

        _root.AddChild(_mainPage);
        _root.AddChild(_postPage);

        _postPage.Visible = false;

        _mainPage.OnPostPressed += category =>
        {
            if (_mainPage.IsAtPostLimit(category))
                return;

            _mainPage.Visible = false;
            _postPage.Visible = true;
            _postPage.SetCategory(category, _mainPage.LastConfig);
        };

        _mainPage.OnMuteToggled += () =>
        {
            userInterface.SendPredictedMessage(new CartridgeUiMessage(
                new STBulletinToggleMuteEvent()));
        };

        _mainPage.OnWithdrawPressed += offerId =>
        {
            userInterface.SendPredictedMessage(new CartridgeUiMessage(
                new STBulletinWithdrawOfferEvent(offerId)));
        };

        _mainPage.OnContactPressed += (posterMessengerId, offerId) =>
        {
            userInterface.SendPredictedMessage(new CartridgeUiMessage(
                new STBulletinContactPosterEvent(posterMessengerId, offerId)));
        };

        _postPage.OnBack += () =>
        {
            _postPage.Visible = false;
            _mainPage.Visible = true;
        };

        _postPage.OnSubmit += (category, description) =>
        {
            userInterface.SendPredictedMessage(new CartridgeUiMessage(
                new STBulletinPostOfferEvent(category, description)));

            _postPage.Visible = false;
            _mainPage.Visible = true;
        };
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not STBulletinUiState boardState)
            return;

        if (boardState.SearchQuery is not null)
            _mainPage?.SetSearchQuery(boardState.SearchQuery);

        _mainPage?.UpdateState(boardState);
    }
}
