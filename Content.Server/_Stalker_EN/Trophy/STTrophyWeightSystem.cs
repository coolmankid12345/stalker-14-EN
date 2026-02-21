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
        var coords = _transform.GetMapCoordinates(uid);
        if (coords == MapCoordinates.Nullspace)
            return;

        CopyMobData(uid, trophy, coords);
    }

    /// <summary>
    /// Finds the closest mob within range and copies its weight (and shader data for variants).
    /// </summary>
    private void CopyMobData(EntityUid uid, STTrophyComponent trophy, MapCoordinates coords)
    {
        EntityUid? closestMob = null;
        var closestDist = float.MaxValue;

        foreach (var ent in _lookup.GetEntitiesInRange<STWeightComponent>(coords, SearchRadius))
        {
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
                $"No mob found within {SearchRadius}m of trophy {ToPrettyString(uid)} (quality={trophy.Quality})");
            return;
        }

        var weight = Comp<STWeightComponent>(closestMob.Value);
        trophy.SourceMobWeight = weight.Self;

        if (TryComp<STMobVariantComponent>(closestMob.Value, out var variant))
        {
            trophy.SpriteTint = variant.SpriteTint;
            trophy.SpriteSaturation = variant.SpriteSaturation;
            trophy.SpriteBrightness = variant.SpriteBrightness;
        }

        Dirty(uid, trophy);
    }
}
