using Content.Server.Chat.Systems;
using Content.Server.Radio.Components;
using Content.Server.Radio.EntitySystems;
using Content.Shared._Stalker_EN.Radio;
using Content.Shared.Inventory;
using Content.Shared.Radio;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Stalker_EN.Radio;

/// <summary>
/// Relays distress emotes (gasps, deathgasps) over the Stalker radio headset
/// when the player's microphone is enabled, allowing teammates on the same
/// frequency to hear critical condition sounds.
/// </summary>
public sealed class STRadioEmoteRelaySystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private static readonly HashSet<string> RelayedEmotes = new()
    {
        "Gasp",
        "DefaultDeathgasp",
    };

    private static readonly ProtoId<RadioChannelPrototype> StalkerInternalChannel = "StalkerInternal";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<InventoryComponent, EmoteEvent>(OnEmote);
    }

    private void OnEmote(Entity<InventoryComponent> ent, ref EmoteEvent args)
    {
        if (!RelayedEmotes.Contains(args.Emote.ID))
            return;

        if (!_inventory.TryGetSlotEntity(ent, "ears", out var headsetUid, ent.Comp))
            return;

        if (!HasComp<STRadioHeadsetComponent>(headsetUid))
            return;

        if (!TryComp<RadioMicrophoneComponent>(headsetUid, out var mic) || !mic.Enabled)
            return;

        if (args.Emote.ChatMessages.Count == 0)
            return;

        var emoteText = Loc.GetString(_random.Pick(args.Emote.ChatMessages), ("entity", ent.Owner));
        _radio.SendRadioMessage(ent, emoteText, StalkerInternalChannel, headsetUid.Value);
    }
}
