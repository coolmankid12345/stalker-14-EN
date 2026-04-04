using Robust.Shared.Serialization;

namespace Content.Shared._Stalker.PersistentCrafting;

[Serializable, NetSerializable]
public sealed class PersistentCraftBranchState
{
    public string Branch;
    public int AvailablePoints;
    public int SpentPoints;

    public PersistentCraftBranchState(
        string branch,
        int availablePoints,
        int spentPoints)
    {
        Branch = branch;
        AvailablePoints = availablePoints;
        SpentPoints = spentPoints;
    }
}

[Serializable, NetSerializable]
public sealed class PersistentCraftState
{
    public bool Loaded;
    public List<PersistentCraftBranchState> BranchStates;
    public List<string> UnlockedNodes;

    public PersistentCraftState(
        bool loaded,
        List<PersistentCraftBranchState> branchStates,
        List<string> unlockedNodes)
    {
        Loaded = loaded;
        BranchStates = branchStates;
        UnlockedNodes = unlockedNodes;
    }
}

[Serializable, NetSerializable]
public sealed class RequestPersistentCraftStateEvent : EntityEventArgs
{
}

[Serializable, NetSerializable]
public sealed class PersistentCraftStateEvent : EntityEventArgs
{
    public PersistentCraftState State { get; }

    public PersistentCraftStateEvent(PersistentCraftState state)
    {
        State = state;
    }
}

[Serializable, NetSerializable]
public enum PersistentCraftRecipeExecutionResult : byte
{
    Completed = 0,
    Cancelled = 1,
}

[Serializable, NetSerializable]
public sealed class PersistentCraftRecipeStartedEvent : EntityEventArgs
{
    public string RecipeId { get; }
    public float DurationSeconds { get; }

    public PersistentCraftRecipeStartedEvent(string recipeId, float durationSeconds)
    {
        RecipeId = recipeId;
        DurationSeconds = durationSeconds;
    }
}

[Serializable, NetSerializable]
public sealed class PersistentCraftRecipeFinishedEvent : EntityEventArgs
{
    public string RecipeId { get; }
    public PersistentCraftRecipeExecutionResult Result { get; }

    public PersistentCraftRecipeFinishedEvent(string recipeId, PersistentCraftRecipeExecutionResult result)
    {
        RecipeId = recipeId;
        Result = result;
    }
}

[Serializable, NetSerializable]
public sealed class RequestPersistentCraftUnlockEvent : EntityEventArgs
{
    public string NodeId { get; }

    public RequestPersistentCraftUnlockEvent(string nodeId)
    {
        NodeId = nodeId;
    }
}
