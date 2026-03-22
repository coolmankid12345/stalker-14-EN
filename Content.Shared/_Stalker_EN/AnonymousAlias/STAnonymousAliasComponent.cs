using Robust.Shared.GameStates;

namespace Content.Shared._Stalker_EN.AnonymousAlias;

/// <summary>
/// Stores a player's anonymous alias and optional name color, displayed when
/// their identity is blocked by a mask or helmet.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedSTAnonymousAliasSystem), Other = AccessPermissions.ReadExecute)]
public sealed partial class STAnonymousAliasComponent : Component
{
    /// <summary>
    ///     The composed alias string, e.g. "Scarred Stalker".
    ///         You should really use <see cref="SharedSTAnonymousAliasSystem.TryGetFullAlias"/>
    ///         and whatnot instead of directly reading this.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string Alias = string.Empty;

    /// <summary>
    ///     Locale that will be used for final alias,
    ///         with <see cref="Alias"/> given to it as a parameter
    ///         named 'alias'.
    ///
    ///     If this is null, just the alias will be returned.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string? TransformLoc = "st-anonymous-alias-default-transform";

    /// <summary>
    /// Player-chosen name color. Null means use default chat color behavior.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Color? AliasColor;
}
