using Content.Shared._Stalker.Cards.Components;
using Content.Shared._Stalker.Cards;
using Robust.Client.GameObjects;

namespace Content.Client._Stalker.Cards;

public sealed class CardSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShitCardComponent, AppearanceChangeEvent>(OnAppearanceChange);
    }

    private void OnAppearanceChange(EntityUid uid, ShitCardComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (_appearance.TryGetData<bool>(uid, CardVisuals.Reserve, out var reserveCard, args.Component))
        {
            if (reserveCard)
                args.Sprite.LayerSetState(0, component.ReserveState);
            else
                args.Sprite.LayerSetState(0, component.State);
        }
    }
}
