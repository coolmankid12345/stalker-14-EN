using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Shared._Stalker.PersistentCrafting;

namespace Content.Server._Stalker.PersistentCrafting;

public sealed class PersistentCraftProfileRepository
{
    private const int CurrentSaveDataVersion = 2;

    private readonly IServerDbManager _db;
    private readonly PersistentCraftBranchRegistry _branchRegistry;
    private readonly ConcurrentDictionary<string, SaveLockHandle> _saveLocks = new();

    public PersistentCraftProfileRepository(IServerDbManager db, PersistentCraftBranchRegistry branchRegistry)
    {
        _db = db;
        _branchRegistry = branchRegistry;
    }

    public async Task<PersistentCraftProfileLoadResult> LoadProfileAsync(Guid userId, string characterName)
    {
        try
        {
            var saved = await _db.GetStalkerPersistentCraftProfileAsync(userId, characterName);
            if (saved is null)
                return PersistentCraftProfileLoadResult.NoData();

            if (!TryDeserializeSaveData(saved.ProfileJson, characterName, out var saveData, out var changed, out var error))
                return PersistentCraftProfileLoadResult.Failed(error ?? $"[PersistentCraft] Failed to parse save data for '{characterName}'.");

            return PersistentCraftProfileLoadResult.Loaded(saveData, changed);
        }
        catch (Exception ex)
        {
            return PersistentCraftProfileLoadResult.Failed($"[PersistentCraft] Failed to load profile for '{characterName}': {ex}");
        }
    }

    public async Task SaveProfileAsync(PersistentCraftProfileSnapshot profile)
    {
        var lockKey = GetSaveLockKey(profile);
        var saveLock = RentSaveLock(lockKey);
        var lockTaken = false;

        try
        {
            await saveLock.Semaphore.WaitAsync();
            lockTaken = true;

            await _db.SetStalkerPersistentCraftProfileAsync(
                profile.UserId,
                profile.CharacterName,
                SerializeSaveData(profile));
        }
        finally
        {
            if (lockTaken)
                saveLock.Semaphore.Release();

            ReleaseSaveLock(lockKey, saveLock);
        }
    }

    public string SerializeSaveData(PersistentCraftProfileSnapshot profile)
    {
        var saveData = CreateDefaultSaveData();

        for (var i = 0; i < saveData.Branches.Count; i++)
        {
            var branchData = saveData.Branches[i];
            if (!profile.BranchEarnedPoints.TryGetValue(branchData.Branch, out var totalEarnedPoints))
                continue;

            branchData.TotalEarnedPoints = Math.Max(0, totalEarnedPoints);
        }

        var unlockedNodes = profile.UnlockedNodes
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .OrderBy(id => id)
            .ToList();

        saveData.UnlockedNodes = unlockedNodes;

        return JsonSerializer.Serialize(saveData);
    }

    private bool TryDeserializeSaveData(
        string json,
        string characterName,
        out PersistentCraftSaveData saveData,
        out bool changed,
        out string? errorMessage)
    {
        saveData = default!;
        changed = false;
        errorMessage = null;

        PersistentCraftSaveData? data;

        try
        {
            data = JsonSerializer.Deserialize<PersistentCraftSaveData>(json);
        }
        catch (Exception ex)
        {
            errorMessage = $"[PersistentCraft] Save parse failed for '{characterName}': {ex}";
            return false;
        }

        if (data is null)
        {
            errorMessage = $"[PersistentCraft] Save data for '{characterName}' deserialized to null.";
            return false;
        }

        if (!TryValidateAndMigrateSaveData(data, characterName, out var migrated, out var migratedChanged, out errorMessage))
            return false;

        saveData = NormalizeSaveData(migrated, out var normalizedChangedAfterMigration);
        changed = migratedChanged || normalizedChangedAfterMigration;
        return true;
    }

