using System.Numerics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client._Stalker_EN.Shaders;

/// <summary>
/// Static helper for applying the STMobTint shader to sprite layers.
/// Used by both <see cref="MobVariant.STMobVariantSpriteSystem"/> and
/// <see cref="Trophy.STTrophySpriteSystem"/> to avoid code duplication.
/// </summary>
public static class STMobTintHelper
{
    private const string ShaderProtoId = "STMobTint";

    /// <summary>
    /// Applies tint/saturation/brightness shader to all layers of a sprite.
    /// Skips if params are all default or already cached with the same values.
    /// </summary>
    public static void TryApplyTint(
        EntityUid uid,
        Color? tint,
        float saturation,
        float brightness,
        SpriteComponent sprite,
        IPrototypeManager prototypeManager,
        Dictionary<EntityUid, (Color? Tint, float Sat, float Bright)> cache)
    {
        // ReSharper disable CompareOfFloatsByEqualityOperator
        if (tint == null && saturation == 1f && brightness == 1f)
            return;
        // ReSharper restore CompareOfFloatsByEqualityOperator

        var key = (tint, saturation, brightness);

        if (cache.TryGetValue(uid, out var existing) && existing == key)
            return;

        var shader = prototypeManager.Index<ShaderPrototype>(ShaderProtoId).InstanceUnique();

        var tintColor = tint ?? Color.White;
        shader.SetParameter("tintColor", new Vector3(tintColor.R, tintColor.G, tintColor.B));
        shader.SetParameter("saturation", saturation);
        shader.SetParameter("brightness", brightness);

        var layerIndex = 0;
        foreach (var _ in sprite.AllLayers)
        {
            sprite.LayerSetShader(layerIndex, shader, ShaderProtoId);
            layerIndex++;
        }

        cache[uid] = key;
    }

    /// <summary>
    /// Removes a cached entry when the source component is removed.
    /// </summary>
    public static void RemoveCached(
        EntityUid uid,
        Dictionary<EntityUid, (Color? Tint, float Sat, float Bright)> cache)
    {
        cache.Remove(uid);
    }
}
