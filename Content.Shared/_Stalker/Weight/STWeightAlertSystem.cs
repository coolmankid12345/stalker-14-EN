using Content.Shared.Alert;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stalker.Weight;

public sealed class STWeightAlertSystem : EntitySystem
{
    [Dependency] private readonly AlertsSystem _alerts = default!;

    private static readonly ProtoId<AlertPrototype> WeightAlert = "StalkerWeight";

    public override void Initialize()
    {
        SubscribeLocalEvent<STWeightComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<STWeightComponent, STWeightChangedEvent>(OnWeightChanged);
    }

    private void OnStartup(Entity<STWeightComponent> ent, ref ComponentStartup args)
    {
        RefreshAlert(ent.Owner, ent.Comp.Total, ent.Comp.TotalMaximum);
    }

    private void OnWeightChanged(Entity<STWeightComponent> ent, ref STWeightChangedEvent args)
    {
        RefreshAlert(ent.Owner, args.Total, args.TotalMaximum);
    }

    private void RefreshAlert(EntityUid uid, float total, float max)
    {
        if (max <= 0f)
        {
            _alerts.ClearAlert(uid, WeightAlert);
            return;
        }

        var percent = total / max;

        if (percent >= 0.75f)
        {
            _alerts.ShowAlert(uid, WeightAlert, 2);
            return;
        }

        if (percent >= 0.5f)
        {
            _alerts.ShowAlert(uid, WeightAlert, 1);
            return;
        }

        _alerts.ClearAlert(uid, WeightAlert);
    }
}