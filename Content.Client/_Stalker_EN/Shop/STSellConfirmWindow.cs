using System.Numerics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client._Stalker_EN.Shop;

/// <summary>
/// Confirmation popup shown before selling an item in the shop.
/// </summary>
public sealed class STSellConfirmWindow : DefaultWindow
{
    public readonly Button ConfirmButton;
    public readonly Button CancelButton;
    public readonly CheckBox SuppressCheckBox;

    public event Action? OnConfirmed;
    public event Action? OnCancelled;

    /// <summary>
    /// Whether the "don't ask again" checkbox is checked.
    /// </summary>
    public bool DontAskAgain => SuppressCheckBox.Pressed;

    public STSellConfirmWindow(string itemName, string price, string currency)
    {
        Title = Loc.GetString("st-shop-sell-confirm-title");

        Contents.AddChild(new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Children =
            {
                new Label
                {
                    Text = Loc.GetString("st-shop-sell-confirm-text",
                        ("item", itemName), ("price", price), ("currency", currency)),
                },
                (SuppressCheckBox = new CheckBox
                {
                    Text = Loc.GetString("st-shop-sell-confirm-suppress"),
                }),
                new BoxContainer
                {
                    Orientation = LayoutOrientation.Horizontal,
                    Align = AlignMode.Center,
                    Children =
                    {
                        (ConfirmButton = new Button
                        {
                            Text = Loc.GetString("st-shop-sell-confirm-yes"),
                        }),
                        new Control { MinSize = new Vector2(20, 0) },
                        (CancelButton = new Button
                        {
                            Text = Loc.GetString("st-shop-sell-confirm-no"),
                        }),
                    },
                },
            },
        });

        ConfirmButton.OnPressed += _ =>
        {
            OnConfirmed?.Invoke();
            Close();
        };

        CancelButton.OnPressed += _ =>
        {
            OnCancelled?.Invoke();
            Close();
        };
    }
}
