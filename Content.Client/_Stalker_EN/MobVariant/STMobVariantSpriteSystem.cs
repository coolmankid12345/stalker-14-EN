using Content.Client._Stalker_EN.Shaders;
using Content.Shared._Stalker_EN.MobVariant;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Client._Stalker_EN.MobVariant;

/// <summary>
/// Client-side system that applies a custom color shader to mob sprite layers
/// from <see cref="STMobVariantComponent"/> data. Supports brightness, saturation,
/// and tint control to create visually distinct variants without unique sprite assets.
/// </summary>
public sealed class STMobVariantSpriteSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    /// <summary>
    /// Tracks applied shader parameters per entity to avoid redundant re-creation
    /// on every PVS state update.
    /// </summary>
    private readonly Dictionary<EntityUid, (Color? Tint, float Sat, float Bright)> _appliedParams = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STMobVariantComponent, AfterAutoHandleStateEvent>(OnAfterState);
        SubscribeLocalEvent<STMobVariantComponent, ComponentRemove>(OnRemove);
    }

    private void OnAfterState(EntityUid uid, STMobVariantComponent variant, ref AfterAutoHandleStateEvent args)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        STMobTintHelper.TryApplyTint(
            uid,
            variant.SpriteTint,
            variant.SpriteSaturation,
            variant.SpriteBrightness,
            sprite,
            _prototypeManager,
            _appliedParams);
    }

    private void OnRemove(EntityUid uid, STMobVariantComponent component, ComponentRemove args)
    {
        STMobTintHelper.RemoveCached(uid, _appliedParams);
    }
}
