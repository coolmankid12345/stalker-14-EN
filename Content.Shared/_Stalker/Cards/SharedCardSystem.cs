
using Content.Shared._Stalker.Cards.Components;

namespace Content.Shared._Stalker.Cards;

public abstract class SharedCardSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public void TurnOver(EntityUid uid, ShitCardComponent component)
    {
        if (component.IsReserve)
        {
            component.IsReserve = false;
            _appearance.SetData(uid, CardVisuals.Reserve, false);
        }
        else
        {
            component.IsReserve = true;
            _appearance.SetData(uid, CardVisuals.Reserve, true);
        }
    }

}
