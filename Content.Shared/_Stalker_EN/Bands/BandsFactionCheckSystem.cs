using Content.Shared._Stalker.Bands;
using Content.Shared._Stalker.Bands.Components;
using Content.Shared._Stalker_EN.FactionRelations;
using Content.Shared.Popups;
using Content.Shared.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stalker_EN.Bands;

/// <summary>
/// Prevents players from opening a faction-restricted Igor (BandsManaging UI) that belongs
/// to a different faction. Runs on both client and server to prevent UI flash from misprediction.
/// <para/>
/// This is a standalone system rather than part of <c>SharedBandsSystem</c> because
/// <c>SharedBandsSystem</c> is upstream Stalker code in <c>_Stalker/</c>. Adding EN-specific
/// faction checking there would create merge conflicts on upstream updates.
/// </summary>
public sealed class BandsFactionCheckSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedSTFactionResolutionSystem _factionResolution = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BandsManagingComponent, ActivatableUIOpenAttemptEvent>(OnActivatableUIOpenAttempt);
    }

    private void OnActivatableUIOpenAttempt(Entity<BandsManagingComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        // If this Igor has no faction set, allow anyone (generic Igor)
        if (string.IsNullOrEmpty(ent.Comp.Faction))
            return;

        if (!TryComp<BandsComponent>(args.User, out var bandsComp) ||
            string.IsNullOrEmpty(bandsComp.BandProto))
        {
            if (!args.Silent)
                _popup.PopupClient(Loc.GetString("st-igor-faction-denied"), args.User);

            args.Cancel();
            return;
        }

        if (!_protoManager.TryIndex<STBandPrototype>(bandsComp.BandProto, out var bandProto))
        {
            if (!args.Silent)
                _popup.PopupClient(Loc.GetString("st-igor-faction-denied"), args.User);

            args.Cancel();
            return;
        }

        var playerFaction = _factionResolution.GetBandFactionName(bandProto.Name);

        if (!string.IsNullOrEmpty(playerFaction))
            playerFaction = _factionResolution.ResolvePrimary(playerFaction);

        var igorFaction = _factionResolution.ResolvePrimary(ent.Comp.Faction);

        if (playerFaction != igorFaction)
        {
            if (!args.Silent)
                _popup.PopupClient(Loc.GetString("st-igor-faction-denied"), args.User);

            args.Cancel();
        }
    }
}
