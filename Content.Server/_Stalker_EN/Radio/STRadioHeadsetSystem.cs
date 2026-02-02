using System.Text.RegularExpressions;
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
using Content.Shared.Radio;
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
    private static readonly ProtoId<RadioChannelPrototype> StalkerInternalChannel = "StalkerInternal";

    /// <summary>
    /// Maximum allowed length for user-entered frequency strings.
    /// Format: "000.0" = 5 characters (3 digits + dot + 1 digit)
    /// </summary>
    private const int MaxFrequencyLength = 5;

    /// <summary>
    /// Regex pattern for validating frequency format: exactly 3 digits, a dot, and 1 digit.
    /// </summary>
    private static readonly Regex FrequencyPattern = new(@"^\d{3}\.\d$", RegexOptions.Compiled);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STRadioHeadsetComponent, STRadioHeadsetToggleMicActionEvent>(OnToggleMicAction);
        SubscribeLocalEvent<STRadioHeadsetComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<STRadioHeadsetComponent, GotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<STRadioHeadsetComponent, RadioReceiveEvent>(OnRadioReceive);
        SubscribeLocalEvent<STRadioHeadsetComponent, STRadioHeadsetToggleMicMessage>(OnUiToggleMic,
            after: new[] { typeof(RadioDeviceSystem) });
        SubscribeLocalEvent<STRadioHeadsetComponent, STRadioHeadsetSelectFrequencyMessage>(OnUiSelectFrequency);
        SubscribeLocalEvent<STRadioHeadsetComponent, BeforeActivatableUIOpenEvent>(OnBeforeUiOpen);
    }

    protected override void OnMapInit(Entity<STRadioHeadsetComponent> ent, ref MapInitEvent args)
    {
        base.OnMapInit(ent, ref args);

        if (!TryComp<RadioStalkerComponent>(ent, out var stalkerComp))
            return;

        if (string.IsNullOrEmpty(stalkerComp.CurrentFrequency))
        {
            stalkerComp.CurrentFrequency = STRadioHeadsetComponent.DefaultFrequency;
            Dirty(ent, stalkerComp);
        }
    }

    private void OnEquipped(Entity<STRadioHeadsetComponent> ent, ref GotEquippedEvent args)
    {
        if (!args.SlotFlags.HasFlag(SlotFlags.EARS))
            return;

        // Recreate action if invalid - handles stash retrieval/loadout where DeleteChildren
        // queues the action for deletion before equip completes.
        if (!_actions.TryGetActionData(ent.Comp.ToggleMicActionEntity, out _, logError: false))
        {
            ent.Comp.ToggleMicActionEntity = null;
            _actionContainer.EnsureAction(ent, ref ent.Comp.ToggleMicActionEntity, ent.Comp.ToggleMicAction);
            Dirty(ent);
        }

        var active = EnsureComp<ActiveRadioComponent>(ent);
        active.Channels.Clear();
        active.Channels.Add(StalkerInternalChannel);

        UpdateActionStates(ent);
    }

    private void OnUnequipped(Entity<STRadioHeadsetComponent> ent, ref GotUnequippedEvent args)
    {
        if (!args.SlotFlags.HasFlag(SlotFlags.EARS))
            return;

        RemComp<ActiveRadioComponent>(ent);
    }

    /// <summary>
    /// Sends radio messages directly to the wearer only, not broadcast to nearby players.
    /// Displays the receiver's tuned frequency as the channel name.
    /// </summary>
    private void OnRadioReceive(Entity<STRadioHeadsetComponent> ent, ref RadioReceiveEvent args)
    {
        if (!TryComp(Transform(ent).ParentUid, out ActorComponent? actor))
            return;

        var channelDisplay = TryComp<RadioStalkerComponent>(ent, out var stalkerComp) && !string.IsNullOrEmpty(stalkerComp.CurrentFrequency)
            ? Loc.GetString("st-radio-headset-frequency-display", ("frequency", stalkerComp.CurrentFrequency))
            : Loc.GetString("st-radio-headset-channel-default");

        var speakerName = GetTransformedSpeakerName(args.MessageSource, out var speechVerb);

        SpeechVerbPrototype speech;
        if (speechVerb != null && _prototype.TryIndex(speechVerb, out var verbProto))
            speech = verbProto;
        else
            speech = _chat.GetSpeechVerb(args.MessageSource, args.Message);

        var content = FormattedMessage.EscapeText(args.Message);

        var wrappedMessage = Loc.GetString(speech.Bold ? "chat-radio-message-wrap-bold" : "chat-radio-message-wrap",
            ("color", ent.Comp.ChatColor),
            ("fontType", speech.FontId),
            ("fontSize", speech.FontSize),
            ("verb", Loc.GetString(_random.Pick(speech.SpeechVerbStrings))),
            ("channel", $"\\[{channelDisplay}\\]"),
            ("name", speakerName),
            ("message", content));

        var chat = new ChatMessage(
            ChatChannel.Radio,
            args.Message,
            wrappedMessage,
            NetEntity.Invalid,
            null);
        var chatMsg = new MsgChatMessage { Message = chat };

        _netMan.ServerSendMessage(chatMsg, actor.PlayerSession.Channel);
    }

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

        _radioDevice.SetMicrophoneEnabled(ent, args.Performer, !mic.Enabled, true);
        UpdateActionStates(ent);
        UpdateRadioUi(ent);
        args.Handled = true;
    }

    private void OnBeforeUiOpen(Entity<STRadioHeadsetComponent> ent, ref BeforeActivatableUIOpenEvent args)
    {
        UpdateRadioUi(ent);
    }

    private void OnUiToggleMic(Entity<STRadioHeadsetComponent> ent, ref STRadioHeadsetToggleMicMessage args)
    {
        _radioDevice.SetMicrophoneEnabled(ent, args.Actor, args.Enabled, true);
        UpdateActionStates(ent);
    }

    private void OnUiSelectFrequency(Entity<STRadioHeadsetComponent> ent, ref STRadioHeadsetSelectFrequencyMessage args)
    {
        if (!TryComp<RadioStalkerComponent>(ent, out var stalkerComp))
            return;

        var frequency = args.Frequency;

        if (string.IsNullOrWhiteSpace(frequency) || !FrequencyPattern.IsMatch(frequency))
            frequency = STRadioHeadsetComponent.DefaultFrequency;
        else if (frequency.Length > MaxFrequencyLength)
            frequency = frequency[..MaxFrequencyLength];

        stalkerComp.CurrentFrequency = frequency;
        Dirty(ent, stalkerComp);
        UpdateRadioUi(ent);
    }

    private void UpdateActionStates(Entity<STRadioHeadsetComponent> ent)
    {
        var micEnabled = TryComp<RadioMicrophoneComponent>(ent, out var mic) && mic.Enabled;
        _actions.SetToggled(ent.Comp.ToggleMicActionEntity, micEnabled);
    }

    private void UpdateRadioUi(EntityUid uid)
    {
        if (!TryComp<RadioStalkerComponent>(uid, out var stalkerComp))
            return;

        var micEnabled = TryComp<RadioMicrophoneComponent>(uid, out var mic) && mic.Enabled;

        var state = new STRadioHeadsetBoundUIState(micEnabled, stalkerComp.CurrentFrequency);
        _ui.SetUiState(uid, STRadioHeadsetUiKey.Key, state);
    }
}
