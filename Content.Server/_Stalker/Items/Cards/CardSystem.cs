using Robust.Shared.Player;
using Content.Shared.Verbs;
using Robust.Shared.Utility;
using Content.Shared.Database;
using Content.Shared._Stalker.Cards;
using Content.Shared._Stalker.Cards.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Hands.Components;

namespace Content.Server._Stalker.Cards;

public sealed class CardSystem : SharedCardSystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShitCardComponent, GetVerbsEvent<AlternativeVerb>>(OnSetTransferVerbs);
        SubscribeLocalEvent<ShitCardComponent, UseInHandEvent>(OnAfterInteract);
    }
    private void OnSetTransferVerbs(EntityUid uid, ShitCardComponent component, GetVerbsEvent<AlternativeVerb> args)
    {

        if (!EntityManager.TryGetComponent(args.User, out ActorComponent? actor))
            return;

        var player = actor.PlayerSession;

        if (HasComp<HandsComponent>(args.User))
        {
            args.Verbs.Add(new AlternativeVerb()
            {
                Text = Loc.GetString("ent-ST-TurnCard"),
                ClientExclusive = true,
                Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/dot.svg.192dpi.png")),
                Act = () => TurnOver(uid, component),
                Impact = LogImpact.Medium
            });
        }
    }
    private void OnAfterInteract(EntityUid uid, ShitCardComponent component, UseInHandEvent args)
    {
        TurnOver(uid, component);
    }
}
