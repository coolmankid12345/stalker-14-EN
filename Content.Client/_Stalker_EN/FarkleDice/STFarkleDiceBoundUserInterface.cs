using Content.Shared._Stalker_EN.FarkleDice;
using JetBrains.Annotations;

namespace Content.Client._Stalker_EN.FarkleDice;

[UsedImplicitly]
public sealed class STFarkleDiceBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private STFarkleDiceWindow? _window;

    public STFarkleDiceBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = new STFarkleDiceWindow();
        _window.OpenCentered();

        _window.OnClose += Close;

        _window.OnJoinPressed += () => SendMessage(new STFarkleJoinMessage());
        _window.OnLeavePressed += () => SendMessage(new STFarkleLeaveMessage());
        _window.OnRollPressed += () => SendMessage(new STFarkleRollMessage());
        _window.OnDieSelected += index => SendMessage(new STFarkleSelectDieMessage(index));
        _window.OnKeepAndContinuePressed += () => SendMessage(new STFarkleKeepAndContinueMessage());
        _window.OnBankPressed += () => SendMessage(new STFarkleBankMessage());
        _window.OnNewGamePressed += () => SendMessage(new STFarkleNewGameMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is STFarkleDiceBoundUiState farkleState)
            _window?.UpdateState(farkleState);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _window?.Close();
            _window?.Dispose();
        }
    }
}
