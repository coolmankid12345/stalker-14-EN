using System.Numerics;
using Robust.Client.Debugging;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Physics.Systems;

/*
    Firstly, this is totally copypasted from PhysicsDebugOverlay
    Secondly, i am aware this is !!!HORRENDOUSLY ATROCIOUS!!! and there's nothing else i could do
*/

namespace Content.Client._Stalker_EN.SmallBbOverlay;

internal sealed class SmallBbOverlay : Overlay
{
    private readonly IEntityManager _entityManager;
    private readonly IEyeManager _eyeManager;
    private readonly IInputManager _inputManager;
    private readonly SharedPhysicsSystem _physicsSystem;
    private readonly DebugPhysicsSystem _debugPhysicsSystem;

    public override OverlaySpace Space => OverlaySpace.WorldSpace | OverlaySpace.ScreenSpace;
    public const float MaxFixtureSizeOrRadius = 15f; // max size/radius to be rendered

    private readonly Font _font;

    public SmallBbOverlay(IEntityManager entityManager, IEyeManager eyeManager, IInputManager inputManager, IResourceCache cache, SharedPhysicsSystem physicsSystem, DebugPhysicsSystem debugPhysicsSystem)
    {
        _entityManager = entityManager;
        _eyeManager = eyeManager;
        _inputManager = inputManager;
        _physicsSystem = physicsSystem;
        _debugPhysicsSystem = debugPhysicsSystem;
        _font = new VectorFont(cache.GetResource<FontResource>("/EngineFonts/NotoSans/NotoSans-Regular.ttf"), 10);
    }

    private void DrawWorld(DrawingHandleWorld worldHandle, OverlayDrawArgs args)
    {
        var viewBounds = args.WorldBounds;
        var mapId = args.MapId;

        foreach (var physBody in _physicsSystem.GetCollidingEntities(mapId, viewBounds))
        {
            if (_entityManager.HasComponent<MapGridComponent>(physBody)) continue;

            var xform = _physicsSystem.GetPhysicsTransform(physBody);
            var comp = physBody.Comp;

            const float AlphaModifier = 0.2f;

            foreach (var fixture in _entityManager.GetComponent<FixturesComponent>(physBody).Fixtures.Values)
            {
                // Invalid shape - Box2D doesn't check for IsSensor but we will for sanity.
                if (comp.BodyType == BodyType.Dynamic && fixture.Density == 0f && fixture.Hard)
                {
                    DrawShape(worldHandle, fixture, xform, Color.Red.WithAlpha(AlphaModifier));
                }
                else if (!comp.CanCollide)
                {
                    DrawShape(worldHandle, fixture, xform, new Color(0.5f, 0.5f, 0.3f).WithAlpha(AlphaModifier));
                }
                else if (comp.BodyType == BodyType.Static)
                {
                    DrawShape(worldHandle, fixture, xform, new Color(0.5f, 0.9f, 0.5f).WithAlpha(AlphaModifier));
                }
                else if ((comp.BodyType & (BodyType.Kinematic | BodyType.KinematicController)) != 0x0)
                {
                    DrawShape(worldHandle, fixture, xform, new Color(0.5f, 0.5f, 0.9f).WithAlpha(AlphaModifier));
                }
                else if (!comp.Awake)
                {
                    DrawShape(worldHandle, fixture, xform, new Color(0.6f, 0.6f, 0.6f).WithAlpha(AlphaModifier));
                }
                else
                {
                    DrawShape(worldHandle, fixture, xform, new Color(0.9f, 0.7f, 0.7f).WithAlpha(AlphaModifier));
                }
            }
        }

        worldHandle.UseShader(null);
        worldHandle.SetTransform(Matrix3x2.Identity);
    }

