using Content.Client.Eui;
using Content.Shared._Stalker_EN.RespawnConfirm;
using JetBrains.Annotations;
using Robust.Client.Graphics;

namespace Content.Client._Stalker_EN.RespawnConfirm;

[UsedImplicitly]
public sealed class STRespawnConfirmEui : BaseEui
{
    private readonly STRespawnConfirmWindow _window;
    private bool _messageSent;

    public STRespawnConfirmEui()
    {
        _window = new STRespawnConfirmWindow();
        _window.OnClose += OnWindowClosed;

        _window.DenyButton.OnPressed += _ =>
        {
            _messageSent = true;
            SendMessage(new STRespawnConfirmMessage(STRespawnConfirmButton.Deny));
            _window.Close();
        };

        _window.AcceptButton.OnPressed += _ =>
        {
            _messageSent = true;
            SendMessage(new STRespawnConfirmMessage(STRespawnConfirmButton.Accept));
            _window.Close();
        };
    }

    private void OnWindowClosed()
    {
        if (!_messageSent)
            SendMessage(new STRespawnConfirmMessage(STRespawnConfirmButton.Deny));
    }

    public override void Opened()
    {
        IoCManager.Resolve<IClyde>().RequestWindowAttention();
        _window.OpenCentered();
    }

    public override void Closed()
    {
        _window.Close();
    }
}
