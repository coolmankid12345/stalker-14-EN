using Robust.Client.Graphics;
using Robust.Shared.Enums;

namespace Content.Client._ES.Viewcone.Overlays;

/// <summary>
///     After <see cref="ESViewconeSetAlphaOverlay"/> has run, resets the alpha of affected entities
///     back to normal.
/// </summary>
public sealed class ESViewconeResetAlphaOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _ent = default!;
    private readonly ESViewconeOverlayManagementSystem _cone;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public ESViewconeResetAlphaOverlay()
    {
        IoCManager.InjectDependencies(this);

        _cone = _ent.EntitySysManager.GetEntitySystem<ESViewconeOverlayManagementSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        foreach (var ((uid, sprite), baseAlpha) in _cone.CachedBaseAlphas)
        {
            sprite.Color = sprite.Color.WithAlpha(baseAlpha);
        }

        _cone.CachedBaseAlphas.Clear();
    }
}
