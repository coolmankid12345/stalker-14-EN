using System.Text;
using Content.Server._Stalker.Chat;
using Content.Server.Chat.Systems;
using Content.Shared._Stalker.Deafness;
using Content.Shared.Chat;
using Content.Shared.Ghost;
using Content.Shared.Speech;
using Content.Shared.Speech.Components;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server._Stalker_EN.Chat;

/// <summary>
///     Muffles or blocks voice chat when walls occlude the path between speaker and listener.
///     Also blocks microphone/radio listeners from picking up speech through walls.
/// </summary>
public sealed class STVoiceOcclusionSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly OccluderSystem _occluder = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    /// <summary>
    ///     Maximum distance at which occluded speech is muffled rather than blocked entirely.
    /// </summary>
    private const int VoiceMuffledRange = 3;

    /// <summary>
    ///     Chance each non-whitespace character is kept readable (same scale as whisper: 0.2 = 20%).
    /// </summary>
    private const float MuffledReadabilityChance = 0.2f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ActorComponent, STChatMessageOverrideInVoiceRangeEvent>(OnVoiceRange);
        SubscribeLocalEvent<ActiveListenerComponent, ListenAttemptEvent>(OnListenAttempt);
    }

    private void OnVoiceRange(Entity<ActorComponent> ent, ref STChatMessageOverrideInVoiceRangeEvent args)
    {
        // Don't interfere with non-IC or visual-only channels.
        if (args.Channel is ChatChannel.LOOC or ChatChannel.Narration
            or ChatChannel.Damage or ChatChannel.Visual or ChatChannel.Notifications)
            return;

        // Ghosts and observers always hear everything.
        if (args.Observer || HasComp<GhostHearingComponent>(ent.Owner))
            return;

        // Relay recipients (surveillance cameras, AI) — their range was computed from the
        // relay entity's position, not the viewer's body; occlusion check would be wrong.
        if (args.HideChatOverride != null)
            return;

        // Let STDeafSystem handle deaf entities instead of us.
        if (HasComp<STDeafComponent>(ent.Owner))
            return;

        // Defensive: negative range means ghost/observer distance — already handled above.
        if (args.Range < 0)
            return;

        var sourcePos = _transform.GetMapCoordinates(args.Source);
        var listenerPos = _transform.GetMapCoordinates(ent.Owner);

        if (_occluder.InRangeUnoccluded(sourcePos, listenerPos, ChatSystem.VoiceRange, ignoreTouching: true))
            return;

        // Wall detected — muffle if close enough on Local, otherwise block.
        if (args.Channel == ChatChannel.Local && args.Range <= VoiceMuffledRange)
        {
            var obfuscated = ObfuscateMessage(args.Message, MuffledReadabilityChance);
            args.Message = obfuscated;
            args.WrappedMessage = Loc.GetString("st-chat-voice-muffled-wrap-message",
                ("message", FormattedMessage.EscapeText(obfuscated)));
            return;
        }

        args.Cancelled = true;
    }

    /// <summary>
    ///     Replaces non-whitespace characters with '~' based on a readability chance,
    ///     mirroring the whisper obfuscation style from <see cref="ChatSystem"/>.
    /// </summary>
    private string ObfuscateMessage(string message, float chance)
    {
        var sb = new StringBuilder(message);
        for (var i = 0; i < message.Length; i++)
        {
            if (char.IsWhiteSpace(sb[i]))
                continue;

            if (_random.Prob(1 - chance))
                sb[i] = '~';
        }
        return sb.ToString();
    }

    private void OnListenAttempt(Entity<ActiveListenerComponent> ent, ref ListenAttemptEvent args)
    {
        var sourcePos = _transform.GetMapCoordinates(args.Source);
        var listenerPos = _transform.GetMapCoordinates(ent.Owner);

        if (!_occluder.InRangeUnoccluded(sourcePos, listenerPos, ent.Comp.Range, ignoreTouching: true))
            args.Cancel();
    }
}
