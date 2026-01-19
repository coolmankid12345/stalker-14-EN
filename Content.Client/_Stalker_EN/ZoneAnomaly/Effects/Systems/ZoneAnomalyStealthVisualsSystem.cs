using Content.Client.Stealth;
using Content.Shared._Stalker.ZoneAnomaly.Effects.Components;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;

namespace Content.Client._Stalker_EN.ZoneAnomaly.Effects.Systems;

public sealed class ZoneAnomalyStealthVisualsSystem : EntitySystem
{
    [Dependency] private readonly SharedStealthSystem _stealth = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ZoneAnomalyEffectStealthComponent, BeforePostShaderRenderEvent>(OnShaderRender, after: [typeof(StealthSystem)]);
    }

    private void OnShaderRender(EntityUid uid, ZoneAnomalyEffectStealthComponent component, BeforePostShaderRenderEvent args)
    {
        // Override the blue tint from the standard stealth system
        // Use grayscale instead so anomalies fade without blue hue
        if (!TryComp<StealthComponent>(uid, out var stealth))
            return;

        var visibility = _stealth.GetVisibility(uid, stealth);
        visibility = Math.Clamp(visibility, -1f, 1f);
        visibility = MathF.Max(0, visibility);

        // Set all channels to visibility (grayscale) instead of blue tint
        args.Sprite.Color = new Color(visibility, visibility, visibility, 1);
    }
}
