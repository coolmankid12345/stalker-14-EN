using Content.Shared._Stalker.ZoneAnomaly.Components;
using Content.Shared._Stalker.ZoneAnomaly.Effects.Components;
using Content.Shared.Whitelist;
using Robust.Shared.Spawners;

namespace Content.Server._Stalker.ZoneAnomaly.Effects.Systems;

public sealed class ZoneAnomalyEffectDestroySystem : EntitySystem
{
    [Dependency] private readonly EntityWhitelistSystem _whitelistSystem = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<ZoneAnomalyEffectDestroyComponent, ZoneAnomalyActivateEvent>(OnActivate);
    }

    private void OnActivate(Entity<ZoneAnomalyEffectDestroyComponent> effect, ref ZoneAnomalyActivateEvent args)
    {
        foreach (var trigger in args.Triggers)
        {
            if(_whitelistSystem.IsWhitelistFail(effect.Comp.Whitelist, trigger))
                continue;

            if (HasComp<TimedDespawnComponent>(trigger))
                continue;

            var comp = EnsureComp<TimedDespawnComponent>(trigger);
            comp.Lifetime = effect.Comp.Lifetime;
        }
    }
}
