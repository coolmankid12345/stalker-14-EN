using Content.Server.Stealth;
using Content.Shared._Stalker.ZoneAnomaly;
using Content.Shared._Stalker.ZoneAnomaly.Components;
using Content.Shared._Stalker.ZoneAnomaly.Effects.Components;
using Content.Shared.Stealth.Components;
using Robust.Shared.Timing;

namespace Content.Server._Stalker.ZoneAnomaly.Effects.Systems;

public sealed partial class ZoneAnomalyEffectStealthSystem : EntitySystem
{
    [Dependency] private readonly StealthSystem _stealth = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ZoneAnomalyEffectStealthComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ZoneAnomalyEffectStealthComponent, ZoneAnomalyChangedState>(OnChangeState);
    }

    private void OnStartup(Entity<ZoneAnomalyEffectStealthComponent> effect, ref ComponentStartup args)
    {
        if (!HasComp<StealthComponent>(effect.Owner))
            return;
        _stealth.SetVisibility(effect, effect.Comp.Idle);
    }

    private void OnChangeState(Entity<ZoneAnomalyEffectStealthComponent> effect, ref ZoneAnomalyChangedState args)
    {
        // Cancel any active fade when state changes
        effect.Comp.IsFading = false;

        switch (args.Current)
        {
            case ZoneAnomalyState.Idle:
                _stealth.SetVisibility(effect, effect.Comp.Idle);
                break;

            case ZoneAnomalyState.Activated:
                _stealth.SetVisibility(effect, effect.Comp.Activated);
                break;

            case ZoneAnomalyState.Charging:
                // Start fade animation instead of instant visibility change
                effect.Comp.IsFading = true;
                effect.Comp.FadeStartVisibility = effect.Comp.Activated;
                effect.Comp.FadeStartTime = _timing.CurTime;
                break;
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ZoneAnomalyEffectStealthComponent, StealthComponent>();
        while (query.MoveNext(out var uid, out var effect, out _))
        {
            if (!effect.IsFading)
                continue;

            var elapsed = (float)(_timing.CurTime - effect.FadeStartTime).TotalSeconds;
            var progress = Math.Clamp(elapsed / effect.ChargingFadeDuration, 0f, 1f);

            var visibility = effect.FadeStartVisibility +
                (effect.Charging - effect.FadeStartVisibility) * progress;

            _stealth.SetVisibility(uid, visibility);

            if (progress >= 1f)
                effect.IsFading = false;
        }
    }
}
