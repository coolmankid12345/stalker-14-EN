using Content.Server.Chat.Systems;
using Content.Server.Radio;
using Content.Server.Radio.Components;
using Content.Server.Radio.EntitySystems;
using Content.Shared._Stalker.RadioStalker.Components;
using Content.Shared._Stalker_EN.Radio;
using Content.Shared.Actions;
using Content.Shared.Chat;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Speech;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server._Stalker_EN.Radio;

/// <summary>
/// Server-side system for stalker radio headsets that handles action events,
/// radio receiving (personal speaker to wearer only), and UI state synchronization.
/// Speaker is always active when equipped - no toggle needed.
/// </summary>
public sealed class STRadioHeadsetSystem : SharedSTRadioHeadsetSystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly INetManager _netMan = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly RadioDeviceSystem _radioDevice = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    /// <summary>
    /// The radio channel that the stalker headset listens to.
    /// </summary>
    private const string StalkerInternalChannel = "StalkerInternal";

    public override void Initialize()
    {
        base.Initialize();

        // Action button events
        SubscribeLocalEvent<STRadioHeadsetComponent, STRadioHeadsetToggleMicActionEvent>(OnToggleMicAction);

        // Equipment events for managing ActiveRadioComponent
        SubscribeLocalEvent<STRadioHeadsetComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<STRadioHeadsetComponent, GotUnequippedEvent>(OnUnequipped);

        // Radio receive event - send messages directly to wearer only
        SubscribeLocalEvent<STRadioHeadsetComponent, RadioReceiveEvent>(OnRadioReceive);

        // UI panel messages - use our own message types instead of upstream ones
        SubscribeLocalEvent<STRadioHeadsetComponent, STRadioHeadsetToggleMicMessage>(OnUiToggleMic,
            after: new[] { typeof(RadioDeviceSystem) });
        SubscribeLocalEvent<STRadioHeadsetComponent, STRadioHeadsetSelectFrequencyMessage>(OnUiSelectFrequency);

        // UI open event
        SubscribeLocalEvent<STRadioHeadsetComponent, BeforeActivatableUIOpenEvent>(OnBeforeUiOpen);
    }

    private void OnEquipped(Entity<STRadioHeadsetComponent> ent, ref GotEquippedEvent args)
    {
        // Only activate radio when equipped to ears slot
        if (!args.SlotFlags.HasFlag(SlotFlags.EARS))
            return;

        // Add ActiveRadioComponent to receive radio messages on StalkerInternal channel
        var active = EnsureComp<ActiveRadioComponent>(ent);
        active.Channels.Clear();
        active.Channels.Add(StalkerInternalChannel);

        UpdateActionStates(ent);
    }

    private void OnUnequipped(Entity<STRadioHeadsetComponent> ent, ref GotUnequippedEvent args)
    {
        // Remove ActiveRadioComponent when unequipped
        RemComp<ActiveRadioComponent>(ent);
    }

    /// <summary>
    /// Handles receiving radio messages - sends directly to the wearer's client only.
    /// Does NOT broadcast to nearby players like RadioSpeaker does.
    /// Constructs a custom message with the receiver's tuned frequency as the channel name.
    /// </summary>
    private void OnRadioReceive(Entity<STRadioHeadsetComponent> ent, ref RadioReceiveEvent args)
    {
        // Get the wearer (parent entity) and check if they're a player
        if (!TryComp(Transform(ent).ParentUid, out ActorComponent? actor))
            return;

        // Get the frequency to display - use the receiver's tuned frequency
        var channelDisplay = TryComp<RadioStalkerComponent>(ent, out var stalkerComp) && !string.IsNullOrEmpty(stalkerComp.CurrentFrequency)
            ? Loc.GetString("st-radio-headset-frequency-display", ("frequency", stalkerComp.CurrentFrequency))
            : Loc.GetString("st-radio-headset-channel-default");

        // Get the speaker name with any transforms applied
        var speakerName = GetTransformedSpeakerName(args.MessageSource, out var speechVerb);

        // Get the speech verb prototype
        SpeechVerbPrototype speech;
        if (speechVerb != null && _prototype.TryIndex(speechVerb, out var verbProto))
            speech = verbProto;
        else
            speech = _chat.GetSpeechVerb(args.MessageSource, args.Message);

        // Escape message content for safe display
        var content = FormattedMessage.EscapeText(args.Message);

        // Build the formatted radio message with the dynamic frequency
        var wrappedMessage = Loc.GetString(speech.Bold ? "chat-radio-message-wrap-bold" : "chat-radio-message-wrap",
            ("color", args.Channel.Color),
            ("fontType", speech.FontId),
            ("fontSize", speech.FontSize),
            ("verb", Loc.GetString(_random.Pick(speech.SpeechVerbStrings))),
            ("channel", $"\\[{channelDisplay}\\]"),
            ("name", speakerName),
            ("message", content));

        // Create and send the chat message
        var chat = new ChatMessage(
            ChatChannel.Radio,
            args.Message,
            wrappedMessage,
            NetEntity.Invalid,
            null);
        var chatMsg = new MsgChatMessage { Message = chat };

        _netMan.ServerSendMessage(chatMsg, actor.PlayerSession.Channel);
    }

    /// <summary>
    /// Gets the transformed speaker name, applying any voice modifications.
    /// </summary>
    private string GetTransformedSpeakerName(EntityUid source, out ProtoId<SpeechVerbPrototype>? speechVerb)
    {
        var evt = new TransformSpeakerNameEvent(source, MetaData(source).EntityName);
        RaiseLocalEvent(source, evt);
        speechVerb = evt.SpeechVerb;
        return FormattedMessage.EscapeText(evt.VoiceName);
    }

    private void OnToggleMicAction(Entity<STRadioHeadsetComponent> ent, ref STRadioHeadsetToggleMicActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<RadioMicrophoneComponent>(ent, out var mic))
            return;

        // Toggle mic state
        var newMicState = !mic.Enabled;
        _radioDevice.SetMicrophoneEnabled(ent, args.Performer, newMicState, true);

        UpdateActionStates(ent);
        UpdateRadioUi(ent);
        args.Handled = true;
    }

    private void OnBeforeUiOpen(Entity<STRadioHeadsetComponent> ent, ref BeforeActivatableUIOpenEvent args)
    {
        UpdateRadioUi(ent);
    }

    /// <summary>
    /// Called when the UI panel mic button is clicked.
    /// Updates the microphone state and syncs action button states.
    /// </summary>
    private void OnUiToggleMic(Entity<STRadioHeadsetComponent> ent, ref STRadioHeadsetToggleMicMessage args)
    {
        _radioDevice.SetMicrophoneEnabled(ent, args.Actor, args.Enabled, true);
        UpdateActionStates(ent);
    }

    /// <summary>
    /// Called when the user enters a frequency in the UI.
    /// Updates the RadioStalkerComponent's CurrentFrequency.
    /// </summary>
    private void OnUiSelectFrequency(Entity<STRadioHeadsetComponent> ent, ref STRadioHeadsetSelectFrequencyMessage args)
    {
        if (!TryComp<RadioStalkerComponent>(ent, out var stalkerComp))
            return;

        stalkerComp.CurrentFrequency = args.Frequency;
        UpdateRadioUi(ent);
    }

    private void UpdateActionStates(Entity<STRadioHeadsetComponent> ent)
    {
        var micEnabled = TryComp<RadioMicrophoneComponent>(ent, out var mic) && mic.Enabled;
        _actions.SetToggled(ent.Comp.ToggleMicActionEntity, micEnabled);
    }

    /// <summary>
    /// Updates the radio UI panel state to reflect current mic status and frequency.
    /// Called after action button toggles to sync the UI with the action button state.
    /// </summary>
    private void UpdateRadioUi(EntityUid uid)
    {
        if (!TryComp<RadioStalkerComponent>(uid, out var stalkerComp))
            return;

        var micEnabled = TryComp<RadioMicrophoneComponent>(uid, out var mic) && mic.Enabled;

        var state = new STRadioHeadsetBoundUIState(micEnabled, stalkerComp.CurrentFrequency);
        _ui.SetUiState(uid, STRadioHeadsetUiKey.Key, state);
    }
}
