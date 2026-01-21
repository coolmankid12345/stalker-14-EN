using Content.Shared._Stalker_EN.Devices.Radar;
using JetBrains.Annotations;

namespace Content.Client._Stalker_EN.Devices.Radar.UI;

[UsedImplicitly]
public sealed class RadarDisplayBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private RadarWindow? _window;

    public RadarDisplayBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = new RadarWindow();
        _window.OnClose += Close;
        _window.OnAnomalyDetectorToggle += () => SendMessage(new RadarToggleAnomalyDetectorMessage());
        _window.OnRadarToggle += () => SendMessage(new RadarToggleArtifactScannerMessage());
        _window.OpenCentered();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;
        _window?.Close();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not RadarDisplayBoundUIState radarState)
            return;

        _window?.UpdateState(radarState);
    }
}
