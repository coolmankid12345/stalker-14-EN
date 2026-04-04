using System;
using System.Threading.Tasks;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;

namespace Content.Server._Stalker.PersistentCrafting;

[AdminCommand(AdminFlags.Host)]
public sealed class PersistentCraftResetCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    public string Command => "st_pcraft_reset";
    public string Description => "Resets persistent crafting progress for an in-game player (by account username).";
    public string Help => "st_pcraft_reset <username>";

    public async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        try
        {
            await ExecuteAsync(shell, args);
        }
        catch (Exception ex)
        {
            shell.WriteError($"Persistent craft reset failed unexpectedly: {ex.Message}");
        }
    }

    private async Task ExecuteAsync(IConsoleShell shell, string[] args)
    {
        try
        {
            if (args.Length != 1)
            {
                shell.WriteError($"Usage: {Help}");
                return;
            }

            if (!_playerManager.TryGetSessionByUsername(args[0], out var session))
            {
                shell.WriteError($"Player '{args[0]}' was not found.");
                return;
            }

            if (session.AttachedEntity is not { Valid: true } attached)
            {
                shell.WriteError($"Player '{args[0]}' is not in game.");
                return;
            }

            var system = _entityManager.System<PersistentCraftingSystem>();
            if (!await system.ResetProfileAsync(attached))
            {
                shell.WriteError($"Unable to reset persistent craft profile for '{args[0]}'.");
                return;
            }

            shell.WriteLine($"Persistent craft profile reset for '{args[0]}'.");
        }
        catch (Exception ex)
        {
            shell.WriteError($"Persistent craft reset failed: {ex.Message}");
        }
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length == 1
            ? CompletionResult.FromHintOptions(CompletionHelper.SessionNames(players: _playerManager), "<username>")
            : CompletionResult.Empty;
    }
}
