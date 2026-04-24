using Content.Shared.CartridgeLoader;
using Robust.Shared.Serialization;

namespace Content.Shared._Stalker.PdaMessenger;

/// <summary>
/// Event sent from client to server to toggle Clear Sky disguise mode.
/// </summary>
[Serializable, NetSerializable]
public sealed class STMessengerToggleDisguiseEvent : CartridgeMessageEvent
{
}