    private void DrawScreen(DrawingHandleScreen screenHandle, OverlayDrawArgs args)
    {
        var mapId = args.MapId;
        var mousePos = _inputManager.MouseScreenPosition;

        if ((_debugPhysicsSystem.Flags & PhysicsDebugFlags.ShapeInfo) != 0x0)
        {
            var hoverBodies = new List<Entity<PhysicsComponent>>();
            var bounds = Box2.UnitCentered.Translated(_eyeManager.PixelToMap(mousePos.Position).Position);

            foreach (var physBody in _physicsSystem.GetCollidingEntities(mapId, bounds))
            {
                var uid = physBody.Owner;
                if (_entityManager.HasComponent<MapGridComponent>(uid)) continue;
                hoverBodies.Add((uid, physBody));
            }

            var lineHeight = _font.GetLineHeight(1f);
            var drawPos = mousePos.Position + new Vector2(20, 0) + new Vector2(0, -(hoverBodies.Count * 4 * lineHeight / 2f));
            int row = 0;

            foreach (var bodyEnt in hoverBodies)
            {
                if (bodyEnt != hoverBodies[0])
                {
                    screenHandle.DrawString(_font, drawPos + new Vector2(0, row * lineHeight), "------");
                    row++;
                }

                var body = bodyEnt.Comp;
                var meta = _entityManager.GetComponent<MetaDataComponent>(bodyEnt);

                screenHandle.DrawString(_font, drawPos + new Vector2(0, row * lineHeight), $"Ent: {bodyEnt.Owner} ({meta.EntityName})");
                row++;
                screenHandle.DrawString(_font, drawPos + new Vector2(0, row * lineHeight), $"Layer: {(body.CollisionLayer, 2)} [base-10, not base-2]");
                row++;
                screenHandle.DrawString(_font, drawPos + new Vector2(0, row * lineHeight), $"Mask: {(body.CollisionMask, 2)} [base-10, not base-2]");
                row++;
                screenHandle.DrawString(_font, drawPos + new Vector2(0, row * lineHeight), $"Enabled: {body.CanCollide}, Hard: {body.Hard}, Anchored: {(body).BodyType == BodyType.Static}");
                row++;
            }
        }

        screenHandle.UseShader(null);
        screenHandle.SetTransform(Matrix3x2.Identity);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        switch (args.Space)
        {
            case OverlaySpace.ScreenSpace:
                DrawScreen((DrawingHandleScreen)args.DrawingHandle, args);
                break;
            case OverlaySpace.WorldSpace:
                DrawWorld((DrawingHandleWorld)args.DrawingHandle, args);
                break;
        }
    }

    private void DrawShape(DrawingHandleWorld worldHandle, Fixture fixture, Transform xform, Color color)
    {
        switch (fixture.Shape)
        {
            case ChainShape cShape:
                {
                    if (cShape.Radius > MaxFixtureSizeOrRadius)
                        return;

                    var count = cShape.Count;
                    var vertices = cShape.Vertices;

                    var v1 = Transform.Mul(xform, vertices[0]);
                    for (var i = 1; i < count; ++i)
                    {
                        var v2 = Transform.Mul(xform, vertices[i]);
                        worldHandle.DrawLine(v1, v2, color);
                        v1 = v2;
                    }
                }
                break;
            case PhysShapeCircle circle:
                if (circle.Radius > MaxFixtureSizeOrRadius)
                    return;

                var center = Transform.Mul(xform, circle.Position);
                worldHandle.DrawCircle(center, circle.Radius, color);
                break;
            // case EdgeShape edge:
            // Unsupported because Vertex1 and Vertex2 are :joy: inaccessible :joy: because :joy: theyre internal :joy:
            // {
            //     var v1 = Transform.Mul(xform, edge.Vertex1);
            //     var v2 = Transform.Mul(xform, edge.Vertex2);
            //     worldHandle.DrawLine(v1, v2, color);

            //     if (edge.OneSided)
            //     {
            //         worldHandle.DrawCircle(v1, 0.1f, color);
            //         worldHandle.DrawCircle(v2, 0.1f, color);
            //     }
            // }

            // break;
            case PolygonShape poly:
                if (poly.Radius > MaxFixtureSizeOrRadius)
                    return;

                // can't be stackalloc'd because we are on client and this isn't enginecode so it fails typechecker
                var verts = new Vector2[poly.VertexCount];

                for (var i = 0; i < verts.Length; i++)
                {
                    verts[i] = Transform.Mul(xform, poly.Vertices[i]);
                }

                worldHandle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, verts, color);
                break;
            default:
                return;
        }
    }
}
