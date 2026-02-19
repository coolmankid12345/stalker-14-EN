using Content.Server.Popups;
using Content.Shared._Stalker.Medical.CPR;
using Content.Shared.Administration.Logs;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Stalker.Medical.CPR;

public sealed class STCPRSystem : SharedSTCPRSystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STCPRComponent, STCPRDoAfterEvent>(OnCPRDoAfter);
    }

    protected override void AttemptCPR(EntityUid uid, STCPRComponent comp, InteractHandEvent args)
    {
        var user = args.User;
        var target = uid;

        // Self-CPR: show popup only
        if (user == target)
        {
            _popup.PopupEntity(Loc.GetString("st-cpr-fail-self"), target, user);
            return;
        }

        // Start popups
        _popup.PopupEntity(
            Loc.GetString("st-cpr-start-performer", ("target", Identity.Entity(target, EntityManager))),
            target, user);
        _popup.PopupEntity(
            Loc.GetString("st-cpr-start-others",
                ("performer", Identity.Entity(user, EntityManager)),
                ("target", Identity.Entity(target, EntityManager))),
            target, Filter.PvsExcept(user), true);

        // Start DoAfter
        var doAfterArgs = new DoAfterArgs(EntityManager, user, comp.DoAfterDuration,
            new STCPRDoAfterEvent(), target, target)
        {
            NeedHand = true,
            BreakOnMove = true,
            BreakOnHandChange = true,
            BlockDuplicate = true,
        };
        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void OnCPRDoAfter(EntityUid uid, STCPRComponent comp, STCPRDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;

        var target = uid;
        var user = args.User;

        // Re-validate target state
        if (!TryComp<MobStateComponent>(target, out var mobState))
            return;

        if (_mobState.IsAlive(target, mobState))
            return;

        // Check cooldown again (another player may have done CPR during our DoAfter)
        if (TryComp<STCPRReceivedComponent>(target, out var received))
        {
            if (_timing.CurTime < received.LastCPRTime + TimeSpan.FromSeconds(comp.CooldownSeconds))
            {
                _popup.PopupEntity(
                    Loc.GetString("st-cpr-fail-rhythm", ("target", Identity.Entity(target, EntityManager))),
                    target, user);
                _popup.PopupEntity(
                    Loc.GetString("st-cpr-fail-rhythm-others",
                        ("performer", Identity.Entity(user, EntityManager)),
                        ("target", Identity.Entity(target, EntityManager))),
                    target, Filter.PvsExcept(user), true);
                return;
            }
        }

        // Heal asphyxiation damage
        var damage = new DamageSpecifier();
        damage.DamageDict["Asphyxiation"] = FixedPoint2.New(-comp.HealAmount);
        _damageable.TryChangeDamage(target, damage, ignoreResistances: true, interruptsDoAfters: false, origin: user);

        // Update cooldown
        var receivedComp = EnsureComp<STCPRReceivedComponent>(target);
        receivedComp.LastCPRTime = _timing.CurTime;

        // Success popups
        _popup.PopupEntity(
            Loc.GetString("st-cpr-success-performer",
                ("target", Identity.Entity(target, EntityManager)),
                ("seconds", (int) comp.CooldownSeconds)),
            target, user);
        _popup.PopupEntity(
            Loc.GetString("st-cpr-success-others",
                ("performer", Identity.Entity(user, EntityManager)),
                ("target", Identity.Entity(target, EntityManager))),
            target, Filter.PvsExcept(user), true);

        // Admin log
        _adminLogger.Add(LogType.Healed,
            $"{ToPrettyString(user):user} performed CPR on {ToPrettyString(target):target}, healing {comp.HealAmount} asphyxiation damage");
    }
}
