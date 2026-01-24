using Robust.Shared.Serialization;

namespace Content.Shared._Stalker_EN.Loadout;

/// <summary>
/// State sent from server to client with all loadout data.
/// </summary>
[Serializable, NetSerializable]
public sealed class LoadoutUpdateState : BoundUserInterfaceState
{
    public List<PlayerLoadout> Loadouts { get; }

    public LoadoutUpdateState(List<PlayerLoadout> loadouts)
    {
        Loadouts = loadouts;
    }
}

/// <summary>
/// Client requests to save current equipment as a new loadout.
/// </summary>
[Serializable, NetSerializable]
public sealed class LoadoutSaveMessage : BoundUserInterfaceMessage
{
    /// <summary>
    /// Name for the new loadout. Empty string for quick save (ID 0).
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// If true, this is a quick save to slot 0.
    /// </summary>
    public bool IsQuickSave { get; }

    public LoadoutSaveMessage(string name, bool isQuickSave = false)
    {
        Name = name;
        IsQuickSave = isQuickSave;
    }
}

/// <summary>
/// Client requests to load a specific loadout.
/// </summary>
[Serializable, NetSerializable]
public sealed class LoadoutLoadMessage : BoundUserInterfaceMessage
{
    /// <summary>
    /// ID of the loadout to load.
    /// </summary>
    public int LoadoutId { get; }

    public LoadoutLoadMessage(int loadoutId)
    {
        LoadoutId = loadoutId;
    }
}

/// <summary>
/// Client requests to delete a specific loadout.
/// </summary>
[Serializable, NetSerializable]
public sealed class LoadoutDeleteMessage : BoundUserInterfaceMessage
{
    /// <summary>
    /// ID of the loadout to delete.
    /// </summary>
    public int LoadoutId { get; }

    public LoadoutDeleteMessage(int loadoutId)
    {
        LoadoutId = loadoutId;
    }
}

/// <summary>
/// Client requests to rename a loadout.
/// </summary>
[Serializable, NetSerializable]
public sealed class LoadoutRenameMessage : BoundUserInterfaceMessage
{
    /// <summary>
    /// ID of the loadout to rename.
    /// </summary>
    public int LoadoutId { get; }

    /// <summary>
    /// New name for the loadout.
    /// </summary>
    public string NewName { get; }

    public LoadoutRenameMessage(int loadoutId, string newName)
    {
        LoadoutId = loadoutId;
        NewName = newName;
    }
}

/// <summary>
/// Client requests the current list of loadouts.
/// </summary>
[Serializable, NetSerializable]
public sealed class LoadoutRequestMessage : BoundUserInterfaceMessage
{
}

/// <summary>
/// Client requests to store all equipped items to stash without saving a loadout.
/// </summary>
[Serializable, NetSerializable]
public sealed class LoadoutQuickStoreMessage : BoundUserInterfaceMessage
{
}

/// <summary>
/// Raised on the repository entity after a loadout save or load operation completes.
/// Used to notify StalkerRepositorySystem to refresh the stash UI.
/// </summary>
[ByRefEvent]
public readonly struct LoadoutOperationCompletedEvent
{
    public readonly EntityUid Actor;
    public readonly EntityUid Repository;

    public LoadoutOperationCompletedEvent(EntityUid actor, EntityUid repository)
    {
        Actor = actor;
        Repository = repository;
    }
}
