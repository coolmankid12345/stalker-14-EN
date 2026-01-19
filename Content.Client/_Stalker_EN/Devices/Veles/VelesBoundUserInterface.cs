using Content.Shared._Stalker_EN.Devices.Veles;
using JetBrains.Annotations;

namespace Content.Client._Stalker_EN.Devices.Veles;

[UsedImplicitly]
public sealed class VelesBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private VelesWindow? _window;

    public VelesBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = new VelesWindow();
        _window.OnClose += Close;
        _window.OnAnomalyDetectorToggle += () => SendMessage(new VelesToggleAnomalyDetectorMessage());
        _window.OnArtifactScannerToggle += () => SendMessage(new VelesToggleArtifactScannerMessage());
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

        if (state is not VelesBoundUIState velesState)
            return;

        _window?.UpdateState(velesState);
    }
}
