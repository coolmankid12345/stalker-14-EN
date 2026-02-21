using System.Numerics;
using Content.Shared._Stalker_EN.Trophy;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client._Stalker_EN.Trophy;

/// <summary>
/// Client-side system that applies the variant mob's color shader to trophy item sprites.
/// Reuses the same STMobTint shader as <see cref="MobVariant.STMobVariantSpriteSystem"/>.
/// </summary>
public sealed class STTrophySpriteSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    private const string ShaderProtoId = "STMobTint";

    private readonly Dictionary<EntityUid, (Color? Tint, float Sat, float Bright)> _appliedParams = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STTrophyComponent, AfterAutoHandleStateEvent>(OnAfterState);
        SubscribeLocalEvent<STTrophyComponent, ComponentRemove>(OnRemove);
    }

    private void OnAfterState(EntityUid uid, STTrophyComponent trophy, ref AfterAutoHandleStateEvent args)
    {
        // ReSharper disable CompareOfFloatsByEqualityOperator
        if (trophy.SpriteTint == null
            && trophy.SpriteSaturation == 1f
            && trophy.SpriteBrightness == 1f)
            return;
        // ReSharper restore CompareOfFloatsByEqualityOperator

        var key = (trophy.SpriteTint, trophy.SpriteSaturation, trophy.SpriteBrightness);

        if (_appliedParams.TryGetValue(uid, out var existing) && existing == key)
            return;

        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        var shader = _prototypeManager.Index<ShaderPrototype>(ShaderProtoId).InstanceUnique();

        var tint = trophy.SpriteTint ?? Color.White;
        shader.SetParameter("tintColor", new Vector3(tint.R, tint.G, tint.B));
        shader.SetParameter("saturation", trophy.SpriteSaturation);
        shader.SetParameter("brightness", trophy.SpriteBrightness);

        var layerIndex = 0;
        foreach (var _ in sprite.AllLayers)
        {
            sprite.LayerSetShader(layerIndex, shader, ShaderProtoId);
            layerIndex++;
        }

        _appliedParams[uid] = key;
    }

    private void OnRemove(EntityUid uid, STTrophyComponent component, ComponentRemove args)
    {
        _appliedParams.Remove(uid);
    }
}
