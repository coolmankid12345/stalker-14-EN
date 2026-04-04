using Robust.Shared.GameStates;
using Robust.Shared.Map;

namespace Content.Shared._Stalker.Weapon.Projectile;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(STProjectileSystem))]
public sealed partial class STProjectileDamageFalloffComponent : Component
{
    /// <summary>
    /// Damage falloff thresholds defining how much damage is lost per tile in each range band.
    ///
    /// The bands approximate exponential decay using escalating linear rates:
    ///
    ///   0-6   tiles: full damage, no falloff
    ///   6-10  tiles: lose 3.0 per tile  → ~25% lost at tile 10 (from 56 base: ~42 remaining)
    ///   10-15 tiles: lose 4.5 per tile  → ~65% lost at tile 15 (from 56 base: ~19 remaining)
    ///   15-20 tiles: lose 6.0 per tile  → floor kicks in around tile 17-18
    ///   20+   tiles: lose 8.0 per tile  → hard floor at 15% of original damage
    ///
    /// MinRemainingDamageModifier = 0.15 ensures bullets never become completely harmless.
    /// At extreme range (20+ tiles), damage stabilizes at ~8 from a 56-damage bullet.
    ///
    /// Weapons can override these thresholds in YAML to tune per-weapon range profiles.
    /// The WeaponModifier field further scales falloff rates (e.g. SMGs fall off faster).
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<DamageFalloffThreshold> Thresholds = new()
    {
        new DamageFalloffThreshold(0f,  0.0f, false),   // 0-6 tiles:   no falloff, full damage
        new DamageFalloffThreshold(6f,  3.0f, false),   // 6-10 tiles:  -3.0/tile  (~25% lost at 10t)
        new DamageFalloffThreshold(10f, 4.5f, false),   // 10-15 tiles: -4.5/tile  (~65% lost at 15t)
        new DamageFalloffThreshold(15f, 6.0f, false),   // 15-20 tiles: -6.0/tile  (floor reached ~17t)
        new DamageFalloffThreshold(20f, 8.0f, false),   // 20+ tiles:   -8.0/tile  (hard floor zone)
    };

    /// <summary>
    /// Minimum fraction of the projectile's original damage that survives falloff.
    /// 0.15 = bullets always deal at least 15% of their base damage regardless of range.
    /// For a 56-damage bullet this means a minimum of ~8 damage at extreme range.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MinRemainingDamageModifier = 0.15f;

    /// <summary>
    /// Weapon-specific falloff multiplier applied to non-IgnoreModifiers bands.
    /// Default 1.0 = standard falloff. Values above 1 increase falloff (shorter effective range).
    /// Example: SMGs might use WeaponModifier=1.5 to fall off faster than rifles.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float WeaponModifier = 1;

    /// <summary>
    /// Coordinates from which the projectile was fired. Set by the weapon system on spawn.
    /// Used to calculate total distance travelled when the projectile hits.
    /// </summary>
    [DataField(readOnly: true), AutoNetworkedField]
    public EntityCoordinates? StartCoordinates;
}
