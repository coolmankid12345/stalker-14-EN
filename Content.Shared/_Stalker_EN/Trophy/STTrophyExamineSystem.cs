using Content.Shared.Examine;

namespace Content.Shared._Stalker_EN.Trophy;

/// <summary>
/// Adds a colored quality label and source mob weight to the examine text of trophy items.
/// </summary>
public sealed class STTrophyExamineSystem : EntitySystem
{
    private const string ColorCommon = "#808080";
    private const string ColorUncommon = "#00FF00";
    private const string ColorRare = "#4169E1";
    private const string ColorLegendary = "#FFD700";

    private const string LocQualityCommon = "st-trophy-quality-common";
    private const string LocQualityUncommon = "st-trophy-quality-uncommon";
    private const string LocQualityRare = "st-trophy-quality-rare";
    private const string LocQualityLegendary = "st-trophy-quality-legendary";

    private const string LocExamineQuality = "st-trophy-examine-quality";
    private const string LocExamineWeight = "st-trophy-examine-weight";

    private static readonly Dictionary<STTrophyQuality, (string Color, string LocKey)> QualityData = new()
    {
        { STTrophyQuality.Common, (ColorCommon, LocQualityCommon) },
        { STTrophyQuality.Uncommon, (ColorUncommon, LocQualityUncommon) },
        { STTrophyQuality.Rare, (ColorRare, LocQualityRare) },
        { STTrophyQuality.Legendary, (ColorLegendary, LocQualityLegendary) },
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STTrophyComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(EntityUid uid, STTrophyComponent trophy, ExaminedEvent args)
    {
        // Quality label for all trophies including Common ("Standard").
        if (QualityData.TryGetValue(trophy.Quality, out var data))
        {
            var qualityName = Loc.GetString(data.LocKey);

            args.PushMarkup(Loc.GetString(LocExamineQuality,
                ("color", data.Color),
                ("quality", qualityName)));
        }

        // Source mob weight.
        if (trophy.SourceMobWeight > 0f)
        {
            args.PushMarkup(Loc.GetString(LocExamineWeight,
                ("weight", $"{trophy.SourceMobWeight:0.0}")));
        }
    }
}
