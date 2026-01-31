using System.Linq;
using Content.Server.Administration.Logs;
using Content.Shared._Stalker_EN.CollisionDamage;
using Content.Shared.Database;
using Content.Shared.Damage;
using Content.Shared.Mind.Components;
using Robust.Server.Audio;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;

namespace Content.Server._Stalker_EN.CollisionDamage;

/// <summary>
/// Applies damage on collision. Supports charging mutants, projectiles, and environmental hazards.
/// </summary>
public sealed class STCollisionDamageSystem : EntitySystem
{
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;

    /// <summary>
    /// Maximum number of targets tracked per entity to prevent memory issues with long-lived entities.
    /// </summary>
    private const int MaxTrackedTargetsPerEntity = 50;

    /// <summary>
    /// Reusable list for cleanup operations to avoid GC pressure.
    /// </summary>
    private readonly List<EntityUid> _toRemove = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<STCollisionDamageComponent, StartCollideEvent>(OnStartCollide);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<STCollisionDamageComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.LastDamageTime.Count == 0)
                continue;

            _toRemove.Clear();
            foreach (var (entity, lastHit) in comp.LastDamageTime)
            {
                if (!Exists(entity) || curTime >= lastHit + comp.DamageCooldown + TimeSpan.FromSeconds(1))
                    _toRemove.Add(entity);
            }

            foreach (var entity in _toRemove)
                comp.LastDamageTime.Remove(entity);
        }
    }

    private void OnStartCollide(EntityUid uid, STCollisionDamageComponent component, ref StartCollideEvent args)
    {
        if (component.PendingDeletion)
            return;

        if (component.FixtureId != null && args.OurFixtureId != component.FixtureId)
            return;

        if (component.MaxTargets.HasValue && component.TargetsHit >= component.MaxTargets.Value)
            return;

        var target = args.OtherEntity;
        var curTime = _timing.CurTime;

        // EntityUid can be reused after deletion
        if (component.LastDamageTime.ContainsKey(target) && !Exists(target))
            component.LastDamageTime.Remove(target);

        if (component.LastDamageTime.TryGetValue(target, out var lastHit) &&
            curTime < lastHit + component.DamageCooldown)
        {
            return;
        }

        if (component.OnlyMindedEntities &&
            (!TryComp<MindContainerComponent>(target, out var mind) || !mind.HasMind))
        {
            return;
        }

        var damage = _damageable.TryChangeDamage(target, component.Damage, component.IgnoreResistances, origin: uid);
        if (damage == null)
            return;

        var isNewTarget = !component.LastDamageTime.ContainsKey(target);
        component.LastDamageTime[target] = curTime;

        if (isNewTarget)
            component.TargetsHit++;

        // Prevent unbounded memory growth on long-lived entities
        if (component.LastDamageTime.Count > MaxTrackedTargetsPerEntity)
        {
            var oldest = component.LastDamageTime.MinBy(x => x.Value).Key;
            component.LastDamageTime.Remove(oldest);
        }

        Dirty(uid, component);

        _adminLog.Add(LogType.Damaged, LogImpact.Medium,
            $"{ToPrettyString(uid):source} dealt collision damage to {ToPrettyString(target):target}");

        if (component.HitSound != null)
            _audio.PlayPvs(component.HitSound, uid);

        if (component.DeleteOnHit &&
            (!component.MaxTargets.HasValue || component.TargetsHit >= component.MaxTargets.Value))
        {
            component.PendingDeletion = true;
            QueueDel(uid);
        }
    }
}
