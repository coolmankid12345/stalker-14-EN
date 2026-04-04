using Content.Shared.Projectiles;
using Content.Shared._Stalker.Weapon.Projectile;
using Robust.Shared.GameStates;
using Robust.Shared.Map;

namespace Content.Shared._RMC14.Weapons.Ranged.Prediction;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedGunPredictionSystem), typeof(SharedProjectileSystem), typeof(STProjectileSystem))]
public sealed partial class PredictedProjectileHitComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityCoordinates Origin;

    [DataField, AutoNetworkedField]
    public float Distance;
}
