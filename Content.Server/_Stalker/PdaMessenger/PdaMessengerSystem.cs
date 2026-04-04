using Content.Server.Administration.Logs;
using Content.Server.CartridgeLoader;
using Content.Server.Discord;
using Content.Server.PDA;
using Content.Server.PDA.Ringer;
using Content.Shared._Stalker.PdaMessenger;
using Content.Shared._Stalker_EN.PdaMessenger;
using Content.Shared.CartridgeLoader;
using Content.Shared._Stalker.CCCCVars;
using Content.Shared.Database;
using Content.Shared.PDA;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Content.Server.Mind;
using Content.Shared.PDA.Ringer;
using System.Threading.Tasks;

namespace Content.Server._Stalker.PdaMessenger;

public sealed class PdaMessengerSystem : EntitySystem
{
    [Dependency] private readonly CartridgeLoaderSystem _cartridgeLoader = default!;
    [Dependency] private readonly DiscordWebhook _discord = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IConfigurationManager _configurationManager = default!;
    [Dependency] private readonly PdaSystem _pda = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly RingerSystem _ringer = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    // Use a static sawmill - will be initialized in Initialize()
    private ISawmill _sawmill = default!;

    private readonly List<PdaChat> _chats = new() { new PdaChat("General"), new PdaChat("Rookie"), new PdaChat("Trading"), new PdaChat("Jobs") };
    private WebhookIdentifier? _webhookIdentifier;

