using Content.Server._Stalker_EN.Emission;
using Content.Shared.Damage;
using Content.Shared.Weather;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server._Stalker_EN.Emission;

[Flags]
public enum EmissionSoundsPlayed
{
    None = 0,
    Stage1 = 1 << 0,
    MainAmbient = 1 << 1,
    Stage2 = 1 << 2,
    Stage3 = 1 << 3
}

[RegisterComponent, Access(typeof(EmissionEventRuleSystem))]
public sealed partial class EmissionEventRuleComponent : Component
{
    [DataField]
    public SoundSpecifier SoundStage1 = new SoundPathSpecifier("/Audio/_Stalker_EN/Emissions/emission_stage1.ogg");

    [DataField]
    public SoundSpecifier SoundMainAmbient = new SoundPathSpecifier("/Audio/_Stalker_EN/Emissions/emission.ogg");

    [DataField]
    public SoundSpecifier SoundStage2 = new SoundPathSpecifier("/Audio/_Stalker_EN/Emissions/emission_stage2.ogg");

    [DataField]
    public SoundSpecifier SoundStage3 = new SoundPathSpecifier("/Audio/_Stalker_EN/Emissions/emission_stage3.ogg");

    [DataField]
    public string AnnouncementStage1 = "Attention stalkers, an emission is approaching! I repeat, an emission is approaching. Seek cover immediately.";

    [DataField]
    public string AnnouncementStage2 = "Attention stalkers, an emission will start any minute now. Find cover if you want to live.";

    [DataField]
    public string AnnouncementStage3 = "Stalkers, the emission is finally over. I hope you're all in one piece.";

    /// <summary>
    /// Custom sender name for emission announcements.
    /// </summary>
    [DataField]
    public string AnnouncementSender = "S.T.A.L.K.E.R Network";

    /// <summary>
    /// Delay before playing the main ambient track.
    /// </summary>
    [DataField]
    public TimeSpan MainAmbientDelay = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Delay before playing stage 2 sound and announcement.
    /// </summary>
    [DataField]
    public TimeSpan Stage2Delay = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Delay before the red hue effect starts.
    /// </summary>
    [DataField]
    public TimeSpan RedHueDelay = TimeSpan.FromSeconds(100);

    /// <summary>
    /// Delay before damage starts.
    /// </summary>
    [DataField]
    public TimeSpan DamageStartDelay = TimeSpan.FromSeconds(100);

    /// <summary>
    /// Delay before damage ends and stage 3 plays.
    /// </summary>
    [DataField]
    public TimeSpan DamageEndDelay = TimeSpan.FromSeconds(330);

    [DataField]
    public DamageSpecifier? Damage;

    [DataField]
    public TimeSpan DamageInterval = TimeSpan.FromSeconds(3);

    [DataField]
    public float ShakeStrength = 50f;

    [DataField]
    public Color EmissionColor = Color.FromHex("#FF0000FF");

    /// <summary>
    /// How many seconds before damage ends to start rain.
    /// </summary>
    [DataField]
    public TimeSpan RainStartBeforeEnd = TimeSpan.FromSeconds(40);

    /// <summary>
    /// Minimum rain duration.
    /// </summary>
    [DataField]
    public TimeSpan RainDurationMin = TimeSpan.FromSeconds(360);

    /// <summary>
    /// Maximum rain duration.
    /// </summary>
    [DataField]
    public TimeSpan RainDurationMax = TimeSpan.FromSeconds(600);

    /// <summary>
    /// Weather prototype to use for rain.
    /// </summary>
    [DataField]
    public ProtoId<WeatherPrototype> RainWeather = "Rain";

    // Runtime state
    public TimeSpan EventStartTime;

    public TimeSpan NextDamageTick;

    public EmissionSoundsPlayed SoundsPlayed = EmissionSoundsPlayed.None;

    public bool InDamageWindow;

    public bool RainStarted;

    public bool AmbientLightSet;
}
