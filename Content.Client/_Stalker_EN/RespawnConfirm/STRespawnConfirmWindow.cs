using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Localization;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client._Stalker_EN.RespawnConfirm;

public sealed class STRespawnConfirmWindow : DefaultWindow
{
    public readonly Button DenyButton;
    public readonly Button AcceptButton;

    public STRespawnConfirmWindow()
    {
        Title = Loc.GetString("st-respawn-confirm-title");

        Contents.AddChild(new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Children =
            {
                new Label { Text = Loc.GetString("st-respawn-confirm-text") },
                new BoxContainer
                {
                    Orientation = LayoutOrientation.Horizontal,
                    Align = AlignMode.Center,
                    Children =
                    {
                        (AcceptButton = new Button { Text = Loc.GetString("st-respawn-confirm-yes") }),
                        new Control { MinSize = new Vector2(20, 0) },
                        (DenyButton = new Button { Text = Loc.GetString("st-respawn-confirm-no") })
                    }
                }
            }
        });
    }
}
