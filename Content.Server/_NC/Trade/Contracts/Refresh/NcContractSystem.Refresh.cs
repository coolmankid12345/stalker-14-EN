using System.Runtime.InteropServices;
using Content.Shared._NC.Trade;


namespace Content.Server._NC.Trade;


public sealed partial class NcContractSystem : EntitySystem
{
    public void RefillContractsForStore(EntityUid uid, NcStoreComponent comp, string? ignoredContractId = null)
    {
        if (comp.ContractPresets.Count == 0)
            return;

        var presets = ResolveContractPresets(uid, comp.ContractPresets);
        if (presets.Count == 0)
            return;

        var limits = MergeDifficultyLimits(presets);
        if (limits.Count == 0)
            return;

        var currentCounts = CountCurrentContracts(comp);
        var poolByDifficulty = BuildCandidatePool(presets, comp, ignoredContractId);

        foreach (var (difficulty, limit) in limits)
            ProcessDifficulty(uid, comp, difficulty, limit, currentCounts, poolByDifficulty);
    }

    private List<StoreContractsPresetPrototype> ResolveContractPresets(EntityUid uid, IReadOnlyList<string> presetIds)
    {
        var presets = new List<StoreContractsPresetPrototype>(presetIds.Count);

        foreach (var presetId in presetIds)
        {
            if (string.IsNullOrWhiteSpace(presetId))
                continue;

            if (!_prototypes.TryIndex<StoreContractsPresetPrototype>(presetId, out var preset))
            {
                Sawmill.Warning($"[Contracts] Preset '{presetId}' not found for {ToPrettyString(uid)}");
                continue;
            }

            presets.Add(preset);
        }

        return presets;
    }

    private static Dictionary<string, int> MergeDifficultyLimits(IReadOnlyList<StoreContractsPresetPrototype> presets)
    {
        var merged = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var preset in presets)
        {
            foreach (var (difficulty, limit) in preset.Limits)
            {
                if (string.IsNullOrWhiteSpace(difficulty) || limit <= 0)
                    continue;

                merged[difficulty] = SaturatingAdd(merged.GetValueOrDefault(difficulty, 0), limit);
            }
        }

