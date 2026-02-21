using Content.Shared._Stalker.Weight;
using Content.Shared._Stalker_EN.MobVariant;
using Content.Shared._Stalker_EN.Trophy;
using Robust.Shared.Map;

namespace Content.Server._Stalker_EN.Trophy;

/// <summary>
/// Initializes trophy items at spawn time: propagates the source mob's weight and shader
/// parameters via spatial lookup. Prices are set directly in YAML prototypes via Currency.
/// </summary>
public sealed class STTrophyWeightSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private ISawmill _sawmill = default!;

    private const float SearchRadius = 2f;

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = Logger.GetSawmill("st.trophy");

        SubscribeLocalEvent<STTrophyComponent, ComponentStartup>(OnTrophyStartup);
    }

    private void OnTrophyStartup(EntityUid uid, STTrophyComponent trophy, ComponentStartup args)
    {
        if (trophy.Quality == STTrophyQuality.Common)
            return;

        var coords = _transform.GetMapCoordinates(uid);
        if (coords == MapCoordinates.Nullspace)
            return;

        CopyMobData(uid, trophy, coords);
    }

    /// <summary>
    /// Finds the closest variant mob within range and copies its weight and shader data.
    /// </summary>
    private void CopyMobData(EntityUid uid, STTrophyComponent trophy, MapCoordinates coords)
    {
        Entity<STMobVariantComponent>? closestMob = null;
        var closestDist = float.MaxValue;

        foreach (var ent in _lookup.GetEntitiesInRange<STMobVariantComponent>(coords, SearchRadius))
        {
            if (!HasComp<STWeightComponent>(ent))
                continue;

            var mobCoords = _transform.GetMapCoordinates(ent);
            var dist = (coords.Position - mobCoords.Position).Length();
            if (dist < closestDist)
            {
                closestDist = dist;
                closestMob = ent;
            }
        }

        if (closestMob == null)
        {
            _sawmill.Warning(
                $"No variant mob found within {SearchRadius}m of trophy {ToPrettyString(uid)} (quality={trophy.Quality})");
            return;
        }

        var variant = closestMob.Value.Comp;
        var weight = Comp<STWeightComponent>(closestMob.Value);

        trophy.SourceMobWeight = weight.Self;
        trophy.SpriteTint = variant.SpriteTint;
        trophy.SpriteSaturation = variant.SpriteSaturation;
        trophy.SpriteBrightness = variant.SpriteBrightness;
        Dirty(uid, trophy);
    }
}
