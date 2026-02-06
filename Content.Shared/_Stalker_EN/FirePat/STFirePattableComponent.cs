using Robust.Shared.GameStates;

namespace Content.Shared._Stalker_EN.FirePat;

/// <summary>
/// Marker component for entities that can be patted to extinguish fire.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class STFirePattableComponent : Component;