        return merged;
    }

    private void ProcessDifficulty(
        EntityUid uid,
        NcStoreComponent comp,
        string difficulty,
        int limit,
        Dictionary<string, int> currentCounts,
        Dictionary<string, List<(StoreContractPrototype Proto, int Weight, int CooldownMinutes)>> poolByDifficulty
    )
    {
        var current = currentCounts.GetValueOrDefault(difficulty, 0);
        var coolingDownSlots = GetActiveResolvedSlotCooldownCount(uid, difficulty);
        var needed = limit - current - coolingDownSlots;

        if (needed <= 0)
            return;

        if (!TryPrepareDifficultyPool(uid, difficulty, needed, poolByDifficulty, out var cd, out var fresh, out var recent))
            return;

        for (var i = 0; i < needed; i++)
        {
            if (!TryIssueDifficultyContract(uid, comp, fresh, recent, cd))
                break;
        }
    }

    private bool TryPrepareDifficultyPool(
        EntityUid uid,
        string difficulty,
        int needed,
        Dictionary<string, List<(StoreContractPrototype Proto, int Weight, int CooldownMinutes)>> poolByDifficulty,
        out CooldownState cooldown,
        out List<(StoreContractPrototype Proto, int Weight, int CooldownMinutes)> fresh,
        out List<(StoreContractPrototype Proto, int Weight, int CooldownMinutes)>? recent)
    {
        cooldown = default!;
        fresh = default!;
        recent = null;

        if (!poolByDifficulty.TryGetValue(difficulty, out var pool) || pool.Count == 0)
            return false;

        var cooldownLimit = ComputeEffectiveContractCooldown(pool.Count, needed);
        cooldown = GetCooldownState(uid, difficulty);
        cooldown.Limit = cooldownLimit;
        cooldown.TrimToLimit();
        SplitDifficultyPoolByCooldown(pool, cooldown, cooldownLimit, out fresh, out recent);
        return true;
    }

    private static void SplitDifficultyPoolByCooldown(
        List<(StoreContractPrototype Proto, int Weight, int CooldownMinutes)> pool,
        CooldownState cooldown,
        int cooldownLimit,
        out List<(StoreContractPrototype Proto, int Weight, int CooldownMinutes)> fresh,
        out List<(StoreContractPrototype Proto, int Weight, int CooldownMinutes)>? recent)
    {
        if (cooldownLimit <= 0)
        {
            fresh = new(pool);
            recent = null;
            return;
        }

        fresh = new(pool.Count);
        recent = new(pool.Count);

        foreach (var entry in pool)
        {
            if (cooldown.Contains(entry.Proto.ID))
                recent.Add(entry);
            else
                fresh.Add(entry);
        }
    }

    private bool TryIssueDifficultyContract(
        EntityUid store,
        NcStoreComponent comp,
        List<(StoreContractPrototype Proto, int Weight, int CooldownMinutes)> fresh,
        List<(StoreContractPrototype Proto, int Weight, int CooldownMinutes)>? recent,
        CooldownState cooldown)
    {
        var source = fresh.Count > 0 ? fresh : recent;
        if (source == null || source.Count == 0)
            return false;

        if (!TryPickAndRemoveWeighted(source, out var pick))
            return false;

        comp.Contracts[pick.Proto.ID] = CreateContractData(store, pick.Proto);
        cooldown.Push(pick.Proto.ID);
        return true;
    }

    private Dictionary<string, List<(StoreContractPrototype Proto, int Weight, int CooldownMinutes)>> BuildCandidatePool(
        IReadOnlyList<StoreContractsPresetPrototype> presets,
        NcStoreComponent comp,
        string? ignoredContractId
    )
    {
        var flattened = GetOrBuildFlattenedPool(presets);
        var result = new Dictionary<string, List<(StoreContractPrototype Proto, int Weight, int CooldownMinutes)>>(StringComparer.Ordinal);

        foreach (var entry in flattened.Values)
        {
            var proto = entry.Proto;
            var weight = entry.Weight;

            if (weight <= 0)
                continue;

            if (ignoredContractId != null && proto.ID == ignoredContractId)
                continue;

            if (comp.Contracts.ContainsKey(proto.ID))
                continue;

            if (!proto.Repeatable && comp.CompletedOneTimeContracts.Contains(proto.ID))
                continue;

            ref var list = ref CollectionsMarshal.GetValueRefOrAddDefault(result, proto.Difficulty, out var exists);
            if (!exists)
                list = new();

            list!.Add((proto, weight, entry.CooldownMinutes));
        }

        return result;
    }

    private Dictionary<string, (StoreContractPrototype Proto, int Weight, int CooldownMinutes)> GetOrBuildFlattenedPool(
        IReadOnlyList<StoreContractsPresetPrototype> presets
    )
    {
        var cacheKey = BuildPresetPoolCacheKey(presets);
        if (_flattenedPoolCache.TryGetValue(cacheKey, out var cached))
            return cached;

        var raw = CollectFlattenedPoolEntries(presets);
        var unique = MergeFlattenedPoolEntries(cacheKey, raw);
        _flattenedPoolCache[cacheKey] = unique;
        return unique;
    }

    private List<(StoreContractPrototype Proto, int Weight, int CooldownMinutes)> CollectFlattenedPoolEntries(
        IReadOnlyList<StoreContractsPresetPrototype> presets)
    {
        var raw = new List<(StoreContractPrototype Proto, int Weight, int CooldownMinutes)>();

        foreach (var preset in presets)
        {
            foreach (var packEntry in preset.Packs)
            {
                if (string.IsNullOrWhiteSpace(packEntry.Id) || packEntry.Weight <= 0)
                    continue;

                CollectFromPackRecursive(
                    packEntry.Id,
                    packEntry.Weight,
                    raw,
                    new HashSet<string>(StringComparer.Ordinal));
            }
        }

        return raw;
    }

    private Dictionary<string, (StoreContractPrototype Proto, int Weight, int CooldownMinutes)> MergeFlattenedPoolEntries(
        string cacheKey,
        IReadOnlyList<(StoreContractPrototype Proto, int Weight, int CooldownMinutes)> raw)
    {
        var unique = new Dictionary<string, (StoreContractPrototype Proto, int Weight, int CooldownMinutes)>(StringComparer.Ordinal);

        foreach (var (proto, weight, cooldownMinutes) in raw)
            AddFlattenedPoolEntry(unique, cacheKey, proto, weight, cooldownMinutes);

        return unique;
    }

    private void AddFlattenedPoolEntry(
        Dictionary<string, (StoreContractPrototype Proto, int Weight, int CooldownMinutes)> unique,
        string cacheKey,
        StoreContractPrototype proto,
        int weight,
        int cooldownMinutes)
    {
        if (weight <= 0)
            return;

        cooldownMinutes = Math.Max(0, cooldownMinutes);
        ref var slot = ref CollectionsMarshal.GetValueRefOrAddDefault(unique, proto.ID, out var exists);
        if (!exists)
        {
            slot = (proto, weight, cooldownMinutes);
            return;
        }

        var merged = SaturatingAdd(slot.Weight, weight);
        if (merged == int.MaxValue && slot.Weight != int.MaxValue)
        {
            Sawmill.Warning(
                $"[Contracts] Total weight overflow for '{proto.ID}' in preset set '{cacheKey}'. " +
                $"Clamping to {int.MaxValue}.");
        }

        slot.Weight = merged;
        slot.CooldownMinutes = Math.Max(slot.CooldownMinutes, cooldownMinutes);
    }

    private static string BuildPresetPoolCacheKey(IReadOnlyList<StoreContractsPresetPrototype> presets)
    {
        if (presets.Count == 0)
            return string.Empty;

        if (presets.Count == 1)
            return presets[0].ID;

        var ids = new string[presets.Count];
        for (var i = 0; i < presets.Count; i++)
            ids[i] = presets[i].ID;

        Array.Sort(ids, StringComparer.Ordinal);
        return string.Join('|', ids);
    }

    private static Dictionary<string, int> CountCurrentContracts(NcStoreComponent comp)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var c in comp.Contracts.Values)
        {
            ref var count = ref CollectionsMarshal.GetValueRefOrAddDefault(counts, c.Difficulty, out _);
            count++;
        }

        return counts;
    }

    private CooldownState GetCooldownState(EntityUid store, string difficulty)
    {
        ref var state = ref CollectionsMarshal.GetValueRefOrAddDefault(
            _contractCooldown,
            (store, difficulty),
            out var exists);
        if (!exists)
            state = new();

        return state!;
    }

    private int GetActiveResolvedSlotCooldownCount(EntityUid store, string difficulty)
    {
        if (store == EntityUid.Invalid || string.IsNullOrWhiteSpace(difficulty))
            return 0;

        var key = (store, difficulty);
        if (!_contractResolvedSlotCooldowns.TryGetValue(key, out var entries) || entries.Count == 0)
            return 0;

        var now = _timing.CurTime;
        for (var i = entries.Count - 1; i >= 0; i--)
        {
            if (entries[i].ExpiresAt <= now)
                entries.RemoveAt(i);
        }

        if (entries.Count > 0)
            return entries.Count;

        _contractResolvedSlotCooldowns.Remove(key);
        return 0;
    }

    private void ApplyContractResolutionCooldown(
        EntityUid store,
        NcStoreComponent comp,
        string contractId,
        string difficulty,
        string contractName)
    {
        var cooldownMinutes = ResolveContractCooldownMinutesForStore(store, comp, contractId);
        if (cooldownMinutes <= 0 || string.IsNullOrWhiteSpace(difficulty))
            return;

        ref var entries = ref CollectionsMarshal.GetValueRefOrAddDefault(
            _contractResolvedSlotCooldowns,
            (store, difficulty),
            out var exists);
        if (!exists)
            entries = new();

        entries!.Add(
            new ResolvedSlotCooldownEntry
            {
                ContractId = contractId,
                ContractName = string.IsNullOrWhiteSpace(contractName) ? contractId : contractName,
                ExpiresAt = _timing.CurTime + TimeSpan.FromMinutes(cooldownMinutes)
            });
    }

    public bool HasActiveSlotCooldowns(EntityUid store)
    {
        if (store == EntityUid.Invalid || _contractResolvedSlotCooldowns.Count == 0)
            return false;

        foreach (var (key, entries) in _contractResolvedSlotCooldowns)
        {
            if (key.Store != store || entries.Count == 0)
                continue;

            return true;
        }

        return false;
    }

    public void RefreshExpiredSlotCooldowns(EntityUid store, NcStoreComponent comp)
    {
        if (store == EntityUid.Invalid || _contractResolvedSlotCooldowns.Count == 0)
            return;

        var expiredAny = false;
        var now = _timing.CurTime;

        _resolvedSlotCooldownKeysToRemoveScratch.Clear();
        foreach (var (key, entries) in _contractResolvedSlotCooldowns)
        {
            if (key.Store != store)
                continue;

            for (var i = entries.Count - 1; i >= 0; i--)
            {
                if (entries[i].ExpiresAt <= now)
                {
                    entries.RemoveAt(i);
                    expiredAny = true;
                }
            }

            if (entries.Count == 0)
                _resolvedSlotCooldownKeysToRemoveScratch.Add(key);
        }

        for (var i = 0; i < _resolvedSlotCooldownKeysToRemoveScratch.Count; i++)
            _contractResolvedSlotCooldowns.Remove(_resolvedSlotCooldownKeysToRemoveScratch[i]);
        _resolvedSlotCooldownKeysToRemoveScratch.Clear();

        if (expiredAny)
            RefillContractsForStore(store, comp);
    }

    public void GetActiveSlotCooldownsForClient(
        EntityUid store,
        List<SlotCooldownClientData> target)
    {
        target.Clear();
        if (store == EntityUid.Invalid || _contractResolvedSlotCooldowns.Count == 0)
            return;

        var now = _timing.CurTime;

        _resolvedSlotCooldownKeysToRemoveScratch.Clear();
        foreach (var (key, entries) in _contractResolvedSlotCooldowns)
        {
            if (key.Store != store)
                continue;

            for (var i = entries.Count - 1; i >= 0; i--)
            {
                var entry = entries[i];
                var remaining = entry.ExpiresAt - now;
                if (remaining <= TimeSpan.Zero)
                {
                    entries.RemoveAt(i);
                    continue;
                }

                target.Add(
                    new SlotCooldownClientData(
                        key.Difficulty,
                        entry.ContractId,
                        string.IsNullOrWhiteSpace(entry.ContractName) ? entry.ContractId : entry.ContractName,
                        Math.Max(1, (int) Math.Ceiling(remaining.TotalSeconds))));
            }

            if (entries.Count == 0)
                _resolvedSlotCooldownKeysToRemoveScratch.Add(key);
        }

        for (var i = 0; i < _resolvedSlotCooldownKeysToRemoveScratch.Count; i++)
            _contractResolvedSlotCooldowns.Remove(_resolvedSlotCooldownKeysToRemoveScratch[i]);
        _resolvedSlotCooldownKeysToRemoveScratch.Clear();

        if (target.Count == 0)
            return;

        target.Sort(
            static (left, right) =>
            {
                var byDifficulty = CompareDifficulty(left.Difficulty, right.Difficulty);
                if (byDifficulty != 0)
                    return byDifficulty;

                var byTime = left.RemainingSeconds.CompareTo(right.RemainingSeconds);
                if (byTime != 0)
                    return byTime;

                var byName = string.Compare(left.LastContractName, right.LastContractName, StringComparison.CurrentCulture);
                if (byName != 0)
                    return byName;

                return string.CompareOrdinal(left.LastContractId, right.LastContractId);
            });
    }

    private static int CompareDifficulty(string left, string right)
    {
        static int rank(string difficulty)
        {
            return difficulty switch
            {
                "Easy" => 0,
                "Medium" => 1,
                "Hard" => 2,
                _ => 99
            };
        }

        var byRank = rank(left).CompareTo(rank(right));
        return byRank != 0 ? byRank : string.Compare(left, right, StringComparison.CurrentCulture);
    }

    private int ResolveContractCooldownMinutesForStore(EntityUid store, NcStoreComponent comp, string contractId)
    {
        if (string.IsNullOrWhiteSpace(contractId) || comp.ContractPresets.Count == 0)
            return 0;

        var presets = ResolveContractPresets(store, comp.ContractPresets);
        if (presets.Count == 0)
            return 0;

        var flattened = GetOrBuildFlattenedPool(presets);
        return flattened.TryGetValue(contractId, out var entry)
            ? Math.Max(0, entry.CooldownMinutes)
            : 0;
    }

    private void CollectFromPackRecursive(
        string packId,
        int weightMult,
        List<(StoreContractPrototype Proto, int FinalWeight, int CooldownMinutes)> acc,
        HashSet<string> recursionStack
    )
    {
        if (string.IsNullOrWhiteSpace(packId) || weightMult <= 0)
            return;

        if (!TryEnterPackRecursion(packId, recursionStack))
            return;

        try
        {
            if (!TryResolveContractPack(packId, out var pack))
                return;

            CollectPackContractEntries(packId, weightMult, pack, acc);
            CollectPackIncludedEntries(packId, weightMult, pack, acc, recursionStack);
        }
        finally
        {
            recursionStack.Remove(packId);
        }
    }

    private bool TryEnterPackRecursion(string packId, HashSet<string> recursionStack)
    {
        if (recursionStack.Add(packId))
            return true;

        Sawmill.Warning($"[Contracts] Cyclic include detected for pack '{packId}'.");
        return false;
    }

    private bool TryResolveContractPack(string packId, out StoreContractPackPrototype pack)
    {
        if (_prototypes.TryIndex<StoreContractPackPrototype>(packId, out pack!))
            return true;

        Sawmill.Error($"[Contracts] Pack '{packId}' not found.");
        return false;
    }

    private void CollectPackContractEntries(
        string packId,
        int weightMult,
        StoreContractPackPrototype pack,
        List<(StoreContractPrototype Proto, int FinalWeight, int CooldownMinutes)> acc)
    {
        foreach (var entry in pack.Contracts)
        {
            if (entry.Weight <= 0 || !_prototypes.TryIndex<StoreContractPrototype>(entry.Id, out var proto))
                continue;

            var finalWeight = MultiplyWeightsWithClamp(
                weightMult,
                entry.Weight,
                $"pack '{packId}' contract '{entry.Id}'");

            if (finalWeight > 0)
                acc.Add((proto, finalWeight, Math.Max(0, entry.CooldownMinutes)));
        }
    }

    private void CollectPackIncludedEntries(
        string packId,
        int weightMult,
        StoreContractPackPrototype pack,
        List<(StoreContractPrototype Proto, int FinalWeight, int CooldownMinutes)> acc,
        HashSet<string> recursionStack)
    {
        foreach (var include in pack.Includes)
        {
            if (include.Weight <= 0)
                continue;

            var nestedWeight = MultiplyWeightsWithClamp(
                weightMult,
                include.Weight,
                $"pack '{packId}' include '{include.Id}'");

            if (nestedWeight > 0)
                CollectFromPackRecursive(include.Id, nestedWeight, acc, recursionStack);
        }
    }

    private int MultiplyWeightsWithClamp(int left, int right, string context)
    {
        if (left <= 0 || right <= 0)
            return 0;

        var scaled = (long) left * right;
        if (scaled <= 0)
            return 0;

        if (scaled <= int.MaxValue)
            return (int) scaled;

        Sawmill.Warning(
            $"[Contracts] Weight overflow in {context}: {left} * {right}. Clamping to {int.MaxValue}.");

        return int.MaxValue;
    }

    private static int SaturatingAdd(int left, int right)
    {
        if (left <= 0)
            return Math.Max(0, right);
        if (right <= 0)
            return left;

        var sum = (long) left + right;
        return sum >= int.MaxValue ? int.MaxValue : (int) sum;
    }

    private static int ComputeEffectiveContractCooldown(int poolCount, int needed)
    {
        if (poolCount <= 1 || needed <= 0)
            return 0;

        var upper = Math.Min(poolCount - 1, poolCount - needed);
        return Math.Max(0, upper);
    }

    private bool TryPickAndRemoveWeighted(
        List<(StoreContractPrototype Proto, int Weight, int CooldownMinutes)> list,
        out (StoreContractPrototype Proto, int Weight, int CooldownMinutes) picked
    )
    {
        picked = default;

        var total = 0;
        for (var i = 0; i < list.Count; i++)
        {
            var w = list[i].Weight;
            if (w <= 0)
                continue;

            total = SaturatingAdd(total, w);
        }

        if (total <= 0)
            return false;

        var roll = _random.Next(total);

        for (var i = 0; i < list.Count; i++)
        {
            var w = list[i].Weight;
            if (w <= 0)
                continue;

            roll -= w;
            if (roll >= 0)
                continue;

            picked = list[i];

            var last = list.Count - 1;
            list[i] = list[last];
            list.RemoveAt(last);
            return true;
        }

        return false;
    }
}
