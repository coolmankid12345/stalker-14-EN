using Robust.Shared.GameStates;
using Robust.Shared.Map;

namespace Content.Shared._Stalker.Weapon.Projectile;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(STProjectileSystem))]
public sealed partial class STProjectileDamageFalloffComponent : Component
{
    /// <summary>
    /// This lists all the thresholds and their falloff values.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<DamageFalloffThreshold> Thresholds = new()
    {
        new DamageFalloffThreshold(0f, 0f, false),      // 0-8: full damage
        new DamageFalloffThreshold(8f, 1.5f, false),    // 8-15: lose 1.5 per unit
        new DamageFalloffThreshold(15f, 3f, false),     // 15-25: lose 3 per unit
        new DamageFalloffThreshold(25f, 5f, false),     // 25+: lose 5 per unit
    };

    /// <summary>
    /// This determines the minimum fraction of the projectile's original damage that must remain after falloff is applied.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MinRemainingDamageModifier = 0.15f;

    /// <summary>
    /// This is the additional falloff multiplier applied by the firing weapon.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float WeaponModifier = 1;

    /// <summary>
    /// These are the coordinates from which the projectile was shot. Used to determine the distance travelled.
    /// </summary>
    [DataField(readOnly: true), AutoNetworkedField]
    public EntityCoordinates? StartCoordinates;
}
