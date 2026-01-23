using Content.Server._Stalker_EN.Emission;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Shared.GameTicking.Components;
using Content.Shared.Weather;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Stalker_EN.Weather;

public sealed class WeatherSchedulerRuleSystem : GameRuleSystem<WeatherSchedulerRuleComponent>
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedWeatherSystem _weather = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;

    protected override void Started(EntityUid uid, WeatherSchedulerRuleComponent component,
        GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);
        var initialDelay = component.ChangeInterval.Next(_random);
        component.NextWeatherChangeTime = _timing.CurTime + TimeSpan.FromSeconds(initialDelay);
    }

    protected override void ActiveTick(EntityUid uid, WeatherSchedulerRuleComponent component,
        GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        if (component.PauseDuringEmission && IsEmissionActive())
        {
            component.WaitingForEmission = true;
            return;
        }

        if (component.WaitingForEmission)
        {
            component.WaitingForEmission = false;
            component.NextWeatherChangeTime = _timing.CurTime + TimeSpan.FromSeconds(60);
            return;
        }

        if (_timing.CurTime < component.NextWeatherChangeTime)
            return;

        ChangeWeather(component);
        var nextInterval = component.ChangeInterval.Next(_random);
        component.NextWeatherChangeTime = _timing.CurTime + TimeSpan.FromSeconds(nextInterval);
    }

    private void ChangeWeather(WeatherSchedulerRuleComponent component)
    {
        var selectedWeather = PickWeather(component);
        var duration = component.WeatherDuration.Next(_random);
        var endTime = _timing.CurTime + TimeSpan.FromSeconds(duration);

        component.CurrentWeather = selectedWeather;

        WeatherPrototype? weatherProto = null;
        if (selectedWeather.HasValue)
            weatherProto = _protoManager.Index(selectedWeather.Value);

        var query = EntityQueryEnumerator<MapComponent>();
        while (query.MoveNext(out _, out var mapComp))
        {
            _weather.SetWeather(mapComp.MapId, weatherProto, endTime);
        }
    }

    private ProtoId<WeatherPrototype>? PickWeather(WeatherSchedulerRuleComponent component)
    {
        var totalWeight = component.ClearWeatherWeight;
        foreach (var weight in component.WeatherPool.Values)
            totalWeight += weight;

        var rand = _random.NextFloat() * totalWeight;
        var accumulated = component.ClearWeatherWeight;

        if (rand < accumulated)
            return null;

        foreach (var (weatherId, weight) in component.WeatherPool)
        {
            accumulated += weight;
            if (rand < accumulated)
                return weatherId;
        }

        return null;
    }

    private bool IsEmissionActive()
    {
        var query = EntityQueryEnumerator<EmissionEventRuleComponent, ActiveGameRuleComponent>();
        return query.MoveNext(out _, out _, out _);
    }
}
