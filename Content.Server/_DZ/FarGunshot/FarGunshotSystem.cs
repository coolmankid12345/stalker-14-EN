using Content.Shared._DZ.FarGunshot;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;

namespace Content.Server._DZ.FarGunshot;

public sealed class FarGunshotSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly TransformSystem _transform = default!;


    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FarGunshotComponent, FargunshotEvent>(OnFarGunshot);

    }

    public void OnFarGunshot(EntityUid uid, FarGunshotComponent component, FargunshotEvent args)
    {
        if (uid == EntityUid.Invalid || args.GunUid != uid.Id)
            return;

        var shootPos = _transform.GetMapCoordinates(uid);

        var range = component.Range * (component.SilencerDecrease ?? component.BaseSilencerDecrease);

        if (component.Range <= 14f) // we need this since i want to decrease number of uselles iterations
            return;


        // Create a filter for players who are far enough to hear the distant gunshot,
        // excluding those within close range (14)
        var farSoundFilter = Filter.Empty()
            .AddInRange(shootPos, range)
            .RemoveInRange(shootPos, 14f);

        var soundParams = component.Sound?.Params ?? AudioParams.Default;
        soundParams.MaxDistance = component.Range;
        soundParams.ReferenceDistance = 14f;

        var sound = component.Sound;
        if (component.SilencerDecrease is null)
            sound = component.SoundSilencer;

        _audio.PlayEntity(
            sound,
            farSoundFilter,
            uid,
            recordReplay: true,
            soundParams
        );
    }

}
