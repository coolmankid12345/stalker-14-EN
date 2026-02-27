using Content.Shared._Stalker.ZoneAnomaly;
using Content.Shared._Stalker.ZoneAnomaly.Components;
using Content.Shared._Stalker.ZoneAnomaly.Effects.Components;
using Content.Shared._Stalker.ZoneAnomaly.Effects.Systems;
using Robust.Shared.Random;

namespace Content.Server._Stalker.ZoneAnomaly.Effects.Systems;

public sealed class ZoneAnomalyEffectRandomTeleportSystem : SharedZoneAnomalyEffectRandomTeleportSystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ZoneAnomalySystem _anomaly = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ZoneAnomalyEffectRandomTeleportComponent, ZoneAnomalyActivateEvent>(OnActivate);
    }

    // stalker-en-changes-start: avoid ToList() allocation on every activation
    private void OnActivate(Entity<ZoneAnomalyEffectRandomTeleportComponent> effect, ref ZoneAnomalyActivateEvent args)
    {
        // First pass: count valid targets (excluding self)
        var count = 0;
        var query = EntityQueryEnumerator<ZoneAnomalyEffectRandomTeleportComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (uid != effect.Owner)
                count++;
        }

        foreach (var trigger in args.Triggers)
        {
            if (count == 0)
            {
                TeleportEntity(trigger, Transform(effect).Coordinates);
                return;
            }

            // Second pass: pick a random target by index
            var targetIndex = _random.Next(count);
            var i = 0;
            var query2 = EntityQueryEnumerator<ZoneAnomalyEffectRandomTeleportComponent>();
            while (query2.MoveNext(out var uid, out _))
            {
                if (uid == effect.Owner)
                    continue;

                if (i == targetIndex)
                {
                    var destination = Transform(uid).Coordinates;

                    if (TryComp<ZoneAnomalyComponent>(uid, out var comp))
                        _anomaly.TryRecharge((uid, comp));

                    TeleportEntity(trigger, destination);
                    break;
                }

                i++;
            }
        }
    }
    // stalker-en-changes-end
}
