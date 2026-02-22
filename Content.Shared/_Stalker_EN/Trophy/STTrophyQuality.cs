using Robust.Shared.Serialization;

namespace Content.Shared._Stalker_EN.Trophy;

/// <summary>
/// Defines the quality tier of a trophy item obtained from variant mutant mobs.
/// </summary>
[Serializable, NetSerializable]
public enum STTrophyQuality : byte
{
    Common,
    Uncommon,
    Rare,
    Legendary,
}
