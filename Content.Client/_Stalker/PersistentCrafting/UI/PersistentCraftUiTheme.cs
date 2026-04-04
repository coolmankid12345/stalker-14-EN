using System;
using System.Linq;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.Client._Stalker.PersistentCrafting.UI;

internal static class PersistentCraftUiTheme
{
    public static readonly Color SurfaceWindow = Color.FromHex("#141a22");
    public static readonly Color SurfacePanel = Color.FromHex("#1b212b");
    public static readonly Color SurfacePanelAlt = Color.FromHex("#202734");
    public static readonly Color SurfacePanelSoft = Color.FromHex("#171d26");
    public static readonly Color SurfaceInset = Color.FromHex("#11161d");
    public static readonly Color BorderSoft = Color.FromHex("#2c3643");
    public static readonly Color Border = Color.FromHex("#3b4756");
    public static readonly Color BorderStrong = Color.FromHex("#4d6073");
    public static readonly Color TextPrimary = Color.FromHex("#f0f3f7");
    public static readonly Color TextSecondary = Color.FromHex("#bac2ce");
    public static readonly Color TextMuted = Color.FromHex("#8f98a6");
    public static readonly Color WeaponAccent = Color.FromHex("#b98558");
    public static readonly Color ArmorAccent = Color.FromHex("#6a928d");
    public static readonly Color AnomalyAccent = Color.FromHex("#83955f");
    public static readonly Color Success = Color.FromHex("#8daa77");
    public static readonly Color Danger = Color.FromHex("#c9776f");
    public static readonly Color Selection = Color.FromHex("#d7c08f");

    public static StyleBoxFlat Panel(
        Color background,
        Color border,
        int thickness = 1,
        int left = 12,
        int right = 12,
        int top = 10,
        int bottom = 10)
    {
        return new StyleBoxFlat
        {
            BackgroundColor = background,
            BorderColor = border,
            BorderThickness = new Thickness(thickness),
            ContentMarginLeftOverride = left,
            ContentMarginRightOverride = right,
            ContentMarginTopOverride = top,
            ContentMarginBottomOverride = bottom,
        };
    }

    public static StyleBoxFlat ProgressBackground()
    {
        return new StyleBoxFlat
        {
            BackgroundColor = SurfaceInset,
        };
    }

    public static StyleBoxFlat ProgressForeground(Color accent)
    {
        return new StyleBoxFlat
        {
            BackgroundColor = accent.WithAlpha(0.9f),
        };
    }

    public static void ApplyTabTheme(
        TabContainer tabs,
        string styleIdentifier,
        Color accent,
        IUserInterfaceManager uiManager)
    {
        var active = new StyleBoxFlat
        {
            BackgroundColor = SurfacePanelAlt,
            BorderColor = accent,
            BorderThickness = new Thickness(1),
        };
        active.SetContentMarginOverride(StyleBox.Margin.Horizontal, 16);
        active.SetContentMarginOverride(StyleBox.Margin.Vertical, 8);

        var inactive = new StyleBoxFlat
        {
            BackgroundColor = SurfacePanelSoft,
            BorderColor = BorderSoft,
            BorderThickness = new Thickness(1),
        };
        inactive.SetContentMarginOverride(StyleBox.Margin.Horizontal, 16);
        inactive.SetContentMarginOverride(StyleBox.Margin.Vertical, 8);

        tabs.PanelStyleBoxOverride = Panel(SurfacePanelSoft, BorderSoft, 1, 12, 12, 12, 12);

        var baseRules = uiManager.Stylesheet?.Rules ?? Array.Empty<StyleRule>();
        tabs.StyleIdentifier = styleIdentifier;
        tabs.Stylesheet = new Stylesheet(
            baseRules
                .Concat(
                    new[]
                    {
                        new StyleRule(
                            new SelectorElement(typeof(TabContainer), null, styleIdentifier, null),
                            new[]
                            {
                                new StyleProperty(TabContainer.StylePropertyTabStyleBox, active),
                                new StyleProperty(TabContainer.StylePropertyTabStyleBoxInactive, inactive),
                                new StyleProperty(TabContainer.stylePropertyTabFontColor, TextPrimary),
                                new StyleProperty(TabContainer.StylePropertyTabFontColorInactive, TextMuted),
                            })
                    })
                .ToArray());
        tabs.ForceRunStyleUpdate();
    }
}
