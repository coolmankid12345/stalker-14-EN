using System.Numerics;
using Content.Server._Stalker.MapLightSimulation;
using Content.Server._Stalker.StationEvents.Components;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.StationEvents.Events;
using Content.Shared._Stalker_EN.Emission;
using Content.Shared.Camera;
using Content.Shared.Damage;
using Content.Shared.GameTicking.Components;
using Content.Shared.Weather;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Stalker_EN.Emission;

public sealed class EmissionEventRuleSystem : StationEventSystem<EmissionEventRuleComponent>
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedCameraRecoilSystem _cameraRecoil = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    [Dependency] private readonly MapDaySystem _mapDay = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly SharedWeatherSystem _weather = default!;
    [Dependency] private readonly EmissionLightningSystem _emissionLightningSystem = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;

    protected override void Added(EntityUid uid, EmissionEventRuleComponent component, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        base.Added(uid, component, gameRule, args);

        component.EventStartTime = _timing.CurTime;
        component.SoundsPlayed = EmissionSoundsPlayed.None;
        component.Stage = EmissionStage.Stage1;
        component.RainStarted = false;
        component.AmbientLightSet = false;

        PlayGlobalSound(component.SoundStage1);
        SendAnnouncement(component.AnnouncementStage1, component.AnnouncementSender);
        component.SoundsPlayed |= EmissionSoundsPlayed.Stage1;
    }

    protected override void Ended(EntityUid uid, EmissionEventRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        base.Ended(uid, component, gameRule, args);
        // Ambient light is cleared at stage 3 (T+330), not at event end
    }

    protected override void ActiveTick(EntityUid uid, EmissionEventRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        var elapsed = _timing.CurTime - component.EventStartTime;

        if (!component.SoundsPlayed.HasFlag(EmissionSoundsPlayed.MainAmbient) &&
            elapsed >= component.MainAmbientDelay)
        {
            PlayGlobalSound(component.SoundMainAmbient);
            component.SoundsPlayed |= EmissionSoundsPlayed.MainAmbient;
        }

        // Red hue timing is intentionally separate from main ambient audio
        if (!component.AmbientLightSet && elapsed >= component.RedHueDelay)
        {
            SetAmbientLightColor(component);
            component.AmbientLightSet = true;
        }

        if (!component.SoundsPlayed.HasFlag(EmissionSoundsPlayed.Stage2) &&
            elapsed >= component.Stage2Delay)
        {
            PlayGlobalSound(component.SoundStage2);
            SendAnnouncement(component.AnnouncementStage2, component.AnnouncementSender);
            component.SoundsPlayed |= EmissionSoundsPlayed.Stage2;
        }

        if (component.Stage == EmissionStage.Stage1 && elapsed >= component.DamageStartDelay)
        {
            component.Stage = EmissionStage.Stage2;
            component.NextDamageTick = _timing.CurTime;

            if (component.LightningEffectProtoId is { } lightningEffectProtoId)
            {
                _emissionLightningSystem.Refresh();

                // Only targets present at stage start receive lightning spawners;
                // late-joining entities are handled by EmissionSafeZoneSystem
                var targetQuery = EntityQueryEnumerator<BlowoutTargetComponent>();
                while (targetQuery.MoveNext(out var targetUid, out _))
                {
                    if (HasComp<StalkerSafeZoneComponent>(targetUid))
                        continue;

                    var lightningSpawnerComponent = EnsureComp<EmissionLightningSpawnerComponent>(targetUid);
                    lightningSpawnerComponent.SpawnRadius = component.LightningSpawnRadius;
                    lightningSpawnerComponent.LightningIntervalRange = component.LightningIntervalRange;
                    lightningSpawnerComponent.LightningEffectProtoId = lightningEffectProtoId;
                }
            }
        }

        var rainStartTime = component.DamageEndDelay - component.RainStartBeforeEnd;
        if (!component.RainStarted && elapsed >= rainStartTime)
        {
            component.RainStarted = true;
            var duration = _random.Next(component.RainDurationMin, component.RainDurationMax);
            var weatherProto = _protoManager.Index(component.RainWeather);

            var weatherQuery = EntityQueryEnumerator<MapComponent>();
            while (weatherQuery.MoveNext(out _, out var mapComp))
            {
                _weather.SetWeather(mapComp.MapId, weatherProto, _timing.CurTime + duration);
            }
        }

        if (component.Stage == EmissionStage.Stage2 && elapsed >= component.DamageEndDelay)
        {
            component.Stage = EmissionStage.Stage3;

            var targetQuery = EntityQueryEnumerator<EmissionLightningSpawnerComponent>();
            while (targetQuery.MoveNext(out var targetUid, out var spawnerComponent))
                RemCompDeferred(targetUid, spawnerComponent);

            ClearAmbientLightColor();

            if (!component.SoundsPlayed.HasFlag(EmissionSoundsPlayed.Stage3))
            {
                PlayGlobalSound(component.SoundStage3);
                SendAnnouncement(component.AnnouncementStage3, component.AnnouncementSender);
                component.SoundsPlayed |= EmissionSoundsPlayed.Stage3;
            }
        }

        var doDamage = component.Stage == EmissionStage.Stage2 && _timing.CurTime >= component.NextDamageTick;
        if (doDamage)
        {
            component.NextDamageTick = _timing.CurTime + component.DamageInterval;
        }

        var query = EntityQueryEnumerator<BlowoutTargetComponent, TransformComponent, DamageableComponent>();
        while (query.MoveNext(out var target, out _, out var transform, out var damageable))
        {
            if (HasComp<StalkerSafeZoneComponent>(target))
                continue;

            if (HasComp<StalkerSafeZoneComponent>(_mapManager.GetMapEntityId(transform.MapID)))
                continue;

            if (doDamage && component.Damage is not null)
            {
                _damageableSystem.TryChangeDamage(target, component.Damage, interruptsDoAfters: false);
            }

            if (component.Stage == EmissionStage.Stage2)
            {
                var kick = new Vector2(_random.NextFloat(), _random.NextFloat()) * component.ShakeStrength;
                _cameraRecoil.KickCamera(target, kick);
            }
        }
    }

    private void PlayGlobalSound(SoundSpecifier sound)
    {
        _audio.PlayGlobal(sound, Filter.Empty().AddAllPlayers(_playerManager), true, AudioParams.Default.WithVolume(-8f));
    }

    private void SendAnnouncement(string message, string sender)
    {
        var filter = Filter.Empty().AddWhere(GameTicker.UserHasJoinedGame);
        _chatSystem.DispatchFilteredAnnouncement(filter, message, sender: sender, playSound: false, colorOverride: Color.Red);
    }

    private void SetAmbientLightColor(EmissionEventRuleComponent emissionRuleComponent)
    {
        _mapDay.SetEnabled(false);
        var query = EntityQueryEnumerator<MapLightComponent>();
        while (query.MoveNext(out var mapUid, out _))
        {
            var mapActiveEmissionComponent = EntityManager.ComponentFactory.GetComponent<MapActiveEmissionComponent>();
            mapActiveEmissionComponent.PrimaryEmissionColor = emissionRuleComponent.PrimaryEmissionColor;
            mapActiveEmissionComponent.SecondaryEmissionColor = emissionRuleComponent.SecondaryEmissionColor;

            mapActiveEmissionComponent.TotalDeviationDecreaseStartTime = emissionRuleComponent.EventStartTime + emissionRuleComponent.RedHueBeforeEndDelay;
            mapActiveEmissionComponent.TotalDeviationDecreaseRate =
                mapActiveEmissionComponent.Deviation / (float)(emissionRuleComponent.DamageEndDelay - emissionRuleComponent.RedHueBeforeEndDelay).TotalSeconds;

            AddComp(mapUid, mapActiveEmissionComponent);
            Dirty(mapUid, mapActiveEmissionComponent);
        }
    }

    private void ClearAmbientLightColor()
    {
        var query = EntityQueryEnumerator<MapActiveEmissionComponent>();
        while (query.MoveNext(out var mapUid, out var activeEmissionComponent))
            RemCompDeferred(mapUid, activeEmissionComponent);

        _mapDay.SetEnabled(true);
    }
}
