using System.Diagnostics.CodeAnalysis;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Input;
using Robust.Shared.Utility;

namespace Content.Client._Stalker_EN.UI.Controls;

/// <summary>
/// Abstract base for PDA cartridge link markup tags.
/// Handles parsing, blue label rendering, hover effects, and click delegation.
/// </summary>
public abstract class PdaLinkTag : IMarkupTagHandler
{
    public abstract string Name { get; }

    /// <summary>Returns the display text for this link.</summary>
    protected abstract string GetLabel(MarkupNode node, long id);

    /// <summary>Called when the link label is clicked.</summary>
    protected abstract void OnClick(Control source, long id);

    public bool TryCreateControl(MarkupNode node, [NotNullWhen(true)] out Control? control)
    {
        if (!node.Value.TryGetLong(out var longId))
        {
            control = null;
            return false;
        }

        var id = longId.Value;

        var label = new Label
        {
            Text = GetLabel(node, id),
            MouseFilter = Control.MouseFilterMode.Stop,
            FontColorOverride = Color.CornflowerBlue,
            DefaultCursorShape = Control.CursorShape.Hand,
        };

        label.OnMouseEntered += _ => label.FontColorOverride = Color.LightSkyBlue;
        label.OnMouseExited += _ => label.FontColorOverride = Color.CornflowerBlue;
        label.OnKeyBindDown += args =>
        {
            if (args.Function != EngineKeyFunctions.UIClick)
                return;

            OnClick(label, id);
        };

        control = label;
        return true;
    }
}
