using Content.Shared.Clothing;

namespace Content.Server._Stalker_EN.PsyHelmet;

/// <summary>
/// Adds psy protection to those who equip the psy helmets.
/// </summary>
public sealed class PsyHelmetSystem : EntitySystem
{
    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<PsyHelmetComponent, ClothingGotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<PsyHelmetComponent, ClothingGotUnequippedEvent>(OnUnequipped);
    }

    private void OnEquipped(EntityUid uid, PsyHelmetComponent comp, ClothingGotEquippedEvent args)
    {
        EnsureComp<PsyHelmetComponent>(args.Wearer);
    }

    private void OnUnequipped(EntityUid uid, PsyHelmetComponent comp, ClothingGotUnequippedEvent args)
    {
        RemCompDeferred<PsyHelmetComponent>(args.Wearer);
    }
}
