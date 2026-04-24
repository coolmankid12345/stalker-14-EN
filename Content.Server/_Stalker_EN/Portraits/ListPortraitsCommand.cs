using System;
using System.Linq;
using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared._Stalker_EN.Portraits;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;

namespace Content.Server._Stalker_EN.Portraits;

/// <summary>
/// Admin command for listing portrait prototypes and inspecting entity portraits.
/// Usage:
///   listportraits                  — List all portrait prototypes
///   listportraits Portrait_ID      — Show details of a specific prototype (texture list)
///   listportraits [EntityUid]      — Check portrait on an entity
/// </summary>
[AdminCommand(AdminFlags.Debug)]
public sealed class ListPortraitsCommand : IConsoleCommand
{
    public string Command => "listportraits";
    public string Description => "Lists portrait prototypes and checks entity portraits.";
    public string Help => "listportraits [id]";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var protoMan = IoCManager.Resolve<IPrototypeManager>();
        var entMan = IoCManager.Resolve<IEntityManager>();
        var portraits = protoMan.EnumeratePrototypes<CharacterPortraitPrototype>().ToList();

        // Entity inspection
        if (args.Length == 1 && EntityUid.TryParse(args[0], out var uid))
        {
            if (!entMan.EntityExists(uid))
            {
                shell.WriteError($"Entity {uid} does not exist.");
                return;
            }

            if (!entMan.TryGetComponent<CharacterPortraitComponent>(uid, out var comp))
            {
                shell.WriteLine($"Entity {uid} has no CharacterPortraitComponent.");
                return;
            }

            shell.WriteLine($"--- Entity {uid} ---");
            shell.WriteLine($"TexturePath: {comp.PortraitTexturePath ?? "(empty)"}");
            return;
        }

        // Prototype details
        if (args.Length == 1)
        {
            var targetId = args[0];
            var proto = portraits.FirstOrDefault(p => p.ID == targetId);
            if (proto == null)
            {
                shell.WriteError($"Portrait '{targetId}' not found.");
                return;
            }

            shell.WriteLine($"--- Prototype {proto.ID} ---");
            shell.WriteLine($"Name: {proto.Name}");
            shell.WriteLine($"JobId: {proto.JobId ?? "(any)"}");
            shell.WriteLine($"BandId: {proto.BandId}");
            shell.WriteLine($"Texture count: {proto.Textures.Count}");
            for (int i = 0; i < proto.Textures.Count; i++)
            {
                shell.WriteLine($"  [{i}] {proto.Textures[i]}");
            }
            return;
        }

        // List all
        shell.WriteLine("--- Portrait list ---");
        foreach (var p in portraits)
        {
            shell.WriteLine($"ID: {p.ID,-35} | Job: {p.JobId ?? "All",-20} | Band: {p.BandId,-15} | Textures: {p.Textures.Count}");
        }
        shell.WriteLine("\nUse 'listportraits <ID>' to see texture paths.");
        shell.WriteLine("Use 'listportraits <UID>' to see the selected portrait on an entity.");
    }
}
