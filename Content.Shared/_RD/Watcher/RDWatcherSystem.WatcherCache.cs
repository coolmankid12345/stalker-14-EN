namespace Content.Shared._RD.Watcher;

public sealed partial class RDWatcherSystem
{
    private readonly List<Entity<RDWatcherComponent>> _watcherCache = new(30);

    private void InitializeWatcherCache()
    {
        SubscribeLocalEvent<RDWatcherComponent, ComponentStartup>(OnWatcherStartup);
        SubscribeLocalEvent<RDWatcherComponent, ComponentRemove>(OnWatcherRemove);
    }

    private void OnWatcherStartup(Entity<RDWatcherComponent> entity, ref ComponentStartup args) => _watcherCache.Add(entity);
    private void OnWatcherRemove(Entity<RDWatcherComponent> entity, ref ComponentRemove args) => _watcherCache.Remove(entity);
}
