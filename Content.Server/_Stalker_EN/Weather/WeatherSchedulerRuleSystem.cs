using Content.Server._Stalker.Map;
using Content.Server._Stalker.StationEvents.Components;
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
    [Dependency] private readonly STMapKeySystem _mapKey = default!;

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
        var curTime = _timing.CurTime;

        // Only check emission when close to weather change OR periodically during emission
        if (component.PauseDuringEmission)
        {
            var shouldCheck = curTime >= component.NextEmissionCheckTime ||
                              curTime >= component.NextWeatherChangeTime - TimeSpan.FromSeconds(5);

            if (shouldCheck)
            {
                component.CachedEmissionActive = IsEmissionActive();
                component.NextEmissionCheckTime = curTime + TimeSpan.FromSeconds(2);
            }

            if (component.CachedEmissionActive)
            {
                component.WaitingForEmission = true;
                return;
            }
        }

        if (component.WaitingForEmission)
        {
            component.WaitingForEmission = false;
            component.NextWeatherChangeTime = curTime + TimeSpan.FromSeconds(60);
            return;
        }

        if (curTime < component.NextWeatherChangeTime)
            return;

        ChangeWeather(component);
        var nextInterval = component.ChangeInterval.Next(_random);
        component.NextWeatherChangeTime = curTime + TimeSpan.FromSeconds(nextInterval);
    }

    private void ChangeWeather(WeatherSchedulerRuleComponent component)
    {
        var baseDuration = component.WeatherDuration.Next(_random);

        var query = EntityQueryEnumerator<MapComponent>();
        while (query.MoveNext(out var mapUid, out var mapComp))
        {
            // Try to get STMapKey for this map
            string? mapKey = null;
            if (TryComp<STMapKeyComponent>(mapUid, out var keyComp))
                mapKey = keyComp.Value;

            // Check for per-map override
            MapWeatherOverride? mapOverride = null;
            if (mapKey != null && component.MapOverrides.TryGetValue(mapKey, out mapOverride))
            {
                if (!mapOverride.WeatherEnabled)
                    continue; // Skip this map entirely
            }

            // Fallback: Skip safe zones (underground maps without STMapKey override)
            if (mapOverride == null && HasComp<StalkerSafeZoneComponent>(mapUid))
                continue;

            // Pick weather using override pool or default
            var weatherProto = PickWeatherForMap(component, mapOverride);

            // Calculate duration with multiplier
            var durationMultiplier = mapOverride?.WeatherDurationMultiplier ?? 1.0f;
            var duration = TimeSpan.FromSeconds(baseDuration * durationMultiplier);
            var endTime = _timing.CurTime + duration;

            WeatherPrototype? proto = null;
            if (weatherProto.HasValue)
                proto = _protoManager.Index(weatherProto.Value);

            _weather.SetWeather(mapComp.MapId, proto, endTime);
        }

        // Update current weather (using default pool for tracking)
        component.CurrentWeather = PickWeather(component);
    }

    private ProtoId<WeatherPrototype>? PickWeatherForMap(
        WeatherSchedulerRuleComponent component,
        MapWeatherOverride? mapOverride)
    {
        // Use override pool if available, otherwise default
        var weatherPool = mapOverride?.WeatherPool ?? component.WeatherPool;
        var clearWeight = mapOverride?.ClearWeatherWeight ?? component.ClearWeatherWeight;

        var totalWeight = clearWeight;
        foreach (var weight in weatherPool.Values)
            totalWeight += weight;

        var rand = _random.NextFloat() * totalWeight;
        var accumulated = clearWeight;

        if (rand < accumulated)
            return null;

        foreach (var (weatherId, weight) in weatherPool)
        {
            accumulated += weight;
            if (rand < accumulated)
                return weatherId;
        }

        return null;
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
