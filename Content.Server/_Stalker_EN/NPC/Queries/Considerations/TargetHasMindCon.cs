using Content.Server.NPC.Queries.Considerations;

namespace Content.Server._Stalker_EN.NPC.Queries.Considerations;

/// <summary>
/// Returns 1f if the target has an active mind (player controlling it), 0f otherwise.
/// Used to filter out catatonic/ghosted players from targeting.
/// </summary>
public sealed partial class TargetHasMindCon : UtilityConsideration
{
}
