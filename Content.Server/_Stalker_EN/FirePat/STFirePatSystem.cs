using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Shared._Stalker_EN.FirePat;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;
using Content.Shared.Whitelist;
using Robust.Server.Audio;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Stalker_EN.FirePat;

/// <summary>
/// Server-side fire patting: adjusts fire stacks, shows popups, plays sound.
/// Hug suppression is handled by the shared base <see cref="SharedSTFirePatSystem"/>.
/// </summary>
public sealed class STFirePatSystem : SharedSTFirePatSystem
{
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly FlammableSystem _flammable = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    protected override void HandleFirePat(EntityUid user, EntityUid target)
    {
        if (!TryComp<STFirePatterComponent>(user, out var patter))
            return;

        if (!TryComp<FlammableComponent>(target, out var flammable) || !flammable.OnFire)
            return;

        var curTime = _timing.CurTime;
        if (curTime < patter.LastPatTime + patter.Cooldown)
            return;

        if (_whitelist.IsBlacklistPass(patter.Blacklist, target))
            return;

        patter.LastPatTime = curTime;

        _flammable.AdjustFireStacks(target, patter.Stacks, flammable);

        var targetName = Identity.Name(target, EntityManager, user);
        var userName = Identity.Name(user, EntityManager, target);

        _popup.PopupEntity(
            Loc.GetString("st-fire-pat-performer", ("target", targetName)),
            target, user);

        _popup.PopupEntity(
            Loc.GetString("st-fire-pat-target", ("user", userName)),
            target, target);

        _popup.PopupEntity(
            Loc.GetString("st-fire-pat-others", ("user", userName), ("target", targetName)),
            target,
            Filter.PvsExcept(target).RemovePlayerByAttachedEntity(user),
            true);

        if (patter.Sound != null)
            _audio.PlayPvs(patter.Sound, target);
    }
}
