using Content.Shared.Storage; // EN

namespace Content.Shared._Stalker.ZoneAnomaly.Effects.Components;

[RegisterComponent]
public sealed partial class ZoneAnomalyEffectRandomTeleportComponent : Component
{
    // EN start
    /// <summary>
    /// An entity spawned at the output of the teleport along side the teleported item. Used for effects.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public List<EntitySpawnEntry> OutputSpawnEntity = new(); // EN end
}
