using Content.Shared._Stalker.Modifier;
using Robust.Shared.GameStates;

namespace Content.Shared._Stalker_EN.Weight.Modifier;

/// <summary>
/// Component for weight self modifier status effects.
/// Attached to status effect entities to store the weight modifier value.
/// Used by STWeightModStatusSystem to track and apply weight changes.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class STWeightSelfModifierComponent : BaseFloatModifierComponent
{
    /// <summary>
    /// System for managing weight self modifier components.
    /// Handles component-based weight modification logic via BaseFloatModifierSystem.
    /// </summary>
    public sealed class STWeightSelfModifierSystem : BaseFloatModifierSystem<STWeightSelfModifierComponent>
    {
    }
}
