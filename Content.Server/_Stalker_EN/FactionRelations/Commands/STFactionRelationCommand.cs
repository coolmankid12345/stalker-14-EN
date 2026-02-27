using Content.Server.Administration;
using Content.Shared._Stalker_EN.FactionRelations;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.IoC;

namespace Content.Server._Stalker_EN.FactionRelations.Commands;

/// <summary>
/// Admin command to view and modify faction relations and proposals.
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed class STFactionRelationCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    public string Command => "st_factionrelation";

    public string Description => "Admin command to view and modify faction relations.";

    public string Help => "Usage:\n" +
                          "st_factionrelation set <factionA> <factionB> <alliance|neutral|hostile|war> [--broadcast]\n" +
                          "st_factionrelation get <factionA> <factionB>\n" +
                          "st_factionrelation reset\n" +
                          "st_factionrelation proposals list\n" +
                          "st_factionrelation proposals clear\n" +
                          "st_factionrelation proposals delete <initiating> <target>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 1)
        {
            shell.WriteLine(Help);
            return;
        }

        var system = _entityManager.System<STFactionRelationsCartridgeSystem>();

        switch (args[0])
        {
            case "set":
                HandleSet(shell, args, system);
                break;
            case "get":
                HandleGet(shell, args, system);
                break;
            case "reset":
                HandleReset(shell, system);
                break;
            case "proposals":
                HandleProposals(shell, args, system);
                break;
            default:
                shell.WriteLine(Help);
                break;
        }
    }

    private void HandleSet(IConsoleShell shell, string[] args, STFactionRelationsCartridgeSystem system)
    {
        if (args.Length < 4)
        {
            shell.WriteLine("Usage: st_factionrelation set <factionA> <factionB> <alliance|neutral|hostile|war> [--broadcast]");
            return;
        }

        var factionA = args[1];
        var factionB = args[2];
        var relationStr = args[3].ToLowerInvariant();

        var broadcast = false;
        for (var i = 4; i < args.Length; i++)
        {
            if (args[i] == "--broadcast")
                broadcast = true;
        }

        var factions = system.GetFactionIds();
        if (factions == null)
        {
            shell.WriteError("Failed to load faction defaults prototype.");
            return;
        }

        if (!factions.Contains(factionA))
        {
            shell.WriteError($"Unknown faction: '{factionA}'. Valid factions: {string.Join(", ", factions)}");
            return;
        }

        if (!factions.Contains(factionB))
        {
            shell.WriteError($"Unknown faction: '{factionB}'. Valid factions: {string.Join(", ", factions)}");
            return;
        }

        if (factionA == factionB)
        {
            shell.WriteError("Cannot set relation between a faction and itself.");
            return;
        }

        if (!TryParseRelation(relationStr, out var relation))
        {
            shell.WriteError($"Unknown relation type: '{relationStr}'. Valid types: alliance, neutral, hostile, war");
            return;
        }

        system.SetRelation(factionA, factionB, relation, broadcast: broadcast);
        shell.WriteLine($"Set relation {factionA} <-> {factionB} to {relation}{(broadcast ? " (announced)" : " (silent)")}");
    }

    private void HandleGet(IConsoleShell shell, string[] args, STFactionRelationsCartridgeSystem system)
    {
        if (args.Length != 3)
        {
            shell.WriteLine("Usage: st_factionrelation get <factionA> <factionB>");
            return;
        }

        var factionA = args[1];
        var factionB = args[2];

        var relation = system.GetRelation(factionA, factionB);
        shell.WriteLine($"{factionA} <-> {factionB}: {relation}");
    }

    private static void HandleReset(IConsoleShell shell, STFactionRelationsCartridgeSystem system)
    {
        system.ResetAllRelations();
        shell.WriteLine("All faction relation overrides and proposals cleared. Reverted to YAML defaults.");
    }

    private void HandleProposals(IConsoleShell shell, string[] args, STFactionRelationsCartridgeSystem system)
    {
        if (args.Length < 2)
        {
            shell.WriteLine("Usage: st_factionrelation proposals <list|clear|delete>");
            return;
        }

        switch (args[1])
        {
            case "list":
            {
                var factions = system.GetFactionIds();
                if (factions == null)
                {
                    shell.WriteError("Failed to load faction defaults prototype.");
                    return;
                }

                var anyFound = false;
                foreach (var faction in factions)
                {
                    var (incoming, outgoing) = system.GetProposalsForFaction(faction);
                    foreach (var p in outgoing)
                    {
                        shell.WriteLine($"  {p.InitiatingFaction} -> {p.TargetFaction}: proposes {p.ProposedRelation}" +
                                        (p.CustomMessage != null ? $" (\"{p.CustomMessage}\")" : ""));
                        anyFound = true;
                    }
                }

                if (!anyFound)
                    shell.WriteLine("No pending proposals.");
                break;
            }
            case "clear":
                system.ResetAllRelations(); // This also clears proposals
                shell.WriteLine("All proposals cleared (along with relation overrides).");
                break;
            case "delete":
                if (args.Length < 4)
                {
                    shell.WriteLine("Usage: st_factionrelation proposals delete <initiating> <target>");
                    return;
                }

                system.CancelProposal(args[2], args[3]);
                shell.WriteLine($"Deleted proposal from {args[2]} to {args[3]} (if it existed).");
                break;
            default:
                shell.WriteLine("Usage: st_factionrelation proposals <list|clear|delete>");
                break;
        }
    }

    private static bool TryParseRelation(string str, out STFactionRelationType relation)
    {
        relation = str switch
        {
            "alliance" => STFactionRelationType.Alliance,
            "neutral" => STFactionRelationType.Neutral,
            "hostile" => STFactionRelationType.Hostile,
            "war" => STFactionRelationType.War,
            _ => STFactionRelationType.Neutral,
        };

        return str is "alliance" or "neutral" or "hostile" or "war";
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            return CompletionResult.FromOptions(new[] { "set", "get", "reset", "proposals" });
        }

        if (args[0] is "set" or "get")
        {
            if (args.Length is 2 or 3)
            {
                var system = _entityManager.System<STFactionRelationsCartridgeSystem>();
                var factions = system.GetFactionIds();
                if (factions != null)
                    return CompletionResult.FromOptions(factions);
            }

            if (args[0] == "set" && args.Length == 4)
            {
                return CompletionResult.FromOptions(new[] { "alliance", "neutral", "hostile", "war" });
            }

            if (args[0] == "set" && args.Length == 5)
            {
                return CompletionResult.FromOptions(new[] { "--broadcast" });
            }
        }

        if (args[0] == "proposals" && args.Length == 2)
        {
            return CompletionResult.FromOptions(new[] { "list", "clear", "delete" });
        }

        if (args[0] == "proposals" && args[1] == "delete" && args.Length is 3 or 4)
        {
            var system = _entityManager.System<STFactionRelationsCartridgeSystem>();
            var factions = system.GetFactionIds();
            if (factions != null)
                return CompletionResult.FromOptions(factions);
        }

        return CompletionResult.Empty;
    }
}
