using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Weapons.Ranged.Components;

/// <summary>
/// Companion component for <see cref="BatteryAmmoProviderComponent"/> that overrides ammunition
/// to use an <see cref="HitscanPrototype"/> instead of spawning an entity.
/// When present alongside a <see cref="BatteryAmmoProviderComponent"/>, the battery's
/// <c>GetShootable</c> returns the hitscan prototype data rather than spawning a projectile entity.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HitscanBatteryAmmoProviderComponent : Component
{
    /// <summary>
    /// The hitscan prototype to use when this battery fires.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public ProtoId<HitscanPrototype> Proto;
}
