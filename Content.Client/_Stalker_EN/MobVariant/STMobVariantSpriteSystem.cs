using System.Numerics;
using Content.Shared._Stalker_EN.MobVariant;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
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

    private const string ShaderProtoId = "STMobTint";

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
        // Skip if no visual modifications at all.
        // ReSharper disable CompareOfFloatsByEqualityOperator
        if (variant.SpriteTint == null
            && variant.SpriteSaturation == 1f
            && variant.SpriteBrightness == 1f)
            return;
        // ReSharper restore CompareOfFloatsByEqualityOperator

        var key = (variant.SpriteTint, variant.SpriteSaturation, variant.SpriteBrightness);

        // Skip if already applied these exact params.
        if (_appliedParams.TryGetValue(uid, out var existing) && existing == key)
            return;

        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        // Create a unique shader instance with this entity's parameters.
        var shader = _prototypeManager.Index<ShaderPrototype>(ShaderProtoId).InstanceUnique();

        var tint = variant.SpriteTint ?? Color.White;
        shader.SetParameter("tintColor", new Vector3(tint.R, tint.G, tint.B));
        shader.SetParameter("saturation", variant.SpriteSaturation);
        shader.SetParameter("brightness", variant.SpriteBrightness);

        // Apply to all existing layers.
        var layerIndex = 0;
        foreach (var _ in sprite.AllLayers)
        {
            sprite.LayerSetShader(layerIndex, shader, ShaderProtoId);
            layerIndex++;
        }

        _appliedParams[uid] = key;
    }

    private void OnRemove(EntityUid uid, STMobVariantComponent component, ComponentRemove args)
    {
        _appliedParams.Remove(uid);
    }
}
