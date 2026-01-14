using Content.Shared._Stalker.Anomaly.Triggers.Events;
using Content.Shared._Stalker.ZoneAnomaly;
using Robust.Server.GameObjects;

namespace Content.Server._Stalker.Anomaly.Visuals;

public sealed class STAnomalyVisualStateSystem : EntitySystem
{
    [Dependency] private readonly AppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<STAnomalyVisualStateComponent, STAnomalyChangedStateEvent>(OnStateChanged);
    }

    private void OnStateChanged(Entity<STAnomalyVisualStateComponent> entity, ref STAnomalyChangedStateEvent args)
    {
        _appearance.SetData(entity, ZoneAnomalyVisuals.Layer, args.State);
    }
}
