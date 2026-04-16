using Content.Client.UserInterface.Fragments;
using Content.Shared._Stalker_EN.PdaMessenger;
using Content.Shared.CartridgeLoader;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Stalker_EN.PdaMessenger;

/// <summary>
/// UIFragment implementation for the stalker messenger cartridge.
/// Manages page navigation between main page, channel/DM view, and compose page.
/// </summary>
public sealed partial class STMessengerUi : UIFragment
{
    private BoxContainer? _root;
    private STMessengerMainPage? _mainPage;
    private STMessengerChannelPage? _channelPage;
    private STMessengerComposePage? _composePage;
    private BoundUserInterface? _userInterface;

    private string? _currentChatId;
    private uint? _replyToId;
    private string? _replySnippet;

    /// <summary>
    /// Maps DM chat ID → display name (character name) for the compose page.
    /// </summary>
    private readonly Dictionary<string, string> _chatDisplayNames = new();

    public override Control GetUIFragmentRoot()
    {
        return _root!;
    }

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        _userInterface = userInterface;

        _root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        _mainPage = new STMessengerMainPage();
        _channelPage = new STMessengerChannelPage();
        _composePage = new STMessengerComposePage();

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
        _root.AddChild(_channelPage);
        _root.AddChild(_composePage);

        _channelPage.Visible = false;
        _composePage.Visible = false;

        _mainPage.OnChannelSelected += chatId => NavigateToChat(chatId);
        _mainPage.OnContactSelected += chatId => NavigateToChat(chatId);

        _mainPage.OnAddContact += messengerId =>
        {
            userInterface.SendPredictedMessage(new CartridgeUiMessage(new STMessengerAddContactEvent(messengerId)));
        };

        _mainPage.OnRemoveContact += contactMessengerId =>
        {
            userInterface.SendPredictedMessage(new CartridgeUiMessage(new STMessengerRemoveContactEvent(contactMessengerId)));
        };

        _mainPage.OnToggleMute += channelId =>
        {
            userInterface.SendPredictedMessage(new CartridgeUiMessage(new STMessengerToggleMuteEvent(channelId)));
        };

        _channelPage.OnBack += () => NavigateToMain();

        _channelPage.OnCompose += chatId =>
        {
            _replyToId = null;
            _replySnippet = null;
            ShowCompose(chatId);
        };

        _channelPage.OnReply += (chatId, messageId, snippet) =>
        {
            _replyToId = messageId;
            _replySnippet = snippet;
            ShowCompose(chatId);
        };

        _channelPage.OnOfferLinkClicked += offerId =>
        {
            userInterface.SendPredictedMessage(new CartridgeUiMessage(
                new STMessengerNavigateToOfferEvent(offerId)));
        };

        _channelPage.OnNewsLinkClicked += articleId =>
        {
            userInterface.SendPredictedMessage(new CartridgeUiMessage(
                new STMessengerNavigateToNewsEvent(articleId)));
        };

        _composePage.OnBack += () =>
        {
            _composePage.Visible = false;
            _channelPage!.Visible = true;
        };

        _composePage.OnDismissReply += () =>
        {
            _replyToId = null;
            _replySnippet = null;
        };

        _composePage.OnSend += (chatId, content, isAnonymous) =>
        {
            userInterface.SendPredictedMessage(new CartridgeUiMessage(
                new STMessengerSendEvent(chatId, content, _replyToId, isAnonymous)));

            _replyToId = null;
            _replySnippet = null;
            _composePage.Visible = false;
            _channelPage!.Visible = true;
        };
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not STMessengerUiState messengerState)
            return;

        // Deep-link from external systems (e.g. merc board Contact button)
        if (messengerState.NavigateToChatId is not null)
        {
            _currentChatId = messengerState.NavigateToChatId;

            if (messengerState.DraftMessage is not null)
            {
                // Navigate directly to compose with draft pre-filled
                _mainPage!.Visible = false;
                _channelPage!.Visible = false;
                _replyToId = null;
                _replySnippet = null;
                ShowCompose(messengerState.NavigateToChatId, messengerState.DraftMessage);
            }
            else
            {
                _mainPage!.Visible = false;
                _composePage!.Visible = false;
                _channelPage!.Visible = true;
                _channelPage.SetChatId(messengerState.NavigateToChatId);
            }
            // Fall through — server already set viewed chat and included messages in this state
        }

        _chatDisplayNames.Clear();
        foreach (var dm in messengerState.DirectMessages)
        {
            _chatDisplayNames[dm.Id] = dm.DisplayName;
        }

        _mainPage?.UpdateState(messengerState);

        if (_currentChatId is not null && _channelPage is { Visible: true })
        {
            var chat = FindChat(messengerState, _currentChatId);
            if (chat is not null)
                _channelPage.UpdateState(chat);
        }
    }

    private void NavigateToChat(string chatId)
    {
        _currentChatId = chatId;

        // Tell server which chat we're viewing (for lazy message loading)
        _userInterface?.SendPredictedMessage(new CartridgeUiMessage(new STMessengerViewChatEvent(chatId)));

        _mainPage!.Visible = false;
        _composePage!.Visible = false;
        _channelPage!.Visible = true;
        _channelPage.SetChatId(chatId);
    }

    private void NavigateToMain()
    {
        _currentChatId = null;
        _channelPage!.Visible = false;
        _composePage!.Visible = false;
        _mainPage!.Visible = true;

        _userInterface?.SendPredictedMessage(new CartridgeUiMessage(new STMessengerViewChatEvent(null)));
    }

    private void ShowCompose(string chatId, string? initialContent = null)
    {
        _channelPage!.Visible = false;
        _composePage!.Visible = true;

        // For DM chats, pass the display name (character name) to the compose page
        _chatDisplayNames.TryGetValue(chatId, out var displayName);
        _composePage.Setup(chatId, _replyToId, _replySnippet, displayName, initialContent);
    }

    private static STMessengerChat? FindChat(STMessengerUiState state, string chatId)
    {
        foreach (var ch in state.Channels)
        {
            if (ch.Id == chatId)
                return ch;
        }

        foreach (var dm in state.DirectMessages)
        {
            if (dm.Id == chatId)
                return dm;
        }

        return null;
    }
}