    private bool TryValidateAndMigrateSaveData(
        PersistentCraftSaveData data,
        string characterName,
        out PersistentCraftSaveData migrated,
        out bool changed,
        out string? errorMessage)
    {
        migrated = data;
        changed = false;
        errorMessage = null;

        if (data.Version < 0)
        {
            errorMessage = $"[PersistentCraft] Save data for '{characterName}' has invalid version {data.Version}.";
            return false;
        }

        if (data.Version > CurrentSaveDataVersion)
        {
            errorMessage = $"[PersistentCraft] Save data for '{characterName}' was created by a newer version ({data.Version}). Expected {CurrentSaveDataVersion}. Data cannot be loaded safely.";
            return false;
        }

        // Цепочка миграций: каждый case мигрирует до следующей версии и проваливается дальше.
        // При добавлении версии N: добавь case (N-1) с методом MigrateV{N-1}ToV{N},
        // поставь changed = true и убери break — goto case N сделает остальное.
        switch (data.Version)
        {
            case 1:
                // v1 → v2: структура идентична, только обновляем номер версии.
                data.Version = 2;
                changed = true;
                goto case 2;

            case 2: // CurrentSaveDataVersion
                migrated = data;
                return true;

            default:
                // Версия 0 никогда не существовала в production.
                // Любая неизвестная старая версия трактуется как чистый профиль.
                migrated = data;
                changed = true;
                return true;
        }
    }

    private PersistentCraftSaveData NormalizeSaveData(PersistentCraftSaveData data, out bool changed)
    {
        changed = data.Branches == null || data.UnlockedNodes == null;

        var normalized = CreateDefaultSaveData();
        normalized.Version = data.Version;

        // Используем одну переменную, без двойного вычисления
        var sourceUnlockedNodes = data.UnlockedNodes ?? new List<string>();
        normalized.UnlockedNodes = sourceUnlockedNodes
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .OrderBy(id => id)
            .ToList();

        if (!sourceUnlockedNodes.SequenceEqual(normalized.UnlockedNodes))
            changed = true;

        if (data.Branches == null)
            return normalized;

        var seenBranches = new HashSet<string>();

        for (var i = 0; i < data.Branches.Count; i++)
        {
            var branchData = data.Branches[i];

            if (!seenBranches.Add(branchData.Branch))
            {
                changed = true;
                continue;
            }

            var target = normalized.Branches.FirstOrDefault(branch => branch.Branch == branchData.Branch);
            if (target == null)
            {
                changed = true;
                continue;
            }

            var totalEarnedPoints = Math.Max(0, branchData.TotalEarnedPoints);
            if (totalEarnedPoints != branchData.TotalEarnedPoints)
                changed = true;

            target.TotalEarnedPoints = totalEarnedPoints;
        }

        if (seenBranches.Count != normalized.Branches.Count)
            changed = true;

        return normalized;
    }

    private PersistentCraftSaveData CreateDefaultSaveData()
    {
        var branches = new List<PersistentCraftBranchSaveData>(_branchRegistry.OrderedBranchIds.Count);

        for (var i = 0; i < _branchRegistry.OrderedBranchIds.Count; i++)
        {
            branches.Add(new PersistentCraftBranchSaveData
            {
                Branch = _branchRegistry.OrderedBranchIds[i],
            });
        }

        return new PersistentCraftSaveData
        {
            Version = CurrentSaveDataVersion,
            Branches = branches,
            UnlockedNodes = new List<string>(),
        };
    }

    private SaveLockHandle RentSaveLock(string lockKey)
    {
        while (true)
        {
            var saveLock = _saveLocks.GetOrAdd(lockKey, _ => new SaveLockHandle());
            Interlocked.Increment(ref saveLock.Renters);

            if (_saveLocks.TryGetValue(lockKey, out var current) && ReferenceEquals(current, saveLock))
                return saveLock;

            Interlocked.Decrement(ref saveLock.Renters);
        }
    }

    private void ReleaseSaveLock(string lockKey, SaveLockHandle saveLock)
    {
        if (Interlocked.Decrement(ref saveLock.Renters) != 0)
            return;

        _saveLocks.TryRemove(new KeyValuePair<string, SaveLockHandle>(lockKey, saveLock));
    }

    private static string GetSaveLockKey(PersistentCraftProfileSnapshot profile)
    {
        return $"{profile.UserId:N}|{profile.CharacterName}";
    }

    private sealed class SaveLockHandle
    {
        public readonly SemaphoreSlim Semaphore = new(1, 1);
        public int Renters;
    }
}
