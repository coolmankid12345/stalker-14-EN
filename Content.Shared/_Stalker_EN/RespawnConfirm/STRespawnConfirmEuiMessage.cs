using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._Stalker_EN.RespawnConfirm;

[Serializable, NetSerializable]
public enum STRespawnConfirmButton
{
    Deny,
    Accept,
}

[Serializable, NetSerializable]
public sealed class STRespawnConfirmMessage : EuiMessageBase
{
    public readonly STRespawnConfirmButton Button;

    public STRespawnConfirmMessage(STRespawnConfirmButton button)
    {
        Button = button;
    }
}
