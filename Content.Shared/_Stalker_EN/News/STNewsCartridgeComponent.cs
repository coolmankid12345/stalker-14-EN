namespace Content.Shared._Stalker_EN.News;

/// <summary>
/// Marker component for the Stalker News PDA cartridge program.
/// </summary>
[RegisterComponent]
public sealed partial class STNewsCartridgeComponent : Component
{
    /// <summary>
    /// Set by <see cref="STOpenNewsArticleEvent"/>, consumed and cleared when building next UI state.
    /// </summary>
    [ViewVariables]
    public int? PendingArticleId;

    /// <summary>Newest article ID this user has seen. Articles with Id > this are "new".</summary>
    [ViewVariables]
    public int LastSeenArticleId;

    /// <summary>Earliest time this cartridge can publish again (server-side cooldown).</summary>
    [ViewVariables]
    public TimeSpan NextPublishTime;
}
