using Content.Shared._Stalker.ZoneAnomaly.Components;
using Content.Shared._Stalker.ZoneAnomaly.Effects.Components;
using Robust.Shared.Random;

namespace Content.Server._Stalker.ZoneAnomaly.Effects.Systems;

public sealed class ZoneAnomalyEffectSpawnSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ZoneAnomalyEffectSpawnComponent, ZoneAnomalyActivateEvent>(OnActivate);
    }

    private void OnActivate(Entity<ZoneAnomalyEffectSpawnComponent> effect, ref ZoneAnomalyActivateEvent args)
    {
        var content = _random.Pick(effect.Comp.Entry);
        var position = Transform(effect).Coordinates;

        for (var i = 0; i < content.Amount; i++)
        {
            var offset = _random.NextVector2(-effect.Comp.Offset, effect.Comp.Offset);
            Spawn(content.PrototypeId, position.Offset(offset));
        }
    }
}
