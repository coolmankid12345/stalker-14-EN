using Content.Server.Radio.Components;
using Content.Server.Radio.EntitySystems;
using Content.Shared._Stalker.RadioStalker;
using Content.Shared._Stalker.RadioStalker.Components;
using Content.Shared._Stalker_EN.Radio;
using Content.Shared.Actions;
using Content.Shared.Inventory.Events;
using Robust.Server.GameObjects;

namespace Content.Server._Stalker_EN.Radio;

/// <summary>
/// Server-side system for stalker radio headsets that handles action events
/// and synchronizes action button states with the UI panel.
/// </summary>
public sealed class STRadioHeadsetSystem : SharedSTRadioHeadsetSystem
{
    [Dependency] private readonly RadioDeviceSystem _radioDevice = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Action button events
        SubscribeLocalEvent<STRadioHeadsetComponent, STRadioHeadsetToggleMicActionEvent>(OnToggleMicAction);
        SubscribeLocalEvent<STRadioHeadsetComponent, STRadioHeadsetToggleSpeakerActionEvent>(OnToggleSpeakerAction);
        SubscribeLocalEvent<STRadioHeadsetComponent, GotEquippedEvent>(OnEquipped);

        // UI panel messages - subscribe to update action states when UI buttons are clicked
        // These run after RadioDeviceSystem handles the actual toggle via its RadioStalkerComponent subscription
        SubscribeLocalEvent<STRadioHeadsetComponent, ToggleRadioMicMessage>(OnUiToggleMic);
        SubscribeLocalEvent<STRadioHeadsetComponent, ToggleRadioSpeakerMessage>(OnUiToggleSpeaker);
    }

    private void OnEquipped(Entity<STRadioHeadsetComponent> ent, ref GotEquippedEvent args)
    {
        UpdateActionStates(ent);
    }

    private void OnToggleMicAction(Entity<STRadioHeadsetComponent> ent, ref STRadioHeadsetToggleMicActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<RadioMicrophoneComponent>(ent, out var mic))
            return;

        // Toggle mic - when enabling mic, disable speaker (push-to-talk style)
        var newMicState = !mic.Enabled;
        _radioDevice.SetMicrophoneEnabled(ent, args.Performer, newMicState, true);

        if (newMicState)
            _radioDevice.SetSpeakerEnabled(ent, args.Performer, false, true);

        UpdateActionStates(ent);
        UpdateRadioUi(ent);
        args.Handled = true;
    }

    private void OnToggleSpeakerAction(Entity<STRadioHeadsetComponent> ent, ref STRadioHeadsetToggleSpeakerActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<RadioSpeakerComponent>(ent, out var speaker))
            return;

        // Toggle speaker - when enabling speaker, disable mic (push-to-talk style)
        var newSpeakerState = !speaker.Enabled;
        _radioDevice.SetSpeakerEnabled(ent, args.Performer, newSpeakerState, true);

        if (newSpeakerState)
            _radioDevice.SetMicrophoneEnabled(ent, args.Performer, false, true);

        UpdateActionStates(ent);
        UpdateRadioUi(ent);
        args.Handled = true;
    }

    /// <summary>
    /// Called when the UI panel mic button is clicked.
    /// Updates the action button states to match the new mic/speaker state.
    /// </summary>
    private void OnUiToggleMic(Entity<STRadioHeadsetComponent> ent, ref ToggleRadioMicMessage args)
    {
        // RadioDeviceSystem.OnToggleRadioMic handles the actual toggle on RadioStalkerComponent
        // We just need to sync the action button states
        UpdateActionStates(ent);
    }

    /// <summary>
    /// Called when the UI panel speaker button is clicked.
    /// Updates the action button states to match the new mic/speaker state.
    /// </summary>
    private void OnUiToggleSpeaker(Entity<STRadioHeadsetComponent> ent, ref ToggleRadioSpeakerMessage args)
    {
        // RadioDeviceSystem.OnToggleRadioSpeaker handles the actual toggle on RadioStalkerComponent
        // We just need to sync the action button states
        UpdateActionStates(ent);
    }

    private void UpdateActionStates(Entity<STRadioHeadsetComponent> ent)
    {
        var micEnabled = TryComp<RadioMicrophoneComponent>(ent, out var mic) && mic.Enabled;
        var speakerEnabled = TryComp<RadioSpeakerComponent>(ent, out var speaker) && speaker.Enabled;

        _actions.SetToggled(ent.Comp.ToggleMicActionEntity, micEnabled);
        _actions.SetToggled(ent.Comp.ToggleSpeakerActionEntity, speakerEnabled);
    }

    /// <summary>
    /// Updates the radio UI panel state to reflect current mic/speaker status.
    /// Called after action button toggles to sync the UI with the action button state.
    /// </summary>
    private void UpdateRadioUi(EntityUid uid)
    {
        if (!TryComp<RadioStalkerComponent>(uid, out var stalkerComp))
            return;

        var micEnabled = TryComp<RadioMicrophoneComponent>(uid, out var mic) && mic.Enabled;
        var speakerEnabled = TryComp<RadioSpeakerComponent>(uid, out var speaker) && speaker.Enabled;

        var state = new RadioStalkerBoundUIState(micEnabled, speakerEnabled, stalkerComp.CurrentFrequency);
        _ui.SetUiState(uid, RadioStalkerUiKey.Key, state);
    }
}
