using System.Numerics;
using Content.Shared._Stalker_EN.Devices.Radar;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Stalker_EN.Devices.Radar.UI;

[UsedImplicitly]
public sealed class RadarDisplayBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private RadarWindow? _window;

    // Static field to persist window position across opens
    private static Vector2? _lastWindowPosition;

    public RadarDisplayBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = new RadarWindow();
        _window.OnClose += Close;
        _window.OnAnomalyDetectorToggle += () => SendMessage(new RadarToggleAnomalyDetectorMessage());

        // Don't open window here - wait for first state update to prevent flicker
        // Window will be opened in UpdateState() after receiving initial state
    }

    private bool IsPositionOnScreen(Vector2 position)
    {
        var clyde = IoCManager.Resolve<IClyde>();
        var screenSize = clyde.ScreenSize;

        // Check if position is reasonably on screen (at least partially visible)
        return position.X >= -100 && position.X < screenSize.X - 50 &&
               position.Y >= -50 && position.Y < screenSize.Y - 50;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        // Save window position before closing
        if (_window != null)
        {
            _lastWindowPosition = _window.Position;
        }

        _window?.Close();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not RadarDisplayBoundUIState radarState)
            return;

        _window?.UpdateState(radarState);

        // Show window after first state update (prevents flicker from incomplete UI)
        if (_window != null && !_window.IsOpen)
        {
            if (_lastWindowPosition.HasValue && IsPositionOnScreen(_lastWindowPosition.Value))
            {
                _window.Open();
                LayoutContainer.SetPosition(_window, _lastWindowPosition.Value);
            }
            else
            {
                _window.OpenCentered();
            }
        }
    }
}
