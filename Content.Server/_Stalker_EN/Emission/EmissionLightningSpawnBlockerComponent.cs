namespace Content.Server._Stalker_EN.Emission;

/// <summary>
///     Blocks lightning from spawning within certain radius of this entity.
/// </summary>
[RegisterComponent]
public sealed partial class EmissionLightningSpawnBlockerComponent : Component
{
    [DataField, ViewVariables]
    public int Radius = 5;
}
