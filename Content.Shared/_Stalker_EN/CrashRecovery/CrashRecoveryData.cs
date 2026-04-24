using System;

namespace Content.Shared._Stalker_EN.CrashRecovery;

/// <summary>
/// Atomic snapshot of a player's full item state at a point in time.
/// Contains both stash contents and equipped items to prevent duplication exploits.
/// On recovery, the stash is replaced with the snapshot, then equipped items are added on top.
/// </summary>
[Serializable]
public sealed class CrashRecoveryData
{
    /// <summary>
    /// Equipped items JSON (AllStorageInventory format).
    /// Only equipped items are saved — the stash is already persisted separately.
    /// </summary>
    public string EquippedJson { get; set; } = "";
}
