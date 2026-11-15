using System.Numerics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.Fluids;

/// <summary>Draws port directions above machinery while respecting the field-of-view mask.</summary>
public sealed class CMUFluidPipeFlowOverlay : Overlay
{
    public static readonly Color InputColor = Color.FromHex("#64B5FF");
    public static readonly Color OutputColor = Color.FromHex("#FFD166");

    private readonly IEntityManager _entities;
    private readonly SharedTransformSystem _transform;
    private readonly Texture _arrow;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public CMUFluidPipeFlowOverlay(IEntityManager entities)
    {
        _entities = entities;
        _transform = entities.System<SharedTransformSystem>();
        _arrow = entities.System<SpriteSystem>().Frame0(
            new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/NavMap/beveled_arrow_north.png")));
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var query = _entities.EntityQueryEnumerator<CMUFluidPipeFlowComponent, TransformComponent, SpriteComponent>();
        var handle = args.WorldHandle;
        while (query.MoveNext(out _, out var flow, out var xform, out var sprite))
        {
            if (!sprite.Visible || !xform.Anchored || xform.MapID != args.MapId)
                continue;
            var position = _transform.GetWorldPosition(xform);
            if (!args.WorldAABB.Contains(position))
                continue;
            // Markers use grid directions; the pipe's own rotation is already included by the server.
            var gridRotation = _transform.GetWorldRotation(xform) - xform.LocalRotation;
            foreach (var arrow in flow.Arrows)
            {
                var side = (Vector2) arrow.Side;
                var lane = 0f;
                foreach (var other in flow.Arrows)
                {
                    if (other.Side == arrow.Side && other != arrow)
                        lane = arrow.Output ? -0.12f : 0.12f;
                }
                var offset = side * 0.28f + new Vector2(-side.Y, side.X) * lane;
                var alongSide = arrow.OnMachineTile ? arrow.Output : !arrow.Output;
                var direction = alongSide ? side : -side;
                var angle = new Angle(Math.Atan2(direction.Y, direction.X) - Math.PI / 2) + gridRotation;
                handle.SetTransform(Matrix3Helpers.CreateTransform(position + gridRotation.RotateVec(offset), angle));
                handle.DrawTextureRect(_arrow, new Box2(-0.14f, -0.14f, 0.14f, 0.14f),
                    arrow.Output ? OutputColor : InputColor);
            }
        }
        handle.SetTransform(Matrix3x2.Identity);
    }
}
