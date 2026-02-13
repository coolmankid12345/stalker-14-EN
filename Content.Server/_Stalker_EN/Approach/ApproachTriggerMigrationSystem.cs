using Content.Server._Stalker.ApproachEmitter;
using Content.Server._Stalker.ApproachTrigger;
using Content.Shared.Trigger.Components.Triggers;
using Content.Shared.Trigger.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Physics.Components;
using Robust.Shared.Utility;

namespace Content.Server._Stalker_EN.Approach;

/*
    This was done to avoid absolutely insane amount of YAML changes.

    Did you know that some ApproachTriggers in YAML specify a whitelist
    but the component doesnt even have them, and nobody knows this because
    people think testfails are normal

    I decided not to fix that so as to preserve current behaviour
    This system is done weirdly so as to preserve backward compatibility
*/

public sealed class ApproachTriggerMigrationSystem : EntitySystem
{
    [Dependency] private readonly PhysicsSystem _physicsSystem = default!;
    [Dependency] private readonly TransformSystem _transformSystem = default!;

    private EntityQuery<ApproachEmitterComponent> _emitterQuery;
    private EntityQuery<ApproachTriggerComponent> _triggerQuery;

    public override void Initialize()
    {
        base.Initialize();

        _emitterQuery = GetEntityQuery<ApproachEmitterComponent>();
        _triggerQuery = GetEntityQuery<ApproachTriggerComponent>();

        SubscribeLocalEvent<ApproachTriggerComponent, MapInitEvent>(OnApproachTriggerMapInit);
        SubscribeLocalEvent<ApproachTriggerComponent, AttemptTriggerOnProximityStartColliding>(OnApproachTriggerAttemptTriggerProximity);
        SubscribeLocalEvent<ApproachEmitterComponent, AttemptTriggerOnProximityStartColliding>(OnApproachEmitterAttemptTriggerProximity);
    }

    private void OnApproachTriggerMapInit(Entity<ApproachTriggerComponent> entity, ref MapInitEvent args)
    {
        if (HasComp<TriggerOnProximityComponent>(entity))
        {
            DebugTools.Assert("Dont have TriggerOnProximityComponent on something with ApproachTriggerComponent");
            return;
        }

        var addedComp = EntityManager.ComponentFactory.GetComponent<TriggerOnProximityComponent>();
        addedComp.Shape.Radius = entity.Comp.Range;

        var physicsComponent = EnsureComp<PhysicsComponent>(entity);
        AddComp(entity.Owner, addedComp);

        _physicsSystem.SetCanCollide(entity.Owner, true, body: physicsComponent);
    }

    private void OnApproachTriggerAttemptTriggerProximity(Entity<ApproachTriggerComponent> entity, ref AttemptTriggerOnProximityStartColliding args)
    {
        // only allowed if was already allowed AND trigger was enabled
        args.Allowed &= entity.Comp.Enabled;

        // only allowed if colliding also has ApproachEmitterComponent
        if (args.Allowed && !_emitterQuery.HasComponent(args.CollidingUid))
            args.Allowed = false;
    }

    // try to calculate minrange
    private void OnApproachEmitterAttemptTriggerProximity(Entity<ApproachEmitterComponent> entity, ref AttemptTriggerOnProximityStartColliding args)
    {
        if (!_triggerQuery.TryGetComponent(args.TriggerUid, out var approachTriggerComponent))
            return;

        if (approachTriggerComponent.MinRange <= 0f)
            return;

        var triggerWorldCoords = _transformSystem.GetWorldPosition(Transform(args.TriggerUid));
        var collidingWorldCoords = _transformSystem.GetWorldPosition(Transform(args.CollidingUid));

        // outside of min range so skip
        if ((collidingWorldCoords - triggerWorldCoords).LengthSquared() < approachTriggerComponent.MinRange * approachTriggerComponent.MinRange)
            args.Allowed = false;
    }
}
