using System.Linq;
using Content.Server._Stalker_EN.MiracleMachine.MiracleMachineComponents;
using Content.Server.Chat.Systems;
using Content.Server.Radio.EntitySystems;
using Content.Shared.Destructible;
using Content.Shared.GameTicking;
using Content.Shared.Radio.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Physics;
using Robust.Shared.Timing;

namespace Content.Server._Stalker_EN.MiracleMachine;

/// <summary>
/// Handles various systems related to the Miracle Machine, such as batteries, teleports, psy fields, etc.
/// </summary>
public sealed class MiracleMachineSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timingSystem = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly MapSystem _mapSystem = default!;
    [Dependency] private readonly RadioSystem _radioSystem = default!;
    private TimeSpan _miracleMachineDisabledTime = TimeSpan.Zero;

    private bool _miracleMachineAnnounced = false;

    private bool _miracleMachineDisabled = false;

    private SoundSpecifier _soundSpecifier = new SoundPathSpecifier("/Audio/Announcements/announce.ogg");
    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<MiracleMachineComponent, ComponentInit>(OnStartup);
        SubscribeLocalEvent<MiracleMachineBatteryComponent, DestructionAttemptEvent>(OnBatteryDestroyed);
    }

    public override void Update(float frameTime)
    {
        if (_miracleMachineAnnounced)
            return;

        if (_miracleMachineDisabledTime == TimeSpan.Zero)
            return;

        if (_miracleMachineDisabledTime <= _timingSystem.CurTime)
        {
            _miracleMachineAnnounced = true;
            _chatSystem.DispatchGlobalAnnouncement("The Miracle Machine has been disabled and additional routes have opened up.", "Stalker Network", true, _soundSpecifier);
        }

    }

    private void OnStartup(EntityUid uid, MiracleMachineComponent comp, ComponentInit args)
    {
        var query = EntityQueryEnumerator<MiracleMachineBatteryComponent>();
        while (query.MoveNext(out var battery, out var _))
        {
            if (!comp.Batteries.Contains(battery))
                comp.Batteries.Add(battery);
        }
    }

    private void OnBatteryDestroyed(EntityUid uid, MiracleMachineBatteryComponent comp, DestructionAttemptEvent args)
    {
        var query = EntityQueryEnumerator<MiracleMachineComponent>();
        while (query.MoveNext(out var _, out var machine))
        {
            if (machine.Batteries.Contains(uid))
            {
                machine.Batteries.Remove(uid);
                if (TryComp<IntrinsicRadioTransmitterComponent>(uid, out var radio))
                {
                    _radioSystem.SendRadioMessage(uid, "A Psy-Battery has been destroyed!", radio.Channels.FirstOrDefault(), uid);
                }
            }


            if (machine.Batteries.Count <= 0 && !_miracleMachineDisabled)
            {
                _miracleMachineDisabled = true;
                MiracleMachineDisabled(machine);
                break;
            }
        }
    }

    /// <summary>
    /// Called when the miracle machine is disabled. Deletes all Miracle Machine specific psy fields, psy sources, and spawns new teleports to different areas
    /// </summary>
    private void MiracleMachineDisabled(MiracleMachineComponent comp)
    {
        // Needed just in case maps are paused and stuff doesn't get deleted. Hopefully works
        foreach (var mapId in _mapSystem.GetAllMapIds())
        {
            _mapSystem.SetPaused(mapId, false);
        }
        var query = EntityQueryEnumerator<MiracleMachineSpawnerComponent>();
        while (query.MoveNext(out var uid, out var spawner))
        {
            if(!spawner.MiracleMachine)
                continue;

            spawner.Inside.Clear();
            QueueDel(uid);
        }

        var query2 = EntityQueryEnumerator<MiracleMachineTeleportsComponent>();
        while (query2.MoveNext(out var uid2, out var teleporters))
        {
            Spawn(teleporters.Teleport, Transform(uid2).Coordinates);
            QueueDel(uid2);
        }

        var query3 = EntityQueryEnumerator<MiracleMachinePsySourceComponent>();
        while (query3.MoveNext(out var uid3, out var psySource))
        {
            QueueDel(uid3);
        }
        _miracleMachineDisabledTime = _timingSystem.CurTime + TimeSpan.FromMinutes(10);
        Spawn("MiracleMachineOff", Transform(comp.Owner).Coordinates);
        QueueDel(comp.Owner);
    }
}
