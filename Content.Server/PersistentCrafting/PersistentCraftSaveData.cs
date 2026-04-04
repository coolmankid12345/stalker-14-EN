using System.Collections.Generic;

namespace Content.Server._Stalker.PersistentCrafting;

public sealed class PersistentCraftSaveData
{
    public int Version { get; set; }
    public List<PersistentCraftBranchSaveData> Branches { get; set; } = new();
    public List<string> UnlockedNodes { get; set; } = new();
}

public sealed class PersistentCraftBranchSaveData
{
    public string Branch { get; set; } = string.Empty;
    public int TotalEarnedPoints { get; set; }
}

public sealed class PersistentCraftProfileLoadResult
{
    public bool Success { get; }
    public bool HasSavedProfile { get; }
    public PersistentCraftSaveData? SaveData { get; }
    public bool DataChanged { get; }
    public string? ErrorMessage { get; }

    private PersistentCraftProfileLoadResult(
        bool success,
        bool hasSavedProfile,
        PersistentCraftSaveData? saveData,
        bool dataChanged,
        string? errorMessage)
    {
        Success = success;
        HasSavedProfile = hasSavedProfile;
        SaveData = saveData;
        DataChanged = dataChanged;
        ErrorMessage = errorMessage;
    }

    public static PersistentCraftProfileLoadResult NoData()
    {
        return new PersistentCraftProfileLoadResult(
            success: true,
            hasSavedProfile: false,
            saveData: null,
            dataChanged: false,
            errorMessage: null);
    }

    public static PersistentCraftProfileLoadResult Loaded(PersistentCraftSaveData saveData, bool dataChanged)
    {
        return new PersistentCraftProfileLoadResult(
            success: true,
            hasSavedProfile: true,
            saveData: saveData,
            dataChanged: dataChanged,
            errorMessage: null);
    }

    public static PersistentCraftProfileLoadResult Failed(string errorMessage)
    {
        return new PersistentCraftProfileLoadResult(
            success: false,
            hasSavedProfile: false,
            saveData: null,
            dataChanged: false,
            errorMessage: errorMessage);
    }
}
