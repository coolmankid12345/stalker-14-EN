using Content.Shared._Stalker_EN.ZoneAnomaly.Audio;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._Stalker_EN.ZoneAnomaly.Audio;

/// <summary>
/// Handles distance-based volume scaling for zone anomaly proximity sounds.
/// Uses cooldown-based updates for performance efficiency.
/// </summary>
public sealed class ZoneAnomalyProximitySoundSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly AudioParams BaseParams = AudioParams.Default
        .WithLoop(true)
        .WithVolume(0f); // Start at 0, we'll control volume ourselves

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ZoneAnomalyProximitySoundComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ZoneAnomalyProximitySoundComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(EntityUid uid, ZoneAnomalyProximitySoundComponent component, ComponentStartup args)
    {
        // Start playing the sound immediately (at initial calculated volume)
        var audioParams = BaseParams.WithMaxDistance(component.MaxRange);

        var stream = _audio.PlayEntity(component.Sound, Filter.Local(), uid, false, audioParams);
        if (stream != null)
        {
            component.PlayingStream = stream.Value.Entity;
            component.CurrentVolume = component.MinVolume;

            // Set initial volume based on distance
            UpdateVolumeForEntity(uid, component);
        }
    }

    private void OnShutdown(EntityUid uid, ZoneAnomalyProximitySoundComponent component, ComponentShutdown args)
    {
        if (component.PlayingStream != null)
        {
            _audio.Stop(component.PlayingStream);
            component.PlayingStream = null;
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_timing.IsFirstTimePredicted)
            return;

        var curTime = _timing.CurTime;

        // Get player position once per update
        var playerEntity = _player.LocalEntity;
        if (playerEntity == null || !TryComp<TransformComponent>(playerEntity, out var playerXform))
            return;

        var playerPos = _transform.GetWorldPosition(playerXform);

        var query = EntityQueryEnumerator<ZoneAnomalyProximitySoundComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            // Skip if not time to update yet
            if (curTime < comp.NextUpdate)
                continue;

            comp.NextUpdate = curTime + TimeSpan.FromSeconds(comp.UpdateCooldown);

            // Skip if no stream playing
            if (comp.PlayingStream == null)
                continue;

            // Skip if on different map
            if (xform.MapID != playerXform.MapID)
            {
                // Mute if on different map
                if (comp.CurrentVolume > 0)
                {
                    comp.CurrentVolume = 0;
                    _audio.SetVolume(comp.PlayingStream, SharedAudioSystem.GainToVolume(0f));
                }
                continue;
            }

            // Calculate distance
            var entityPos = _transform.GetWorldPosition(xform);
            var distance = (playerPos - entityPos).Length();

            // Calculate target volume based on distance
            float targetVolume;
            if (distance >= comp.MaxRange)
            {
                targetVolume = 0f; // Out of range, silent
            }
            else
            {
                // Linear interpolation: MaxVolume at center, MinVolume at MaxRange
                var t = 1f - (distance / comp.MaxRange);
                targetVolume = comp.MinVolume + (comp.MaxVolume - comp.MinVolume) * t;
            }

            // Apply volume if changed significantly (avoid unnecessary audio calls)
            if (MathF.Abs(targetVolume - comp.CurrentVolume) > 0.01f)
            {
                comp.CurrentVolume = targetVolume;
                // Convert our 0-1 volume to the audio system's gain/volume
                _audio.SetVolume(comp.PlayingStream, SharedAudioSystem.GainToVolume(targetVolume));
            }
        }
    }

    private void UpdateVolumeForEntity(EntityUid uid, ZoneAnomalyProximitySoundComponent component)
    {
        if (component.PlayingStream == null)
            return;

        var playerEntity = _player.LocalEntity;
        if (playerEntity == null || !TryComp<TransformComponent>(playerEntity, out var playerXform))
            return;

        if (!TryComp<TransformComponent>(uid, out var xform))
            return;

        if (xform.MapID != playerXform.MapID)
        {
            _audio.SetVolume(component.PlayingStream, SharedAudioSystem.GainToVolume(0f));
            return;
        }

        var playerPos = _transform.GetWorldPosition(playerXform);
        var entityPos = _transform.GetWorldPosition(xform);
        var distance = (playerPos - entityPos).Length();

        float targetVolume;
        if (distance >= component.MaxRange)
        {
            targetVolume = 0f;
        }
        else
        {
            var t = 1f - (distance / component.MaxRange);
            targetVolume = component.MinVolume + (component.MaxVolume - component.MinVolume) * t;
        }

        component.CurrentVolume = targetVolume;
        _audio.SetVolume(component.PlayingStream, SharedAudioSystem.GainToVolume(targetVolume));
    }
}
