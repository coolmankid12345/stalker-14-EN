using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Stalker_EN.Emission;


[AdminCommand(AdminFlags.Debug)]
public sealed class TestEmissionLightningCommand : LocalizedCommands
{
    [Dependency] private readonly IEntitySystemManager _entitySystemManager = default!;
    private EmissionLightningSystem _emissionLightningSystem = default!;

    public override string Command => "testemissionlightning";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } session ||
            session.AttachedEntity is not { } uid)
            return;

        _entitySystemManager.Resolve(ref _emissionLightningSystem);

        _emissionLightningSystem.Refresh();
        _emissionLightningSystem.TrySpawnLightningNearby(uid, 10f, "EmissionLightningEffect", 10f, 10);
    }
}
