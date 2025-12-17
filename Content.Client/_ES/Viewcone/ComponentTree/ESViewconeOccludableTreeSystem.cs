using System.Numerics;
using Content.Shared._ES.Viewcone;
using Robust.Client.GameObjects;
using Robust.Shared.ComponentTrees;
using Robust.Shared.Physics;

namespace Content.Client._ES.Viewcone.ComponentTree;

/// <summary>
///     Handles gathering sprites to modify alpha in the viewcone overlays
/// </summary>
public sealed class ESViewconeOccludableTreeSystem : ComponentTreeSystem<ESViewconeOccludableTreeComponent, ESViewconeOccludableComponent>
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    protected override bool DoFrameUpdate => true;
    protected override bool DoTickUpdate => false;
    protected override bool Recursive => false;

    protected override Box2 ExtractAabb(in ComponentTreeEntry<ESViewconeOccludableComponent> entry, Vector2 pos, Angle rot)
    {
        // Fixed default size for the sprite (width x height)
        // You can adjust this to match typical sprite sizes in your game
        var defaultSize = new Vector2(1f, 1f);
        var halfSize = defaultSize / 2f;

        // Axis-aligned bounding box centered at pos
        return new Box2(pos - halfSize, pos + halfSize);
    }
}
