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
    CanScore,
}

/// <summary>
/// Custom UI control that renders a single d6 die using sprites from dice.rsi,
/// with a colored border to indicate selection/kept/scoring state and a rolling animation.
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

    private static readonly Color NormalBorderColor = Color.FromHex("#555555");
    private static readonly Color SelectedBorderColor = Color.LimeGreen;
    private static readonly Color KeptBorderColor = Color.FromHex("#888888");
    private static readonly Color CanScoreBorderColor = Color.FromHex("#CCCC66");
    private static readonly Color UnknownModulate = new(0.4f, 0.4f, 0.4f);
    private static readonly Color NormalModulate = Color.White;
    private static readonly Color KeptModulate = new(0.5f, 0.5f, 0.5f);

    private readonly IRobustRandom _random;
    private readonly Texture[] _faceTextures = new Texture[DiceFaces];
    private readonly PanelContainer _panel;
    private readonly TextureRect _textureRect;

    // Pre-allocated style boxes to avoid per-frame allocation
    private readonly StyleBoxFlat _normalStyle;
    private readonly StyleBoxFlat _selectedStyle;
    private readonly StyleBoxFlat _keptStyle;
    private readonly StyleBoxFlat _canScoreStyle;

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

        var borderThickness = new Thickness(2);
        _normalStyle = new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex("#222222"),
            BorderColor = NormalBorderColor,
            BorderThickness = borderThickness,
        };
        _selectedStyle = new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex("#1A3A1A"),
            BorderColor = SelectedBorderColor,
            BorderThickness = borderThickness,
        };
        _keptStyle = new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex("#2A2A2A"),
            BorderColor = KeptBorderColor,
            BorderThickness = borderThickness,
        };
        _canScoreStyle = new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex("#2A2A1A"),
            BorderColor = CanScoreBorderColor,
            BorderThickness = borderThickness,
        };

        _panel = new PanelContainer
        {
            PanelOverride = _normalStyle,
        };

        _textureRect = new TextureRect
        {
            TextureScale = new Vector2(2, 2),
            Stretch = TextureRect.StretchMode.KeepCentered,
        };

        _panel.AddChild(_textureRect);
        AddChild(_panel);

        // 64px die (32 * 2 scale) + 4px border + 4px margin = 72px, use 68 for tight fit
        MinSize = new Vector2(68, 68);

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
            DieVisualState.CanScore => _canScoreStyle,
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
            _textureRect.Texture = _faceTextures[value - 1];
        else
            _textureRect.Texture = null;
    }
}
