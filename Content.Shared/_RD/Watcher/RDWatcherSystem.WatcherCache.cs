namespace Content.Shared._RD.Watcher;

public sealed partial class RDWatcherSystem
{
    private readonly List<Entity<RDWatcherComponent>> _watcherCache = new(30);

    private void InitializeWatcherCache()
    {
        SubscribeLocalEvent<RDWatcherComponent, ComponentStartup>(OnWatcherStartup);
        SubscribeLocalEvent<RDWatcherComponent, ComponentRemove>(OnWatcherRemove);
        // stalker change EN: clear watcher targets as soon as tracked mobs start shutting down.
        SubscribeLocalEvent<RDWatcherTargetComponent, ComponentRemove>(OnWatcherTargetRemove);
        SubscribeLocalEvent<RDWatcherTargetComponent, EntityTerminatingEvent>(OnWatcherTargetTerminating);
    }

    private void OnWatcherStartup(Entity<RDWatcherComponent> entity, ref ComponentStartup args) => _watcherCache.Add(entity);
    private void OnWatcherRemove(Entity<RDWatcherComponent> entity, ref ComponentRemove args) => _watcherCache.Remove(entity);

    private void OnWatcherTargetRemove(Entity<RDWatcherTargetComponent> entity, ref ComponentRemove args) =>
        RemoveTargetFromWatchers(entity);

    private void OnWatcherTargetTerminating(Entity<RDWatcherTargetComponent> entity, ref EntityTerminatingEvent args) =>
        RemoveTargetFromWatchers(entity);

    private void RemoveTargetFromWatchers(EntityUid target)
    {
        if (_net.IsClient)
            return;

        for (var i = _watcherCache.Count - 1; i >= 0; i--)
        {
            var watcher = _watcherCache[i];
            if (!watcher.Comp.Entities.Remove(target))
                continue;

            // stalker change EN: dirty the whole watcher after pruning stale tracked entities.
            Dirty(watcher, watcher.Comp);

            if (watcher.Comp.Entities.Count == 0)
                QueueDel(watcher);
        }
    }

    private void PruneWatcherTargets(HashSet<EntityUid> liveTargets)
    {
        if (_net.IsClient)
            return;

        for (var i = _watcherCache.Count - 1; i >= 0; i--)
        {
            var watcher = _watcherCache[i];
            var removed = watcher.Comp.Entities.RemoveWhere(uid => !liveTargets.Contains(uid));
            if (removed == 0)
                continue;

            // stalker change EN: sync cleanup when grouping discovers dead or missing targets.
            Dirty(watcher, watcher.Comp);

            if (watcher.Comp.Entities.Count == 0)
                QueueDel(watcher);
        }
    }
}
