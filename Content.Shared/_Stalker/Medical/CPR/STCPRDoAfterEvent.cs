using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Stalker.Medical.CPR;

[Serializable, NetSerializable]
public sealed partial class STCPRDoAfterEvent : SimpleDoAfterEvent;
