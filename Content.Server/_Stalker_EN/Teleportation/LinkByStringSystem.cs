using Content.Server.SubFloor;
using Content.Shared.Teleportation.Components;
using Content.Shared.Teleportation.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Physics.Events;

namespace Content.Server._Stalker_EN.Teleportation;

/// <summary>
/// This is used for Linking portals via a common string
/// </summary>
public sealed class LinkByStringSystem : EntitySystem
{
    [Dependency] private readonly LinkedEntitySystem _link = default!;

    public override void Initialize()
    {
        base.Initialize();

        // ComponentInit fires after component creation but BEFORE data is loaded from save.
        SubscribeLocalEvent<LinkByStringComponent, ComponentInit>(OnInit);

        // ComponentHandleState fires when component state is applied (from save or network).
        SubscribeLocalEvent<LinkByStringComponent, ComponentHandleState>(OnHandleState);
    }

    private void OnInit(Entity<LinkByStringComponent> ent, ref ComponentInit args)
    {
        // For entities spawned during gameplay, data is already set.
        // For entities loaded from save, this fires BEFORE data is restored.

        if (ent.Comp.LinkString != null)
        {
            LinkEntity(ent);
            return;
        }

        if (ent.Comp.FallbackId)
        {
            var prototypeId = MetaData(ent.Owner).EntityPrototype?.ID;
            if (prototypeId != null)
            {
                TryLinkWithFallback(ent, prototypeId);
            }
        }
    }

    private void OnHandleState(Entity<LinkByStringComponent> ent, ref ComponentHandleState args)
    {
        // State was just applied from save or network sync.
        LinkEntity(ent);
    }

    /// <summary>
    /// Links an entity to its pair based on the effective link string.
    /// </summary>
    private void LinkEntity(Entity<LinkByStringComponent> ent)
    {
        if (ent.Comp.FallbackId && ent.Comp.LinkString == null)
        {
            var prototypeId = MetaData(ent.Owner).EntityPrototype?.ID;
            if (prototypeId != null)
            {
                TryLinkWithFallback(ent, prototypeId);
                return;
            }
        }
        TryLink(ent);
    }

    private void TryLinkWithFallback(Entity<LinkByStringComponent> ent, string fallbackString)
    {
        var query = EntityQueryEnumerator<LinkByStringComponent>();

        while (query.MoveNext(out var uid, out var link))
        {
            if (ent.Owner == uid)
                continue;

            var otherString = link.LinkString;
            if (otherString == null && link.FallbackId)
                otherString = MetaData(uid).EntityPrototype?.ID;

            if (fallbackString != otherString)
                continue;

            _link.TryLink(ent.Owner, uid);
        }
    }

    private void TryLink(Entity<LinkByStringComponent> ent)
    {
        var entString = GetEffectiveLinkString(ent);
        if (entString == null)
            return;

        var query = EntityQueryEnumerator<LinkByStringComponent>();

        while (query.MoveNext(out var uid, out var link))
        {
            if (ent.Owner == uid)
                continue;

            var otherString = GetEffectiveLinkString(uid, link);
            if (entString != otherString)
                continue;

            _link.TryLink(ent.Owner, uid);
        }
    }

    /// <summary>
    /// Gets the effective link string for an entity, considering FallbackId if LinkString is null.
    /// </summary>
    private string? GetEffectiveLinkString(Entity<LinkByStringComponent> ent)
    {
        if (ent.Comp.LinkString != null)
            return ent.Comp.LinkString;

        if (ent.Comp.FallbackId)
            return MetaData(ent.Owner).EntityPrototype?.ID;

        return null;
    }

    /// <summary>
    /// Gets the effective link string for an entity from its component.
    /// </summary>
    private string? GetEffectiveLinkString(EntityUid uid, LinkByStringComponent link)
    {
        if (link.LinkString != null)
            return link.LinkString;

        if (link.FallbackId)
            return MetaData(uid).EntityPrototype?.ID;

        return null;
    }
}
