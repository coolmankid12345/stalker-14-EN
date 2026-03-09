using Robust.Shared.Map;

namespace Content.Shared._RD.Watcher;

public sealed partial class RDWatcherSystem
{
    private Entity<RDWatcherComponent> CreateWatcher(HashSet<EntityUid> entities)
    {
        var instance = Spawn(null, MapCoordinates.Nullspace);
        var component = EnsureComp<RDWatcherComponent>(instance);

        component.Entities = entities;
        DirtyField(instance, component, nameof(RDWatcherComponent.Entities));

        return (instance, component);
    }

    private void WatcherAdd(Entity<RDWatcherComponent> entity, HashSet<EntityUid> targets)
    {
        entity.Comp.Entities.UnionWith(targets);
        DirtyField(entity, entity.Comp, nameof(RDWatcherComponent.Entities));
    }

    private void WatcherAdd(Entity<RDWatcherComponent> entity, EntityUid targetUid)
    {
        entity.Comp.Entities.Add(targetUid);
        DirtyField(entity, entity.Comp, nameof(RDWatcherComponent.Entities));
    }
}
