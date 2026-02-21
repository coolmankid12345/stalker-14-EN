using Content.Client._Stalker_EN.Shaders;
using Content.Shared._Stalker_EN.Trophy;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Client._Stalker_EN.Trophy;

/// <summary>
/// Client-side system that applies the variant mob's color shader to trophy item sprites.
/// Reuses the same STMobTint shader as <see cref="MobVariant.STMobVariantSpriteSystem"/>.
/// </summary>
public sealed class STTrophySpriteSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    private readonly Dictionary<EntityUid, (Color? Tint, float Sat, float Bright)> _appliedParams = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STTrophyComponent, AfterAutoHandleStateEvent>(OnAfterState);
        SubscribeLocalEvent<STTrophyComponent, ComponentRemove>(OnRemove);
    }

    private void OnAfterState(EntityUid uid, STTrophyComponent trophy, ref AfterAutoHandleStateEvent args)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        STMobTintHelper.TryApplyTint(
            uid,
            trophy.SpriteTint,
            trophy.SpriteSaturation,
            trophy.SpriteBrightness,
            sprite,
            _prototypeManager,
            _appliedParams);
    }

    private void OnRemove(EntityUid uid, STTrophyComponent component, ComponentRemove args)
    {
        STMobTintHelper.RemoveCached(uid, _appliedParams);
    }
}
