using System;
using System.Threading.Tasks;
using Content.Server.Administration;
using Content.Server.Database;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Stalker.PersistentCrafting;

[AdminCommand(AdminFlags.Host)]
public sealed class PersistentCraftResetAllCommand : IConsoleCommand
{
    [Dependency] private readonly IServerDbManager _db = default!;

    public string Command => "st_pcraft_reset_all";
    public string Description => "Deletes ALL persistent crafting profiles for every player. Irreversible.";
    public string Help => "st_pcraft_reset_all confirm";

    public async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        try
        {
            await ExecuteAsync(shell, args);
        }
        catch (Exception ex)
        {
            shell.WriteError($"Persistent craft reset-all failed unexpectedly: {ex.Message}");
        }
    }

    private async Task ExecuteAsync(IConsoleShell shell, string[] args)
    {
        try
        {
            if (args.Length != 1 || !string.Equals(args[0], "confirm", StringComparison.OrdinalIgnoreCase))
            {
                shell.WriteError($"This will delete ALL persistent craft profiles. To confirm: {Help}");
                return;
            }

            await _db.DeleteAllStalkerPersistentCraftProfilesAsync();
            shell.WriteLine("All persistent craft profiles have been deleted.");
        }
        catch (Exception ex)
        {
            shell.WriteError($"Persistent craft reset-all failed: {ex.Message}");
        }
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length == 1
            ? CompletionResult.FromHint("confirm")
            : CompletionResult.Empty;
    }
}
