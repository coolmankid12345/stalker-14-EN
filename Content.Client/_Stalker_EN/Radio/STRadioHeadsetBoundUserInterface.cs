using Content.Shared._Stalker_EN.Radio;
using JetBrains.Annotations;

namespace Content.Client._Stalker_EN.Radio;

/// <summary>
/// Bound user interface for the stalker radio headset.
/// Opens the STRadioHeadsetMenu and sends mic toggle and frequency selection messages.
/// </summary>
[UsedImplicitly]
public sealed class STRadioHeadsetBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private STRadioHeadsetMenu? _menu;

    public STRadioHeadsetBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _menu = new STRadioHeadsetMenu();

        _menu.OnMicPressed += enabled =>
        {
            SendMessage(new STRadioHeadsetToggleMicMessage(enabled));
        };

        _menu.OnFrequencyEntered += frequency =>
        {
            SendMessage(new STRadioHeadsetSelectFrequencyMessage(frequency));
        };

        _menu.OnClose += Close;
        _menu.OpenCentered();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;
        _menu?.Close();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not STRadioHeadsetBoundUIState msg)
            return;

        _menu?.Update(msg);
    }
}
