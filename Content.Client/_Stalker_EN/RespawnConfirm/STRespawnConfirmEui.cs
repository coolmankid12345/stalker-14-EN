using Content.Client.Eui;
using Content.Shared._Stalker_EN.RespawnConfirm;
using JetBrains.Annotations;
using Robust.Client.Graphics;

namespace Content.Client._Stalker_EN.RespawnConfirm;

[UsedImplicitly]
public sealed class STRespawnConfirmEui : BaseEui
{
    private readonly STRespawnConfirmWindow _window;

    public STRespawnConfirmEui()
    {
        _window = new STRespawnConfirmWindow();

        _window.DenyButton.OnPressed += _ =>
        {
            SendMessage(new STRespawnConfirmMessage(STRespawnConfirmButton.Deny));
            _window.Close();
        };

        _window.AcceptButton.OnPressed += _ =>
        {
            SendMessage(new STRespawnConfirmMessage(STRespawnConfirmButton.Accept));
            _window.Close();
        };
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
