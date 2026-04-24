using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Stalker_EN.CrashRecovery;

[AdminCommand(AdminFlags.Host)]
public sealed class CrashRecoverySnapshotCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entity = default!;

    public string Command => "crash_recovery_snapshot";
    public string Description => "Manually triggers a crash recovery snapshot for all online players.";
    public string Help => $"Usage: {Command}";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var system = _entity.System<CrashRecoverySystem>();
        system.ForceSnapshot();
        shell.WriteLine("Crash recovery snapshot triggered for all online players.");
    }
}
