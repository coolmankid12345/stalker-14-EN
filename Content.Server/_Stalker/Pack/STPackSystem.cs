using Content.Server._Stalker.ApproachTrigger;
using Content.Server.NPC;
using Content.Shared.Trigger;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared._Stalker.Pack;
using Content.Shared.Mobs;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using System.Numerics;

namespace Content.Server._Stalker.Pack;

public sealed class STPackSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly NPCSystem _npc = default!;
    [Dependency] private readonly HTNSystem _htn = default!;

    public override void Initialize()
    {
        base.Initialize();

SubscribeLocalEvent<STPackSpawnerComponent, MapInitEvent>(OnSpawnerMapInit);
        SubscribeLocalEvent<STPackSpawnerComponent, TriggerEvent>(OnTrigger);

        SubscribeLocalEvent<STPackHeadComponent, MobStateChangedEvent>(OnHeadStateChanged);
        SubscribeLocalEvent<STPackHeadComponent, EntityTerminatingEvent>(OnHeadDeleted);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<STPackSpawnerComponent>();
        while (query.MoveNext(out var uid, out var spawner))
        {
            if (spawner.CooldownTime > _timing.CurTime)
                continue;

            if (TryComp<ApproachTriggerComponent>(uid, out var approach))
                approach.Enabled = true;

            spawner.Enabled = true;
        }
    }

    private void OnTrigger(Entity<STPackSpawnerComponent> entity, ref TriggerEvent args)
    {
        if (!entity.Comp.Enabled)
            return;

        if (!_random.Prob(entity.Comp.Chance))
            return;

        CreatePack(entity.Comp.ProtoId, _transform.GetMapCoordinates(entity));

        entity.Comp.CooldownTime = _timing.CurTime + TimeSpan.FromSeconds(entity.Comp.Cooldown);
        entity.Comp.Enabled = false;

        if (TryComp<ApproachTriggerComponent>(entity, out var approach))
            approach.Enabled = false;
    }

    private void OnHeadStateChanged(Entity<STPackHeadComponent> entity, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Alive)
            return;

        SetRandomHead(entity);
    }

    private void OnHeadDeleted(Entity<STPackHeadComponent> entity, ref EntityTerminatingEvent args)
    {
        SetRandomHead(entity);
    }

private void OnSpawnerMapInit(Entity<STPackSpawnerComponent> entity, ref MapInitEvent args)
    {
        CreatePack(entity.Comp.ProtoId, _transform.GetMapCoordinates(entity));
    }

    public void CreatePack(ProtoId<STPackPrototype> prototypeId, MapCoordinates coordinates)
    {
        if (!_prototype.TryIndex(prototypeId, out var prototype))
        {
            Log.Error($"Failed create pack, prototype {prototypeId} not exists");
            return;
        }

        // Creating head
        var headUid = Spawn(prototype.Head, coordinates);
        AddComp<STPackHeadComponent>(headUid);

        // Creating members
        var memberCount = _random.Next(prototype.MinMemberCount, prototype.MaxMemberCount);
        for (var i = 0; i < memberCount; i++)
        {
            var memberPrototype = _random.Pick(prototype.Members);

            var memberUid = Spawn(memberPrototype, coordinates);
            var memberComponent = EnsureComp<STPackMemberComponent>(memberUid);
            memberComponent.Head = headUid;

            SetBlackboard(memberUid, memberComponent.BlackboardHeadKey, headUid);
        }
    }

    private void SetRandomHead(EntityUid previousHead)
    {
        EntityUid? newHead = null;
        var query = EntityQueryEnumerator<STPackMemberComponent>();

        while (query.MoveNext(out var uid, out var memberComponent))
        {
            if (memberComponent.Head != previousHead)
                continue;

            if (newHead is null)
            {
                newHead ??= uid;
                continue;
            }

            memberComponent.Head = newHead.Value;
            SetBlackboard(uid, memberComponent.BlackboardHeadKey, newHead.Value);
        }

        if (newHead is null)
            return;

        if (HasComp<STPackMemberComponent>(newHead.Value))
            RemComp<STPackMemberComponent>(newHead.Value);

        if (!HasComp<STPackHeadComponent>(newHead.Value))
            AddComp<STPackHeadComponent>(newHead.Value);
    }

    private void SetBlackboard(EntityUid member, string blackboard, EntityUid head)
    {
        if (!TryComp<HTNComponent>(member, out var htn))
            return;

        if (htn.Plan is not null)
            _htn.ShutdownPlan(htn);

        _npc.SetBlackboard(member, blackboard, new EntityCoordinates(head, Vector2.Zero));
        _htn.Replan(htn);
    }
}
