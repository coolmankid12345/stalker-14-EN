using Content.Shared._Stalker.RadioStalker;
using JetBrains.Annotations;

namespace Content.Client._Stalker_EN.Radio;

/// <summary>
/// Bound user interface for the stalker handheld radio.
/// Opens the STRadioMenu (split frequency input) and sends existing message types
/// to the server-side handlers in RadioDeviceSystem.
/// </summary>
[UsedImplicitly]
public sealed class STRadioBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private STRadioMenu? _menu;

    public STRadioBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _menu = new STRadioMenu();

        _menu.OnMicPressed += enabled =>
        {
            SendMessage(new ToggleRadioMicMessage(enabled));
        };

        _menu.OnSpeakerPressed += enabled =>
        {
            SendMessage(new ToggleRadioSpeakerMessage(enabled));
        };

        _menu.OnFrequencyEntered += frequency =>
        {
            SendMessage(new SelectRadioChannelMessage(frequency));
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

        if (state is not RadioStalkerBoundUIState msg)
            return;

        _menu?.Update(msg);
    }
}
