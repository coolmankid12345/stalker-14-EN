using Content.Shared.Chat;
using Content.Shared.IdentityManagement;
using Content.Shared.IdentityManagement.Components;

namespace Content.Shared._Stalker_EN.IdentityManagement;

/// <summary>
/// Bridges the identity system into chat and radio by replacing the raw MetaData voice name
/// with the identity-aware name from <see cref="Identity.Name"/>. This ensures that when a
/// player's identity is blocked (e.g., by a mask or helmet with <see cref="IdentityBlockerComponent"/>),
/// their hidden identity is also used in chat messages and radio transmissions.
/// </summary>
public sealed class STIdentityVoiceSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<IdentityComponent, TransformSpeakerNameEvent>(OnTransformSpeakerName);
    }

    private void OnTransformSpeakerName(Entity<IdentityComponent> ent, ref TransformSpeakerNameEvent args)
    {
        args.VoiceName = Identity.Name(ent, EntityManager);
    }
}
