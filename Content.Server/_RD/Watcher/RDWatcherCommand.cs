using Content.Server.Administration;
using Content.Shared._RD.Watcher;
using Content.Shared.Administration;
using Robust.Shared.Toolshed;

namespace Content.Server._RD.Watcher;

[ToolshedCommand, AdminCommand(AdminFlags.Mapping)]
public sealed class RDWatcherCommand : ToolshedCommand
{
    [CommandImplementation("list")]
    public void List([CommandInvocationContext] IInvocationContext ctx)
    {
        var query = EntityManager.AllEntityQueryEnumerator<RDWatcherComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var watcher, out _))
        {
            ctx.WriteLine($"Watcher: {EntityManager.ToPrettyString(uid)}");
            ctx.WriteLine($"Entities ({watcher.Entities.Count}):");

            foreach (var entity in watcher.Entities)
            {
                ctx.WriteLine($"  - {EntityManager.ToPrettyString(entity)}");
            }

            ctx.WriteLine("");
        }
    }
}
