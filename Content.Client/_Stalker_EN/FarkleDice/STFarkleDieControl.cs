using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Graphics;
using Robust.Shared.Input;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client._Stalker_EN.FarkleDice;

/// <summary>
/// Visual states for a Farkle die, controlling the border color indicator.
/// </summary>
public enum DieVisualState : byte
{
    Normal,
    Selected,
    Kept,
}

/// <summary>
/// Custom UI control that renders a single d6 die using sprites from dice.rsi,
/// with a colored border to indicate selection/kept state, a number overlay, and a rolling animation.
/// </summary>
public sealed class STFarkleDieControl : Control
{
    private const string DiceRsiPath = "/Textures/Objects/Fun/dice.rsi";
    private const int DiceFaces = 6;

    /// <summary>
    /// Total duration of the roll animation in seconds.
    /// </summary>
    private const float AnimationDuration = 0.7f;

    /// <summary>
    /// Fastest interval between face changes at animation start.
    /// </summary>
    private const float InitialInterval = 0.04f;

    /// <summary>
    /// Slowest interval between face changes at animation end.
    /// </summary>
    private const float FinalInterval = 0.15f;

    private static readonly Color NormalBorderColor = Color.FromHex("#4A4A3A");
    private static readonly Color SelectedBorderColor = Color.FromHex("#D4912A");
    private static readonly Color KeptBorderColor = Color.FromHex("#6B6B55");
    private static readonly Color UnknownModulate = new(0.3f, 0.3f, 0.25f);
    private static readonly Color NormalModulate = Color.White;
    private static readonly Color KeptModulate = new(0.55f, 0.52f, 0.45f);

    private readonly IRobustRandom _random;
    private readonly Texture[] _faceTextures = new Texture[DiceFaces];
    private readonly PanelContainer _panel;
    private readonly TextureRect _textureRect;
    private readonly Label _numberLabel;

    // Pre-allocated style boxes to avoid per-frame allocation
    private readonly StyleBoxFlat _normalStyle;
    private readonly StyleBoxFlat _selectedStyle;
    private readonly StyleBoxFlat _keptStyle;

    private int _currentValue;
    private bool _disabled;
    private bool _isAnimating;
    private float _animationElapsed;
    private float _nextFaceChangeAt;
    private int _finalValue;

    /// <summary>
    /// Fired when the die is clicked and not disabled or animating.
    /// </summary>
    public event Action? OnPressed;

    /// <summary>
    /// Whether this die blocks click interaction.
    /// </summary>
    public bool Disabled
    {
        get => _disabled;
        set => _disabled = value;
    }

    public STFarkleDieControl()
    {
        _random = IoCManager.Resolve<IRobustRandom>();
        var resCache = IoCManager.Resolve<IResourceCache>();

        var rsi = resCache.GetResource<RSIResource>(DiceRsiPath).RSI;
        for (var i = 0; i < DiceFaces; i++)
        {
            if (rsi.TryGetState($"d6_{i + 1}", out var state))
                _faceTextures[i] = state.Frame0;
        }

        var borderThickness = new Thickness(3);
        _normalStyle = new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex("#1A1A17"),
            BorderColor = NormalBorderColor,
            BorderThickness = borderThickness,
        };
        _selectedStyle = new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex("#2A2210"),
            BorderColor = SelectedBorderColor,
            BorderThickness = borderThickness,
        };
        _keptStyle = new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex("#1A1A1A"),
            BorderColor = KeptBorderColor,
            BorderThickness = borderThickness,
        };

        _panel = new PanelContainer
        {
            PanelOverride = _normalStyle,
        };

        _textureRect = new TextureRect
        {
            TextureScale = new Vector2(3, 3),
            Stretch = TextureRect.StretchMode.KeepCentered,
        };

        _numberLabel = new Label
        {
            FontColorOverride = Color.White,
            FontColorShadowOverride = Color.Black,
            ShadowOffsetXOverride = 1,
            ShadowOffsetYOverride = 1,
            HorizontalAlignment = HAlignment.Right,
            VerticalAlignment = VAlignment.Bottom,
            Margin = new Thickness(0, 0, 4, 2),
        };

        _panel.AddChild(_textureRect);
        _panel.AddChild(_numberLabel);
        AddChild(_panel);

        // 96px die (32 * 3 scale) + 6px border + 2px breathing room = 104px
        MinSize = new Vector2(104, 104);

        MouseFilter = MouseFilterMode.Stop;
        OnKeyBindDown += OnKeyPressed;
    }

    /// <summary>
    /// Sets the die to show a specific face value, optionally playing a roll animation.
    /// </summary>
    /// <param name="value">Die face value (1-6).</param>
    /// <param name="animate">Whether to play the rolling animation before settling.</param>
    public void SetValue(int value, bool animate)
    {
        _finalValue = value;

        if (_isAnimating)
        {
            _isAnimating = false;
        }

        if (animate && value >= 1 && value <= DiceFaces)
        {
            _isAnimating = true;
            _animationElapsed = 0f;
            _nextFaceChangeAt = 0f;
            SetFaceTexture(_random.Next(1, DiceFaces + 1));
        }
        else
        {
            SetFaceTexture(value);
        }

        _textureRect.Modulate = NormalModulate;
    }

    /// <summary>
    /// Sets the visual state (border color) of the die.
    /// </summary>
    public void SetVisualState(DieVisualState state)
    {
        _panel.PanelOverride = state switch
        {
            DieVisualState.Selected => _selectedStyle,
            DieVisualState.Kept => _keptStyle,
            _ => _normalStyle,
        };

        _textureRect.Modulate = state == DieVisualState.Kept ? KeptModulate : NormalModulate;
    }

    /// <summary>
    /// Sets the die to a dimmed unknown state before the first roll.
    /// </summary>
    public void SetUnknown()
    {
        _isAnimating = false;
        _textureRect.Texture = _faceTextures.Length > 0 ? _faceTextures[0] : null;
        _textureRect.Modulate = UnknownModulate;
        _numberLabel.Text = "";
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (!_isAnimating)
            return;

        _animationElapsed += args.DeltaSeconds;

        if (_animationElapsed >= AnimationDuration)
        {
            _isAnimating = false;
            SetFaceTexture(_finalValue);
            return;
        }

        if (_animationElapsed >= _nextFaceChangeAt)
        {
            // Quadratic easing: interval grows from InitialInterval to FinalInterval
            var progress = _animationElapsed / AnimationDuration;
            var currentInterval = InitialInterval + (FinalInterval - InitialInterval) * progress * progress;
            _nextFaceChangeAt = _animationElapsed + currentInterval;

            // Show a random face that differs from the current one for visual variety
            int newFace;
            do
            {
                newFace = _random.Next(1, DiceFaces + 1);
            } while (newFace == _currentValue && DiceFaces > 1);

            SetFaceTexture(newFace);
        }
    }

    private void OnKeyPressed(GUIBoundKeyEventArgs args)
    {
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        if (_disabled || _isAnimating)
            return;

        OnPressed?.Invoke();
    }

    private void SetFaceTexture(int value)
    {
        _currentValue = value;
        if (value >= 1 && value <= DiceFaces)
        {
            _textureRect.Texture = _faceTextures[value - 1];
            _numberLabel.Text = value.ToString();
        }
        else
        {
            _textureRect.Texture = null;
            _numberLabel.Text = "";
        }
    }
}
