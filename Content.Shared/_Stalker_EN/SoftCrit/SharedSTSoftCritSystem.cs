using Content.Shared.ActionBlocker;
using Content.Shared.Buckle.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Stunnable;
using Robust.Shared.Timing;

namespace Content.Shared._Stalker_EN.SoftCrit;

/// <summary>
/// Splits the <see cref="MobState.Critical"/> damage range into two sub-phases:
/// <list type="bullet">
///   <item><b>Soft crit</b> (lower damage): entity is forced prone, can crawl at reduced speed, speech forced to whisper.</item>
///   <item><b>Hard crit</b> (higher damage): fully incapacitated (current vanilla behavior).</item>
/// </list>
/// This system dynamically adds/removes <see cref="STSoftCritComponent"/> based on total damage
/// relative to a computed hard-crit threshold. When present, the component uncancels movement events
/// that <see cref="MobStateSystem"/> blocks, and applies a severe speed penalty.
/// </summary>
public sealed class SharedSTSoftCritSystem : EntitySystem
{
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _speedModifier = default!;
    [Dependency] private readonly MobThresholdSystem _threshold = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STSoftCritEnabledComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<STSoftCritEnabledComponent, DamageChangedEvent>(OnDamageChanged);

        SubscribeLocalEvent<STSoftCritComponent, ComponentInit>(OnSoftCritInit);
        SubscribeLocalEvent<STSoftCritComponent, ComponentShutdown>(OnSoftCritShutdown);

        // Must run after MobStateSystem so we can uncancel the movement blocks it applies during crit.
        SubscribeLocalEvent<STSoftCritComponent, UpdateCanMoveEvent>(OnUpdateCanMove,
            after: [typeof(MobStateSystem)]);
        SubscribeLocalEvent<STSoftCritComponent, ChangeDirectionAttemptEvent>(OnChangeDirectionAttempt,
            after: [typeof(MobStateSystem)]);

        SubscribeLocalEvent<STSoftCritComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<STSoftCritComponent, STSoftCritSpeechEvent>(OnSoftCritSpeech);
    }

    private void OnMobStateChanged(Entity<STSoftCritEnabledComponent> entity, ref MobStateChangedEvent args)
    {
        // Server state is authoritative -- don't add/remove components during client state replay.
        if (_timing.ApplyingState)
            return;

        if (args.NewMobState == MobState.Critical)
            TryEnterSoftCrit(entity, entity.Comp);
        else
            RemCompDeferred<STSoftCritComponent>(entity);
    }

    private void OnDamageChanged(EntityUid uid, STSoftCritEnabledComponent enabled, DamageChangedEvent args)
    {
        if (_timing.ApplyingState)
            return;

        if (!TryComp<MobStateComponent>(uid, out var mobState) || mobState.CurrentState != MobState.Critical)
            return;

        if (TryComp<STSoftCritComponent>(uid, out var softCrit))
        {
            if (args.Damageable.TotalDamage >= softCrit.HardCritThreshold)
                RemCompDeferred<STSoftCritComponent>(uid);
        }
        else
        {
            TryEnterSoftCrit(uid, enabled);
        }
    }

    /// <summary>
    /// Attempts to add <see cref="STSoftCritComponent"/> if the entity's current damage
    /// falls within the soft crit range.
    /// </summary>
    private void TryEnterSoftCrit(EntityUid uid, STSoftCritEnabledComponent enabled)
    {
        if (!_threshold.TryGetThresholdForState(uid, MobState.Critical, out var critThreshold))
            return;

        if (!_threshold.TryGetThresholdForState(uid, MobState.Dead, out var deadThreshold))
            return;

        var hardCritThreshold = critThreshold.Value + (deadThreshold.Value - critThreshold.Value) * enabled.SoftCritFraction;

        if (!TryComp<DamageableComponent>(uid, out var damageable))
            return;

        if (damageable.TotalDamage >= hardCritThreshold)
            return;

        var softCrit = EnsureComp<STSoftCritComponent>(uid);
        softCrit.CrawlSpeedModifier = enabled.CrawlSpeedModifier;
        softCrit.HardCritThreshold = hardCritThreshold;
        Dirty(uid, softCrit);
    }

    private void OnSoftCritInit(Entity<STSoftCritComponent> entity, ref ComponentInit args)
    {
        _blocker.UpdateCanMove(entity);
        _speedModifier.RefreshMovementSpeedModifiers(entity);
    }

    private void OnSoftCritShutdown(Entity<STSoftCritComponent> entity, ref ComponentShutdown args)
    {
        _blocker.UpdateCanMove(entity);
        _speedModifier.RefreshMovementSpeedModifiers(entity);
    }

    private void OnUpdateCanMove(Entity<STSoftCritComponent> entity, ref UpdateCanMoveEvent args)
    {
        // ComponentShutdown triggers UpdateCanMove -- without this guard we'd re-enable movement
        // during teardown, fighting the transition back to hard crit or death.
        if (entity.Comp.LifeStage > ComponentLifeStage.Running)
            return;

        // Don't override movement blocks imposed by other systems (stun, buckle, etc.).
        if (HasComp<StunnedComponent>(entity))
            return;

        if (TryComp<BuckleComponent>(entity, out var buckle) && buckle.Buckled)
            return;

        if (HasComp<BlockMovementComponent>(entity))
            return;

        args.Uncancel();
    }

    private void OnChangeDirectionAttempt(Entity<STSoftCritComponent> entity, ref ChangeDirectionAttemptEvent args)
    {
        // Same LifeStage guard as OnUpdateCanMove -- prevent uncancel during teardown.
        if (entity.Comp.LifeStage > ComponentLifeStage.Running)
            return;

        args.Uncancel();
    }

    private void OnRefreshSpeed(Entity<STSoftCritComponent> entity, ref RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(entity.Comp.CrawlSpeedModifier);
    }

    private void OnSoftCritSpeech(Entity<STSoftCritComponent> entity, ref STSoftCritSpeechEvent args)
    {
        args.Override = true;
    }
}
