using Content.Shared._Stalker.Weight;
using Content.Shared._Stalker_EN.MobVariant;
using Content.Shared._Stalker_EN.Trophy;
using Robust.Shared.Map;

namespace Content.Server._Stalker_EN.Trophy;

/// <summary>
/// Propagates the source mob's weight and shader parameters to trophy items at spawn time.
/// When a variant mob is butchered, trophy items spawn at the mob's position.
/// At ComponentStartup the mob still exists, so we find it via EntityLookup
/// and copy its weight and visual data to the trophy.
/// </summary>
public sealed class STTrophyWeightSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private const float SearchRadius = 2f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STTrophyComponent, ComponentStartup>(OnTrophyStartup);
    }

    private void OnTrophyStartup(EntityUid uid, STTrophyComponent trophy, ComponentStartup args)
    {
        if (trophy.Quality == STTrophyQuality.Common)
            return;

        var coords = _transform.GetMapCoordinates(uid);
        if (coords == MapCoordinates.Nullspace)
            return;

        foreach (var ent in _lookup.GetEntitiesInRange<STMobVariantComponent>(coords, SearchRadius))
        {
            if (!TryComp<STWeightComponent>(ent, out var weight))
                continue;

            var variant = ent.Comp;

            trophy.SourceMobWeight = weight.Self;
            trophy.SpriteTint = variant.SpriteTint;
            trophy.SpriteSaturation = variant.SpriteSaturation;
            trophy.SpriteBrightness = variant.SpriteBrightness;
            Dirty(uid, trophy);
            break;
        }
    }
}
