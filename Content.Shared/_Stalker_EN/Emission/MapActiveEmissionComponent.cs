

using Robust.Shared.GameStates;

namespace Content.Shared._Stalker_EN.Emission;

/// <summary>
///     Added to maps with ongoing emission so that visuals
///         can properly be rendered.
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class MapActiveEmissionComponent : Component
{
    /// <summary>
    ///     One of two colors that will be used for active emission.
    ///         Emission will get closer and closer to this color with lower emission strength.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public Color PrimaryEmissionColor = Color.FromHex("#FF0000FF");

    /// <summary>
    ///     One of two colors that will be used for active emission.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public Color SecondaryEmissionColor = Color.FromHex("#E05B26");

    /// <summary>
    ///     Set to 1 for seizure.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public float Strength = 0.35f;

    /// <summary>
    ///     Strength of emission's random color-changing from 0 (dead) to 1 (full).
    ///         If <see cref="TotalStrengthDecreaseTime"/> is not-null,
    ///         this value will be interpolated to 0 as current gametime
    ///         reaches that.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public float Deviation = 1f;

    /// <summary>
    ///     Game-time at which emission strength starts decreasing.
    ///         Nothing happens if this is null.
    /// </summary>
    [DataField, AutoPausedField]
    [AutoNetworkedField]
    public TimeSpan TotalDeviationDecreaseStartTime = TimeSpan.Zero;

    /// <summary>
    ///     Rate of strength decrease
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public float TotalDeviationDecreaseRate = 0.5f;
}
