using System.Numerics;
using Content.Shared._RMC14.Flora;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Graphics.RSI;

namespace Content.Client._RMC14.Flora;

public sealed partial class TallGrassOcclusionOverlay : Overlay
{
    [Dependency] private IEntityManager _entity = default!;

    private readonly EntityLookupSystem _lookup;
    private readonly SharedTransformSystem _transform;
    private readonly EntityQuery<SpriteComponent> _spriteQuery;
    private readonly EntityQuery<TallGrassOcclusionComponent> _tallGrassQuery;
    private readonly EntityQuery<TransformComponent> _transformQuery;
    private readonly HashSet<EntityUid> _intersecting = new();

    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;

    public TallGrassOcclusionOverlay()
    {
        IoCManager.InjectDependencies(this);

        _lookup = _entity.System<EntityLookupSystem>();
        _transform = _entity.System<SharedTransformSystem>();
        _spriteQuery = _entity.GetEntityQuery<SpriteComponent>();
        _tallGrassQuery = _entity.GetEntityQuery<TallGrassOcclusionComponent>();
        _transformQuery = _entity.GetEntityQuery<TransformComponent>();

        ZIndex = (int) Content.Shared.DrawDepth.DrawDepth.OverMobs;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        _intersecting.Clear();
        _lookup.GetEntitiesIntersecting(args.MapId, args.WorldAABB, _intersecting);

        var eyeRotation = args.Viewport.Eye?.Rotation ?? default;
        foreach (var uid in _intersecting)
        {
            if (!_tallGrassQuery.TryComp(uid, out var grass) ||
                !_spriteQuery.TryComp(uid, out var sprite) ||
                !sprite.Visible ||
                sprite.BaseRSI is not { } rsi ||
                !rsi.TryGetState(grass.State, out var state) ||
                !_transformQuery.TryComp(uid, out var xform))
            {
                continue;
            }

            var (position, rotation) = _transform.GetWorldPositionRotation(xform);
            if (!args.WorldBounds.Contains(position))
                continue;

            var direction = SpriteComponent.Layer.GetDirection(
                state.RsiDirections,
                (rotation + eyeRotation).Reduced().FlipPositive());
            if (grass.Diagonal)
                direction = ToDiagonal(direction);

            args.WorldHandle.DrawTexture(state.GetFrame(direction, 0), position - new Vector2(0.5f), sprite.Color);
        }
    }

    private static RsiDirection ToDiagonal(RsiDirection direction)
    {
        return direction switch
        {
            RsiDirection.South => RsiDirection.SouthEast,
            RsiDirection.North => RsiDirection.SouthWest,
            RsiDirection.East => RsiDirection.NorthEast,
            RsiDirection.West => RsiDirection.NorthWest,
            _ => direction,
        };
    }
}
