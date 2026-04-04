namespace Content.Server._Stalker.PersistentCrafting;

public sealed class PersistentCraftBranchProfile
{
    private int _totalEarnedPoints;
    public int TotalEarnedPoints
    {
        get => _totalEarnedPoints;
        set => _totalEarnedPoints = value < 0 ? 0 : value;
    }
}
