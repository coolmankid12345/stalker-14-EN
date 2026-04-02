using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stalker_EN.CharacterRank;

/// <summary>
/// Tracks a character's rank progression based on accumulated playtime.
/// Added to player entities at spawn by the server rank system.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class STCharacterRankComponent : Component
{
    /// <summary>
    /// Current rank index (0-7), networked for client display.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int RankIndex;

    /// <summary>
    /// The JobIcon prototype ID for the current rank, networked for client icon rendering.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<JobIconPrototype> RankIconId = "STRankNovice";

    /// <summary>
    /// Localization key for the rank's display name. Server-only to avoid dirtying on every flush.
    /// </summary>
    [DataField, AutoNetworkedField]
    public LocId RankName = "st-rank-novice";

    /// <summary>
    /// Total accumulated active playtime. Server-only to avoid dirtying on every flush.
    /// </summary>
    [DataField]
    public TimeSpan AccumulatedTime;

    [DataField("action"), ViewVariables(VVAccess.ReadOnly)]
    public string Action = "ActionToggleRank";

    [DataField] public EntityUid? ActionEntity;

    [AutoNetworkedField]
    public bool Enabled = true;
}
