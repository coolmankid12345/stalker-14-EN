using System;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._Stalker.PersistentCrafting.UI.Coordinators;

public sealed class PersistentCraftNodeDetailsWindowCoordinator
{
    private readonly IClyde _clyde;
    private readonly float _windowWidth;
    private readonly float _windowHeight;
    private readonly float _windowMinWidth;
    private readonly float _windowMinHeight;
    private readonly float _windowMargin;
    private DefaultWindow? _window;

    public bool IsOpen => _window is { Disposed: false, IsOpen: true };

    public PersistentCraftNodeDetailsWindowCoordinator(
        IClyde clyde,
        float windowWidth,
        float windowHeight,
        float windowMinWidth,
        float windowMinHeight,
        float windowMargin)
    {
        _clyde = clyde;
        _windowWidth = windowWidth;
        _windowHeight = windowHeight;
        _windowMinWidth = windowMinWidth;
        _windowMinHeight = windowMinHeight;
        _windowMargin = windowMargin;
    }

    public void Show(string title, Control content)
    {
        EnsureWindow();
        var window = _window!;
        window.Title = title;
        window.Contents.RemoveAllChildren();

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(6),
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        root.AddChild(content);

        var scroll = new ScrollContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            HScrollEnabled = false,
            VScrollEnabled = true,
        };
        scroll.AddChild(root);
        window.Contents.AddChild(scroll);

        ApplyWindowSize(window);

        if (!window.IsOpen)
            window.OpenCentered();
        else
            window.MoveToFront();
    }

    public void Close()
    {
        if (_window == null || _window.Disposed)
            return;

        _window.Close();
    }

    private void EnsureWindow()
    {
        if (_window != null && !_window.Disposed)
            return;

        _window = new DefaultWindow
        {
            MinSize = new Vector2(_windowMinWidth, _windowMinHeight),
            SetSize = BuildWindowSize(),
            Resizable = true,
        };
    }

    private void ApplyWindowSize(DefaultWindow window)
    {
        window.SetSize = BuildWindowSize();
    }

    private Vector2 BuildWindowSize()
    {
        var screen = _clyde.ScreenSize;
        var maxWidth = MathF.Max(_windowMinWidth, screen.X - (_windowMargin * 2f));
        var maxHeight = MathF.Max(_windowMinHeight, screen.Y - (_windowMargin * 2f));
        var width = MathF.Min(MathF.Max(_windowWidth, _windowMinWidth), maxWidth);
        var height = MathF.Min(MathF.Max(_windowHeight, _windowMinHeight), maxHeight);
        return new Vector2(width, height);
    }
}
