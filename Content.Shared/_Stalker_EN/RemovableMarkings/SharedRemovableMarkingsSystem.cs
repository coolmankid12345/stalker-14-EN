using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.IdentityManagement;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.Systems;
using Content.Shared.Verbs;
using Robust.Shared.Collections;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Stalker_EN.RemovableMarkings;

public abstract class SharedRemovableMarkingsSystem : EntitySystem
{
    [Dependency] private readonly INetManager _netManager = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly SharedToolSystem _toolSystem = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    [Dependency] private readonly SharedStunSystem _stunSystem = default!;
    [Dependency] private readonly MobStateSystem _mobStateSystem = default!;

    private EntityQuery<HumanoidAppearanceComponent> _humanoidAppearanceQuery;
    private EntityQuery<ToolComponent> _toolQuery;

    public override void Initialize()
    {
        base.Initialize();

        _humanoidAppearanceQuery = GetEntityQuery<HumanoidAppearanceComponent>();
        _toolQuery = GetEntityQuery<ToolComponent>();

        SubscribeLocalEvent<RemovableMarkingsComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAltVerbs);
        SubscribeLocalEvent<RemovableMarkingsComponent, MarkingRemovalDoAfterEvent>(OnMarkingRemovalDoAfterFinished);
    }

    /// <summary>
    ///     Forces <see cref="RemovableMarkingsComponent.RemovalEmote"/> to be done on the specified
    ///         entity. Assumes it is notnull.
    /// </summary>
    protected abstract void DoRemovalEmote(Entity<RemovableMarkingsComponent> entity);

    private void OnGetAltVerbs(Entity<RemovableMarkingsComponent> entity, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract ||
            !args.CanAccess ||
            args.Using is not { } toolUid)
            return;

        if (!_humanoidAppearanceQuery.TryGetComponent(entity, out var humanoidAppearanceComponent) ||
            !_toolQuery.TryGetComponent(toolUid, out var toolComponent))
            return;

        // i hate this approach, and so do you, but there is no other way
        var validMarkingsFound = false;
        foreach (var (_, markingList) in humanoidAppearanceComponent.MarkingSet.Markings)
        {
            foreach (var marking in markingList)
            {
                if (!_prototypeManager.TryIndex<MarkingPrototype>(marking.MarkingId, out var markingPrototype) ||
                    !entity.Comp.CompatibleMarkingFlags.HasFlag(markingPrototype.RemovabilityFlags))
                    continue;

                validMarkingsFound = true;
                break;
            }

            if (validMarkingsFound)
                break;
        }

        if (!validMarkingsFound)
            return;

        // args.User is dereferenced like this because you can't use members of a `ref` parameter in a lambda expression
        // (see AlternativeVerb.Act down below)
        var userUid = args.User;

        var toolQualityPrototype = _prototypeManager.Index(entity.Comp.RequiredToolQuality);
        var altVerb = new AlternativeVerb
        {
            Priority = 2,
            IconEntity = GetNetEntity(toolUid),
            Text = Loc.GetString("removable-markings-verb-text"),
            Impact = LogImpact.Medium,
            CloseMenu = true
        };

        altVerb.Act = () => OnRemoveVerbActed(entity, userUid, toolUid, altVerb);

        if (userUid == entity.Owner)
        {
            altVerb.Disabled = true;
            altVerb.Message = Loc.GetString("removable-markings-verb-cant-target-yourself");
        }
        else
        {
            if (!_toolSystem.HasQuality(toolUid, entity.Comp.RequiredToolQuality, tool: toolComponent))
            {
                altVerb.Disabled = true;
                altVerb.Message = Loc.GetString("removable-markings-verb-invalid-tool", ("quality", Loc.GetString(toolQualityPrototype.Name)));
            }
            else
                altVerb.Message = Loc.GetString("removable-markings-verb-info", ("type", Enum.GetName(entity.Comp.CompatibleMarkingFlags) ?? Loc.GetString("removable-markings-verb-type-unknown")));
        }

        args.Verbs.Add(altVerb);
    }

    /// <summary>
    ///     Assumes that the user is the client.
    /// </summary>
    private void PopupUserOthersAndTarget(EntityUid targetUid, EntityUid userUid, string prefix, PopupType popupType)
    {
        var userValueTuple = ((string, object))("userName", Identity.Name(userUid, EntityManager));
        var targetValueTuple = ((string, object))("targetName", Identity.Name(targetUid, EntityManager));

        // User
        _popupSystem.PopupClient(
            Loc.GetString(prefix + "-user", targetValueTuple),
            userUid,
            userUid,
            type: popupType
        );

        // Others
        _popupSystem.PopupEntity(
            Loc.GetString(prefix + "-others", userValueTuple, targetValueTuple),
            userUid,
            Filter.PvsExcept(userUid, entityManager: EntityManager).RemovePlayerByAttachedEntity(targetUid),
            true,
            type: popupType
        );

        // Target
        _popupSystem.PopupEntity(
            Loc.GetString(prefix + "-target", userValueTuple),
            targetUid, // only target gets the popup on themselves
            Filter.Empty().FromEntities(targetUid), // because of doafter jank, thank you [REDACTED] very much
            false,
            type: popupType
        );
    }

    private void OnRemoveVerbActed(Entity<RemovableMarkingsComponent> targetEntity, EntityUid userUid, EntityUid toolUid, AlternativeVerb verb)
    {
        // !!HOLY SHIT!!; altverbs that are disabled get mispredicted if you activate them via alt-interact.
        if (verb.Disabled)
            return;

        // i hate this
        // i could actually use SharedToolSystem.UseTool but i REALLY can't be bothered and i can't understand if it works
        var doAfterEventArgs = new DoAfterArgs(
            EntityManager,
            userUid,
            targetEntity.Comp.RemovalDoAfterLength,
            new MarkingRemovalDoAfterEvent(),
            targetEntity,
            target: targetEntity,
            used: toolUid
        )
        {
            BreakOnMove = true,
            BreakOnDamage = true,

            BreakOnDropItem = true, // lets hope this covers someone taking the item out of the user's hand lol
            BreakOnHandChange = true,

            NeedHand = true,
        };

        if (!_doAfterSystem.TryStartDoAfter(doAfterEventArgs) ||
            !_gameTiming.IsFirstTimePredicted)
            return;

        PopupUserOthersAndTarget(targetEntity, userUid, "removable-markings-removal-started", PopupType.MediumCaution);
    }

    private void OnMarkingRemovalDoAfterFinished(Entity<RemovableMarkingsComponent> entity, ref MarkingRemovalDoAfterEvent args)
    {
        if (args.Cancelled ||
            !_gameTiming.IsFirstTimePredicted)
            return;

        TryForcefullyRemoveValidMarkings(entity);

        _damageableSystem.TryChangeDamage(entity.Owner, entity.Comp.RemovalDamage, tool: args.Used);
        if (entity.Comp.RemovalEmoteId != null &&
            _mobStateSystem.IsAlive(entity.Owner))
            DoRemovalEmote(entity);

        if (entity.Comp.RemovalKnockdownDuration is { } removalKnockdownDuration)
            _stunSystem.TryKnockdown(entity.Owner, removalKnockdownDuration, false);

        if (_toolQuery.TryGetComponent(args.Used, out var toolComponent))
            _toolSystem.PlayToolSound(args.Used.Value, toolComponent, user: args.User);

        if (entity.Comp.RemovedEntity is { } removedEntityId &&
            _netManager.IsServer /* TODO LCDC: use PredictedSpawn when RT update */)
        {
            // try pickup, default to dropping on floor
            var removedUid = Spawn(removedEntityId, Transform(entity).Coordinates);
            _handsSystem.TryPickupAnyHand(args.User, removedUid);
        }

        PopupUserOthersAndTarget(entity, args.User, "removable-markings-removal-finished", PopupType.LargeCaution);
    }

    /// <summary>
    ///     Forcefully removes markings. Obviously. Does related events etcetera.
    /// </summary>
    /// <returns>Whether anything happened.</returns>
    public bool TryForcefullyRemoveValidMarkings(Entity<RemovableMarkingsComponent> entity, HumanoidAppearanceComponent? humanoidAppearanceComponent = null)
    {
        if (!Resolve(entity.Owner, ref humanoidAppearanceComponent))
            return false;

        // this is absolute insanity
        var markingsToRemove = new ValueList<(MarkingCategories category, string id)>();
        foreach (var (category, markingList) in humanoidAppearanceComponent.MarkingSet.Markings)
        {
            foreach (var marking in markingList)
            {
                if (!_prototypeManager.TryIndex<MarkingPrototype>(marking.MarkingId, out var markingPrototype) ||
                    !entity.Comp.CompatibleMarkingFlags.HasFlag(markingPrototype.RemovabilityFlags))
                    continue;

                markingsToRemove.Add((category, marking.MarkingId));
            }
        }

        if (markingsToRemove.Count == 0)
            return false;

        foreach (var (markingCategory, markingId) in markingsToRemove)
            humanoidAppearanceComponent.MarkingSet.Remove(markingCategory, markingId);

        Dirty(entity.Owner, humanoidAppearanceComponent);
        return true;
    }
}
