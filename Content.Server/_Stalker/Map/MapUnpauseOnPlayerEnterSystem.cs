using Content.Shared._Stalker.Teleport;
using Robust.Server.Player;
using Robust.Shared.Map;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;

namespace Content.Server._Stalker.Map;

/// <summary>
/// Unpauses maps when a player enters them via parent change, teleport, or session attach.
/// Does not unpause uninitialized maps (mapping mode).
/// </summary>
public sealed class MapUnpauseOnPlayerEnterSystem : EntitySystem
{
    [Dependency] private readonly IMapManager _mapMan = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<EntParentChangedMessage>(OnEntParentChanged);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<AfterEntityTeleportedEvent>(OnAfterTeleport);
    }

    private void TryUnpauseMap(MapId mapId)
    {
        if (mapId == MapId.Nullspace)
            return;

        if (!_mapMan.IsMapPaused(mapId))
            return;

        if (!_mapMan.IsMapInitialized(mapId))
            return;

        _mapMan.SetMapPaused(mapId, false);
    }

    private void OnEntParentChanged(ref EntParentChangedMessage args)
    {
        foreach (var session in _playerManager.Sessions)
        {
            if (session.AttachedEntity == args.Entity)
            {
                var xform = EntityManager.GetComponent<TransformComponent>(args.Entity);
                TryUnpauseMap(xform.MapID);
                break;
            }
        }
    }

    private void OnPlayerAttached(PlayerAttachedEvent args)
    {
        var xform = EntityManager.GetComponent<TransformComponent>(args.Entity);
        TryUnpauseMap(xform.MapID);
    }

    private void OnAfterTeleport(ref AfterEntityTeleportedEvent args)
    {
        if (!EntityManager.HasComponent<ActorComponent>(args.EntityUid))
            return;

        TryUnpauseMap(args.Destination);
    }
}
