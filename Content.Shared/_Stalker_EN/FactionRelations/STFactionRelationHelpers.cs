namespace Content.Shared._Stalker_EN.FactionRelations;

/// <summary>
/// Shared helpers for faction relation pair normalization and classification.
/// </summary>
public static class STFactionRelationHelpers
{
    /// <summary>
    /// Normalizes a faction pair so that FactionA is always alphabetically first.
    /// </summary>
    public static (string, string) NormalizePair(string a, string b)
    {
        return string.Compare(a, b, StringComparison.Ordinal) <= 0 ? (a, b) : (b, a);
    }

    /// <summary>
    /// Returns true if the transition requires bilateral confirmation from the target faction.
    /// De-escalation (War->Hostile, Hostile->Neutral) and Alliance proposals require confirmation.
    /// </summary>
    public static bool RequiresConfirmation(STFactionRelationType current, STFactionRelationType proposed)
    {
        // Alliance always requires bilateral confirmation
        if (proposed == STFactionRelationType.Alliance)
            return true;

        // Breaking alliance is unilateral
        if (current == STFactionRelationType.Alliance)
            return false;

        // De-escalation: proposed is less aggressive than current
        // War(3) -> Hostile(2), War(3) -> Neutral(0), Hostile(2) -> Neutral(0)
        return (int) proposed < (int) current;
    }

    /// <summary>
    /// Resolves a faction ID to its human-readable display name.
    /// Falls back to the raw ID if no display name is configured.
    /// </summary>
    public static string GetDisplayName(string factionId, Dictionary<string, string>? displayNames)
    {
        if (displayNames != null && displayNames.TryGetValue(factionId, out var name))
            return name;
        return factionId;
    }

    /// <summary>
    /// Returns true if the transition is an escalation toward conflict or breaking an alliance.
    /// These changes are applied unilaterally (no confirmation needed).
    /// </summary>
    public static bool IsEscalation(STFactionRelationType current, STFactionRelationType proposed)
    {
        // Breaking an alliance is treated as escalation (unilateral)
        if (current == STFactionRelationType.Alliance && proposed != STFactionRelationType.Alliance)
            return true;

        // Moving toward more conflict: Neutral(0)->Hostile(2), Neutral(0)->War(3), Hostile(2)->War(3)
        // Excludes Alliance since that requires confirmation
        return proposed != STFactionRelationType.Alliance && (int) proposed > (int) current;
    }
}
