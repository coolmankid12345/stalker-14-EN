using Content.Shared.Storage;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;

namespace Content.Shared._Stalker_EN.Loadout;

/// <summary>
/// Represents a nested item inside a container (e.g., magazine in vest, medkit in backpack).
/// Supports recursive nesting for containers within containers.
/// </summary>
[Serializable, NetSerializable]
public sealed class LoadoutNestedItem
{
    /// <summary>
    /// The container ID this item is stored in (e.g., "storagebase").
    /// </summary>
    public string ContainerName { get; set; } = string.Empty;

    /// <summary>
    /// The prototype ID of the item.
    /// </summary>
    public string PrototypeId { get; set; } = string.Empty;

    /// <summary>
    /// Serialized item state (IItemStalkerStorage data).
    /// Not sent over network - only used server-side and in database.
    /// </summary>
    [field: NonSerialized]
    public object? StorageData { get; set; }

    /// <summary>
    /// Unique identifier for matching items in stash.
    /// </summary>
    public string Identifier { get; set; } = string.Empty;

    /// <summary>
    /// Grid position X coordinate within storage (if applicable).
    /// </summary>
    public int? StoragePositionX { get; set; }

    /// <summary>
    /// Grid position Y coordinate within storage (if applicable).
    /// </summary>
    public int? StoragePositionY { get; set; }

    /// <summary>
    /// Rotation direction (0-7, corresponding to Direction enum) within storage.
    /// </summary>
    public int? StorageDirection { get; set; }

    /// <summary>
    /// Gets the storage location as ItemStorageLocation if position data exists.
    /// Computed property - not serialized directly (uses StoragePositionX/Y/Direction).
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public ItemStorageLocation? StorageLocation
    {
        get
        {
            if (StoragePositionX.HasValue && StoragePositionY.HasValue && StorageDirection.HasValue)
                return new ItemStorageLocation(((Direction)StorageDirection.Value).ToAngle(), new Vector2i(StoragePositionX.Value, StoragePositionY.Value));
            return null;
        }
        set
        {
            if (value.HasValue)
            {
                StoragePositionX = value.Value.Position.X;
                StoragePositionY = value.Value.Position.Y;
                StorageDirection = (int)value.Value.Direction;
            }
            else
            {
                StoragePositionX = null;
                StoragePositionY = null;
                StorageDirection = null;
            }
        }
    }

    /// <summary>
    /// Items nested inside this item's container (recursive).
    /// </summary>
    public List<LoadoutNestedItem> NestedItems { get; set; } = new();

    #region Grid Position (for StorageComponent containers)

    /// <summary>
    /// Grid position X coordinate. Null = auto-place (backward compatible with old loadouts).
    /// </summary>
    public int? GridX { get; set; }

    /// <summary>
    /// Grid position Y coordinate. Null = auto-place (backward compatible with old loadouts).
    /// </summary>
    public int? GridY { get; set; }

    /// <summary>
    /// Item rotation in storage grid. Null = default rotation (South).
    /// </summary>
    public Direction? GridRotation { get; set; }

    /// <summary>
    /// Returns true if this item has a saved grid position.
    /// </summary>
    public bool HasGridPosition => GridX.HasValue && GridY.HasValue;

    /// <summary>
    /// Converts the saved grid position to an ItemStorageLocation.
    /// Returns null if no position is saved.
    /// </summary>
    public ItemStorageLocation? GetStorageLocation()
    {
        if (!HasGridPosition)
            return null;

        return new ItemStorageLocation(
            (GridRotation ?? Direction.South).ToAngle(),
            new Vector2i(GridX!.Value, GridY!.Value));
    }

    #endregion
}
