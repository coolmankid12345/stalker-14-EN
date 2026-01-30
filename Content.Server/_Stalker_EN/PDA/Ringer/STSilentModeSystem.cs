using Content.Server.PDA;
using Content.Server.PDA.Ringer;
using Content.Shared._Stalker_EN.PDA.Ringer;
using Content.Shared.PDA;
using Content.Shared.PDA.Ringer;
using Content.Shared.Popups;

namespace Content.Server._Stalker_EN.PDA.Ringer;

/// <summary>
/// Handles the silent mode feature for PDA ringers.
/// Automatically adds STSilentModeComponent to all entities with RingerComponent
/// and handles toggle messages from the UI.
/// </summary>
public sealed class STSilentModeSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly PdaSystem _pda = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RingerComponent, ComponentInit>(OnRingerInit);
        SubscribeLocalEvent<PdaComponent, STPdaToggleSilentModeMessage>(OnToggleSilentMode);
    }

    private void OnRingerInit(EntityUid uid, RingerComponent component, ComponentInit args)
    {
        EnsureComp<STSilentModeComponent>(uid);
    }

    private void OnToggleSilentMode(EntityUid uid, PdaComponent component, STPdaToggleSilentModeMessage args)
    {
        if (!PdaUiKey.Key.Equals(args.UiKey))
            return;

        if (!TryComp<STSilentModeComponent>(uid, out var silentMode))
            return;

        silentMode.Enabled = !silentMode.Enabled;
        Dirty(uid, silentMode);

        var message = silentMode.Enabled
            ? Loc.GetString("comp-pda-ui-silent-mode-enabled")
            : Loc.GetString("comp-pda-ui-silent-mode-disabled");
        _popup.PopupEntity(message, uid, args.Actor, PopupType.Small);

        _pda.UpdatePdaUi(uid);
    }
}
