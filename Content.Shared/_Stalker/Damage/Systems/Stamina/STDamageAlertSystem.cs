using Content.Shared.Alert;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stalker.Damage.Systems;

public sealed class STDamageAlertSystem : EntitySystem
{
    [Dependency] private readonly AlertsSystem _alerts = default!;

    private static readonly ProtoId<AlertPrototype> AcidAlert = "StalkerAcid";
    private static readonly ProtoId<AlertPrototype> RadAlert = "StalkerRad";
    private static readonly ProtoId<AlertPrototype> PsyAlert = "StalkerPsy";

    public override void Initialize()
    {
        SubscribeLocalEvent<DamageableComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(Entity<DamageableComponent> ent, ref ComponentStartup args)
    {
        RefreshAlerts(ent.Owner, ent.Comp);
    }

    public void RefreshAlerts(EntityUid uid, DamageableComponent comp)
    {
        if (comp.TotalDamage <= FixedPoint2.Zero)
        {
            _alerts.ClearAlert(uid, AcidAlert);
            _alerts.ClearAlert(uid, RadAlert);
            _alerts.ClearAlert(uid, PsyAlert);
            return;
        }

        comp.Damage.DamageDict.TryGetValue("Caustic", out var acid);
        comp.Damage.DamageDict.TryGetValue("Radiation", out var rad);
        comp.Damage.DamageDict.TryGetValue("Psy", out var psy);

        var critThreshold = GetCriticalThreshold(uid);

        UpdateAlert(uid, AcidAlert, acid, critThreshold, 8f, 28f);
        UpdateAlert(uid, RadAlert, rad, critThreshold, 12f, 40f);
        UpdateAlert(uid, PsyAlert, psy, critThreshold, 10f, 35f);
    }

    private void UpdateAlert(
        EntityUid uid,
        ProtoId<AlertPrototype> alert,
        FixedPoint2 value,
        FixedPoint2? critThreshold,
        float fallbackYellow,
        float fallbackRed)
    {
        if (value <= FixedPoint2.Zero)
        {
            _alerts.ClearAlert(uid, alert);
            return;
        }

        FixedPoint2 yellowThreshold;
        FixedPoint2 redThreshold;

        if (critThreshold is { } crit && crit > FixedPoint2.Zero)
        {
            yellowThreshold = crit * 0.15f;
            redThreshold = crit * 0.50f;
        }
        else
        {
            yellowThreshold = FixedPoint2.New(fallbackYellow);
            redThreshold = FixedPoint2.New(fallbackRed);
        }

        if (value >= redThreshold)
        {
            _alerts.ShowAlert(uid, alert, 2);
            return;
        }

        if (value >= yellowThreshold)
        {
            _alerts.ShowAlert(uid, alert, 1);
            return;
        }

        _alerts.ClearAlert(uid, alert);
    }

    private FixedPoint2? GetCriticalThreshold(EntityUid uid)
    {
        if (!TryComp<MobThresholdsComponent>(uid, out var thresholds))
            return null;

        foreach (var (threshold, state) in thresholds.Thresholds)
        {
            if (state == MobState.Critical)
                return threshold;
        }

        return null;
    }
}