    public override void Initialize()
    {
        // Initialize sawmill and set log level to Debug
        _sawmill = Logger.GetSawmill("pda-notify");
        _sawmill.Level = LogLevel.Debug;
        
        _sawmill.Info("[PDA Notify] === PdaMessengerSystem Initialize() CALLED ===");
        
        SubscribeLocalEvent<PdaMessengerComponent, CartridgeMessageEvent>(OnUiMessage);
        SubscribeLocalEvent<PdaMessengerComponent, CartridgeUiReadyEvent>(OnUiReady);
        
        _sawmill.Info("[PDA Notify] Subscribed to CartridgeMessageEvent and CartridgeUiReadyEvent");

        _configurationManager.OnValueChanged(CCCCVars.DiscordPdaMessageWebhook, value =>
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                _discord.GetWebhook(value, data => _webhookIdentifier = data.ToIdentifier());
            }
        }, true);
    }

    private void OnUiReady(Entity<PdaMessengerComponent> ent, ref CartridgeUiReadyEvent args)
    {
        UpdateUiState(ent, args.Loader, ent.Comp);
    }

    private void OnUiMessage(Entity<PdaMessengerComponent> messenger, ref CartridgeMessageEvent args)
    {
        _sawmill.Info($"[PDA Notify] OnUiMessage received: MessageType={args.GetType().Name}, NextSendTime={messenger.Comp.NextSendTime}, CurTime={_timing.CurTime}");
        
        if (messenger.Comp.NextSendTime > _timing.CurTime)
        {
            _sawmill.Info($"[PDA Notify] OnUiMessage: Cooldown active, returning");
            return;
        }

        messenger.Comp.NextSendTime = _timing.CurTime + messenger.Comp.SendTimeCooldown;
        SendMessage(messenger, ref args);
    }

    private void SendMessage(Entity<PdaMessengerComponent> messenger, ref CartridgeMessageEvent args)
    {
        _sawmill.Info($"[PDA Notify] SendMessage called, args type={args.GetType().FullName}");

        var user = messenger.Owner;
        _sawmill.Info($"[PDA Notify] SendMessage: user={user}, Exists={Exists(user)}");
        
        if (!Exists(user))
        {
            _sawmill.Warning($"[PDA Notify] SendMessage: user does not exist");
            return;
        }

        _sawmill.Info($"[PDA Notify] SendMessage: LoaderUid={args.LoaderUid}");
        
        if (!TryComp<PdaComponent>(GetEntity(args.LoaderUid), out var senderPda))
        {
            _sawmill.Warning($"[PDA Notify] SendMessage: PdaComponent not found on loader {args.LoaderUid}");
            return;
        }

        _sawmill.Info($"[PDA Notify] SendMessage: Actor={args.Actor}");
        
        if (!_mind.TryGetMind(args.Actor, out _, out var mindComp))
        {
            _sawmill.Warning($"[PDA Notify] SendMessage: Mind not found for actor {args.Actor}");
            return;
        }

        _sawmill.Info($"[PDA Notify] SendMessage: mindComp found, CharacterName={mindComp.CharacterName}");

        // stalker-en-changes: pda.OwnerName is a numeric hash set by StationSpawningSystem
        var senderName = mindComp.CharacterName ?? "Unknown";
        senderPda.OwnerName = senderName;

        if (args is MessengerUiSetLoginEvent)
        {
            _sawmill.Info($"[PDA Notify] MessengerUiSetLoginEvent received");
            UpdateUiState(messenger, GetEntity(args.LoaderUid), messenger.Comp);
        }

        if (args is not MessengerUiMessageEvent message)
        {
            _sawmill.Warning($"[PDA Notify] Expected MessengerUiMessageEvent, got {args.GetType().Name}");
            return;
        }

        _sawmill.Info($"[PDA Notify] Processing message: Receiver={message.Message.Receiver}, Title={message.Message.Title}");

        _adminLogger.Add(LogType.PdaMessage, LogImpact.Medium, $"{ToPrettyString(user):player} send message to {message.Message.Receiver}, title: {message.Message.Title}, content: {message.Message.Content}");

        message.Message.Title = $"{senderName}: {message.Message.Title}";
        if (message.Message.Receiver == "General")
        {
            _sawmill.Info($"[PDA Notify] General channel message received: Title={message.Message.Title}, BandId={message.Message.BandId}");

            _chats[0].Messages.Add(message.Message);
            _ = SendMessageDiscordMessageAsync(message.Message);
            UpdateUiState(messenger, GetEntity(args.LoaderUid), messenger.Comp);

            // Send pop-up notification event to all clients EXCEPT the sender
            var generalEvent = new PdaGeneralMessageEvent(message.Message.Title, message.Message.Content, senderName, message.Message.BandId);
            RaiseNetworkEvent(generalEvent, Filter.PvsExcept(user));
            _sawmill.Info($"[PDA Notify] Sent PdaGeneralMessageEvent to all clients except sender");

            // Play ringtone on all PDAs with messenger installed
            TryNotify();

            return;
        }
        else if (message.Message.Receiver == "Rookie")
        {
            _chats[1].Messages.Add(message.Message);
            _ = SendMessageDiscordMessageAsync(message.Message);
            UpdateUiState(messenger, GetEntity(args.LoaderUid), messenger.Comp);
            return;
        }
        else if (message.Message.Receiver == "Trading")
        {
            _chats[2].Messages.Add(message.Message);
            _ = SendMessageDiscordMessageAsync(message.Message);
            UpdateUiState(messenger, GetEntity(args.LoaderUid), messenger.Comp);
            return;
        }
        else if (message.Message.Receiver == "Jobs")
        {
            _chats[3].Messages.Add(message.Message);
            _ = SendMessageDiscordMessageAsync(message.Message);
            UpdateUiState(messenger, GetEntity(args.LoaderUid), messenger.Comp);
            return;
        }

        // stalker-en-changes-start: fix DM delivery
        var inserted = false;
        foreach (var chat in _chats)
        {
            if (chat.Receiver is null)
                continue;

            var isSameDirection = chat.Sender == senderName && chat.Receiver == message.Message.Receiver;
            var isReverseDirection = chat.Sender == message.Message.Receiver && chat.Receiver == senderName;
            if (!isSameDirection && !isReverseDirection)
                continue;

            chat.Messages.Add(message.Message);
            inserted = true;
            break;
        }

        if (!inserted)
        {
            var newChat = new PdaChat(message.Message.Receiver, message.Message.Receiver, senderName);
            newChat.Messages.Add(message.Message);
            _chats.Add(newChat);
        }

        // Find the recipient and send DM notification
        EntityUid? recipientEntity = null;
        var query = EntityQueryEnumerator<PdaComponent>();
        while (query.MoveNext(out var uid, out var pda))
        {
            if (pda.PdaOwner is not { } pdaOwner
                || !_mind.TryGetMind(pdaOwner, out _, out var recipientMind)
                || recipientMind.CharacterName != message.Message.Receiver)
            {
                continue;
            }

            recipientEntity = pdaOwner;

            if (TryComp<RingerComponent>(uid, out var recipientRinger))
                _ringer.RingerPlayRingtone((uid, recipientRinger));

            if (_cartridgeLoader.TryGetProgram<PdaMessengerComponent>(uid, out var progUid, out var messengerComp))
                UpdateUiState(progUid.Value, uid, messengerComp);
        }

        // Send DM notification event to the recipient only
        if (recipientEntity.HasValue)
        {
            var dmEvent = new PdaDirectMessageEvent(senderName, message.Message.Content, message.Message.BandId);
            RaiseNetworkEvent(dmEvent, recipientEntity.Value);
            _sawmill.Info($"[PDA Notify] Sent PdaDirectMessageEvent to recipient {recipientEntity.Value}");
        }

        UpdateUiState(messenger, GetEntity(args.LoaderUid), messenger.Comp);
        // stalker-en-changes-end
    }

    // stalker-en-changes: pda.OwnerName is unreliable, resolve via PdaOwner instead
    private void UpdateUiState(EntityUid uid, EntityUid loaderUid, PdaMessengerComponent component)
    {
        if (!TryComp<PdaComponent>(loaderUid, out var pda))
            return;

        string? ownerName = null;
        if (pda.PdaOwner is { } pdaOwner
            && _mind.TryGetMind(pdaOwner, out _, out var ownerMind))
        {
            ownerName = ownerMind.CharacterName;
        }

        var chats = new List<PdaChat>();
        foreach (var chat in _chats)
        {
            if (chat.Receiver is not null
                && chat.Receiver != ownerName
                && chat.Sender != ownerName)
            {
                continue;
            }

            // stalker-en-changes: For DM chats, display the other person's name
            if (chat.Receiver is not null && chat.Receiver == ownerName)
            {
                var displayChat = new PdaChat(chat.Sender ?? chat.Name, chat.Receiver, chat.Sender);
                foreach (var msg in chat.Messages)
                    displayChat.Messages.Add(msg);
                chats.Add(displayChat);
            }
            else
            {
                chats.Add(chat);
            }
        }

        var state = new MessengerUiState(chats);
        _cartridgeLoader.UpdateCartridgeUiState(loaderUid, state);
    }

    private void TryNotify()
    {
        var query = EntityQueryEnumerator<CartridgeLoaderComponent, RingerComponent, ContainerManagerComponent>();
        while (query.MoveNext(out var uid, out var comp, out var ringer, out var cont))
        {
            if (!_cartridgeLoader.TryGetProgram<PdaMessengerComponent>(uid, out _, out _, false, comp, cont))
                continue;

            _ringer.RingerPlayRingtone((uid, ringer));
        }
    }

    private async Task SendMessageDiscordMessageAsync(PdaMessage message)
    {
        try
        {
            if (_webhookIdentifier is null)
                return;

            var payload = new WebhookPayload
            {
                Content = $"### {message.Title}\n```{message.Content}```",
            };

            await _discord.CreateMessage(_webhookIdentifier.Value, payload);
        }
        catch (Exception e)
        {
            Log.Error($"Error while sending discord PDA message:\n{e}");
        }
    }
}
