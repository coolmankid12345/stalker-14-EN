using Content.Shared._Stalker_EN.Devices.ArtifactRadar;
using JetBrains.Annotations;

namespace Content.Client._Stalker_EN.Devices.ArtifactRadar;

[UsedImplicitly]
public sealed class ArtifactRadarBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private ArtifactRadarWindow? _window;

    public ArtifactRadarBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = new ArtifactRadarWindow();
        _window.OnClose += Close;
        _window.OnAnomalyDetectorToggle += () => SendMessage(new ArtifactRadarToggleAnomalyDetectorMessage());
        _window.OnArtifactScannerToggle += () => SendMessage(new ArtifactRadarToggleArtifactScannerMessage());
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

        if (state is not ArtifactRadarBoundUIState radarState)
            return;

        _window?.UpdateState(radarState);
    }
}
