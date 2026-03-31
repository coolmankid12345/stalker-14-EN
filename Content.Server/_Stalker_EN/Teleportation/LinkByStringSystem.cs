using Content.Server.SubFloor;
using Content.Shared.Random.Rules;
using Content.Shared.Teleportation.Components;
using Content.Shared.Teleportation.Systems;
using NetCord;
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

        SubscribeLocalEvent<LinkByStringComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<LinkByStringComponent, ComponentHandleState>(OnHandleState);
    }

    private void OnStartup(Entity<LinkByStringComponent> ent, ref ComponentStartup args)
    {
        TryLink(ent);
    }

    private void OnHandleState(Entity<LinkByStringComponent> ent, ref ComponentHandleState args)
    {
        TryLink(ent);
    }

    private void TryLink(Entity<LinkByStringComponent> ent)
    {
        // If we don't have any valid way of linking, then return
        var linkString = EvaluateLinkString(ent);
        if (linkString == null)
            return;

        var query = EntityQueryEnumerator<LinkByStringComponent>();

        while (query.MoveNext(out var uid, out var link))
        {
            if (ent.Owner == uid)
                continue;

            var otherLinkString = EvaluateLinkString((uid, link));

            if (otherLinkString == null)
                continue;

            if (otherLinkString != linkString)
                continue;

            _link.TryLink(ent.Owner, uid);
        }
    }

    private string? EvaluateLinkString(Entity<LinkByStringComponent> ent)
    {
        var linkString = ent.Comp.LinkString;

        if (ent.Comp.FallbackId &&
            linkString == null &&
            MetaData(ent.Owner).EntityPrototype != null)
        {
            linkString = MetaData(ent.Owner).EntityPrototype?.ID;
        }

        return linkString;
    }
}
