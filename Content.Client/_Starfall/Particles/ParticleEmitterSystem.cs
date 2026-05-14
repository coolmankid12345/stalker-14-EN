using Content.Shared._Starfall.Particles;
using Robust.Shared.Map;

namespace Content.Client._Starfall.Particles;

/// <summary>
/// Spawns a particle effect on this client when an entity with
/// <see cref="ParticleEmitterComponent"/> enters the local view (MapInitEvent).
/// </summary>
public sealed class ParticleEmitterSystem : EntitySystem
{
    [Dependency] private readonly ParticleSystem _particles = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ParticleEmitterComponent, AfterAutoHandleStateEvent>(OnAfterAutoHandleState); // EN start
        SubscribeLocalEvent<ParticleEmitterComponent, ComponentInit>(OnCompInit);
    }

    private void OnAfterAutoHandleState(Entity<ParticleEmitterComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        SpawnEffects(ent);
    }

    private void OnCompInit(Entity<ParticleEmitterComponent> ent, ref ComponentInit args)
    {
        SpawnEffects(ent);
    }

    private void SpawnEffects(Entity<ParticleEmitterComponent> ent)
    {
        var coords = _transform.GetMapCoordinates(ent.Owner);
        foreach (var effect in ent.Comp.Effect)
        {
            var emitter = _particles.SpawnEffect(effect, coords, ent.Owner, ent.Comp.ColorOverride);
            if (emitter == null)
                return;

            if (ent.Comp.Intensity != 1f)
                emitter.Intensity = ent.Comp.Intensity;
        } // EN end
    }
}

