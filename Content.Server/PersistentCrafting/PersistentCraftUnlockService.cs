using Content.Shared._Stalker.PersistentCrafting;

namespace Content.Server._Stalker.PersistentCrafting;

public enum PersistentCraftUnlockFailure
{
    None = 0,
    AutoUnlockedNode,
    AlreadyUnlocked,
    MissingPrerequisites,
    NotEnoughPoints,
}

public sealed class PersistentCraftUnlockService
{
    private readonly PersistentCraftProfileService _profileService;

    public PersistentCraftUnlockService(PersistentCraftProfileService profileService)
    {
        _profileService = profileService;
    }

    public bool TryUnlockNode(
        PersistentCraftProfileComponent profile,
        PersistentCraftNodePrototype node,
        out PersistentCraftUnlockFailure failure)
    {
        if (PersistentCraftingHelper.IsAutoUnlockedNode(node))
        {
            failure = PersistentCraftUnlockFailure.AutoUnlockedNode;
            return false;
        }

        if (profile.UnlockedNodes.Contains(node.ID))
        {
            failure = PersistentCraftUnlockFailure.AlreadyUnlocked;
            return false;
        }

        if (!_profileService.AreNodePrerequisitesMet(profile, node))
        {
            failure = PersistentCraftUnlockFailure.MissingPrerequisites;
            return false;
        }

        var availablePoints = _profileService.GetAvailableBranchPoints(profile, node.Branch);
        if (availablePoints < node.Cost)
        {
            failure = PersistentCraftUnlockFailure.NotEnoughPoints;
            return false;
        }

        profile.UnlockedNodes.Add(node.ID);

        failure = PersistentCraftUnlockFailure.None;
        return true;
    }
}
