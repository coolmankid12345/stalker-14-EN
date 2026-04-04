using Content.Client.UserInterface.Fragments;
using Content.Shared._Stalker.PdaMessenger;
using Content.Shared.CartridgeLoader;
using Robust.Client.UserInterface;
using Robust.Shared.Log;

namespace Content.Client._Stalker.PdaMessenger;

public sealed partial class MessengerUi : UIFragment
{
    private static readonly ISawmill _sawmill = Logger.GetSawmill("pda-notify-client");
    
    private MessengerUiFragment? _fragment;

    public override Control GetUIFragmentRoot()
    {
        return _fragment!;
    }

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        _sawmill.Info($"[PDA Client] MessengerUi.Setup() called, fragmentOwner={fragmentOwner}");
        
        _fragment = new MessengerUiFragment();
        _fragment.OnSendMessage += message =>
        {
            var msg = new CartridgeUiMessage(new MessengerUiMessageEvent(message));
            _sawmill.Info($"[PDA Client] Sending message: Title={message.Title}, Receiver={message.Receiver}");
            userInterface.SendMessage(msg);
            _sawmill.Info($"[PDA Client] Message sent via SendMessage()");
        };

        _fragment.OnLogin += owner =>
        {
            userInterface.SendMessage(new CartridgeUiMessage(new MessengerUiSetLoginEvent(owner)));
        };
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not MessengerUiState messengerState)
            return;

        _fragment?.UpdateState(messengerState.Chats);
    }
}
