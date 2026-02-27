using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared._Stalker.ZoneAnomaly;
using Content.Shared._Stalker.ZoneAnomaly.Components;
using Content.Shared._Stalker.ZoneAnomaly.Effects.Components;
using Content.Shared.Power.Components;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server._Stalker.ZoneAnomaly.Effects.Systems;

public sealed class ZoneAnomalyEffectDischargeSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly BatterySystem _batterySystem = default!;

    // stalker-en-changes: reusable list to avoid per-call heap allocations
    private readonly List<(EntityUid Entity, BatteryComponent Battery)> _batteryBuffer = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<ZoneAnomalyEffectDischargeComponent, ZoneAnomalyActivateEvent>(OnActivate);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ZoneAnomalyComponent, ZoneAnomalyEffectDischargeComponent>();
        while (query.MoveNext(out var uid, out var anomaly, out var effect))
        {
            if (anomaly.State != ZoneAnomalyState.Activated)
                continue;

            if (effect.DischargeUpdateTime > _timing.CurTime)
                continue;

            foreach (var target in anomaly.InAnomaly)
            {
                Discharge((uid, effect), target);
            }
            effect.DischargeUpdateTime = _timing.CurTime + effect.DischargeUpdateDelay;
        }
    }

    private void OnActivate(Entity<ZoneAnomalyEffectDischargeComponent> effect, ref ZoneAnomalyActivateEvent args)
    {
        if (!TryComp<ZoneAnomalyComponent>(effect, out var anomaly))
            return;

        effect.Comp.DischargeUpdateTime = TimeSpan.Zero;
        foreach (var trigger in anomaly.InAnomaly)
        {
            Discharge(effect, trigger);
        }
    }

    // stalker-en-changes-start: avoid per-call list allocations in Discharge/GetBatteries
    private void Discharge(Entity<ZoneAnomalyEffectDischargeComponent> effect, EntityUid target)
    {
        if (!TryComp<ContainerManagerComponent>(target, out var container))
            return;

        _batteryBuffer.Clear();
        GetBatteries(target, _batteryBuffer, container);

        foreach (var (item, battery) in _batteryBuffer)
        {
            _batterySystem.UseCharge((item, battery), battery.CurrentCharge);
        }
    }

    private void GetBatteries(EntityUid uid, List<(EntityUid Entity, BatteryComponent Battery)> result, ContainerManagerComponent? managerComponent = null)
    {
        if (!Resolve(uid, ref managerComponent))
            return;

        foreach (var container in managerComponent.Containers.Values)
        {
            foreach (var element in container.ContainedEntities)
            {
                if (TryComp<BatteryComponent>(element, out var battery))
                {
                    result.Add((element, battery));
                }
                if (TryComp<ContainerManagerComponent>(element, out var manager))
                {
                    GetBatteries(element, result, manager);
                }
            }
        }
    }
    // stalker-en-changes-end
}
