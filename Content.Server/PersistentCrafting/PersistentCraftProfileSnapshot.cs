using System;
using System.Collections.Generic;

namespace Content.Server._Stalker.PersistentCrafting;

public sealed class PersistentCraftProfileSnapshot
{
    public Guid UserId { get; init; }
    public string CharacterName { get; init; } = string.Empty;
    public Dictionary<string, int> BranchEarnedPoints { get; init; } = new();
    public List<string> UnlockedNodes { get; init; } = new();
}
