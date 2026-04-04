using System;
using System.Threading.Tasks;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;

namespace Content.Server._Stalker.PersistentCrafting;

[AdminCommand(AdminFlags.Host)]
public sealed class PersistentCraftResetOfflineCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    public string Command => "st_pcraft_reset_offline";
    public string Description => "Resets persistent crafting progress for a character by userId and characterName.";
    public string Help => "st_pcraft_reset_offline <userId-guid> <characterName>";

    public async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        try
        {
            await ExecuteAsync(shell, args);
        }
        catch (Exception ex)
        {
            shell.WriteError($"Persistent craft offline reset failed unexpectedly: {ex.Message}");
        }
    }

    private async Task ExecuteAsync(IConsoleShell shell, string[] args)
    {
        try
        {
            if (args.Length < 2)
            {
                shell.WriteError($"Usage: {Help}");
                return;
            }

            if (!Guid.TryParse(args[0], out var userId))
            {
                shell.WriteError("Invalid userId guid.");
                return;
            }

            var characterName = string.Join(' ', args, 1, args.Length - 1).Trim();
            if (string.IsNullOrWhiteSpace(characterName))
            {
                shell.WriteError("Character name cannot be empty.");
                return;
            }

            var system = _entityManager.System<PersistentCraftingSystem>();
            var emptyJson = system.SerializeEmptyProfile();

            await system.WriteProfileJsonAsync(userId, characterName, emptyJson);
            shell.WriteLine($"Persistent craft profile reset for '{characterName}' ({userId}).");
        }
        catch (Exception ex)
        {
            shell.WriteError($"Persistent craft offline reset failed: {ex.Message}");
        }
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return CompletionResult.Empty;
    }
}
