using System;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Maths;

namespace Content.Client._Stalker.PersistentCrafting.UI.Controls;


public sealed class PersistentCraftRecipeIconProgressOverlay : Control
{
    private float? _progressRatio;
    private Color _progressColor = PersistentCraftUiTheme.Selection;
    private float _flashAlpha;
    private Color _flashColor = Color.Transparent;

    public PersistentCraftRecipeIconProgressOverlay()
    {
        MouseFilter = MouseFilterMode.Ignore;
    }

    public void SetProgress(float? progressRatio, Color progressColor)
    {
        _progressRatio = progressRatio.HasValue
            ? Math.Clamp(progressRatio.Value, 0f, 1f)
            : null;
        _progressColor = progressColor;
    }

    public void SetFlash(Color flashColor, float flashAlpha)
    {
        _flashColor = flashColor;
        _flashAlpha = Math.Clamp(flashAlpha, 0f, 1f);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        if (_progressRatio is { } ratio && ratio > 0f)
        {
            var height = PixelSizeBox.Height * ratio;
            var box = new UIBox2(
                PixelSizeBox.Left,
                PixelSizeBox.Bottom - height,
                PixelSizeBox.Right,
                PixelSizeBox.Bottom);
            handle.DrawRect(box, _progressColor.WithAlpha(0.38f));
        }

        if (_flashAlpha <= 0f)
            return;

        handle.DrawRect(PixelSizeBox, _flashColor.WithAlpha(_flashAlpha));
    }
}
