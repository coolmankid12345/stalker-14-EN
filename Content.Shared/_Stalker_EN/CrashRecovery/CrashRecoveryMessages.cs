using System;
using Robust.Shared.Serialization;

namespace Content.Shared._Stalker_EN.CrashRecovery;

/// <summary>
/// Server → Client: indicates whether crash recovery items are available.
/// Sent through the existing StalkerRepositoryUiKey.Key BUI channel.
/// </summary>
[Serializable, NetSerializable]
public sealed class CrashRecoveryUpdateState : BoundUserInterfaceState
{
    public bool HasRecoveryData;
    public int ItemCount;

    public CrashRecoveryUpdateState(bool hasRecoveryData, int itemCount)
    {
        HasRecoveryData = hasRecoveryData;
        ItemCount = itemCount;
    }
}

/// <summary>
/// Client → Server: player requests to recover crash items (spawned at their feet).
/// </summary>
[Serializable, NetSerializable]
public sealed class CrashRecoveryClaimMessage : BoundUserInterfaceMessage
{
}
