using Content.Shared.Light.Components;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Systems;

namespace Content.Shared.Light.EntitySystems;

public abstract partial class SharedRoofSystem
{
    [Dependency] private SharedPhysicsSystem _roofPhysics = default!;
    [Dependency] private FixtureSystem _roofFixtures = default!;
    [Dependency] private SharedContainerSystem _roofContainers = default!;

    /// <summary>
    /// Resolves point roofs and proven fixture intersections in one spatial query.
    /// Fixture edges, containers and overlapping colors retain exact per-tile lookup semantics.
    /// Rebuilt for each view so replication, movement and fixture changes need no cache invalidation.
    /// </summary>
    public void GetEntityRoofColors(
        Entity<MapGridComponent, RoofComponent> grid,
        Box2 localBounds,
        Dictionary<Vector2i, Color> colors,
        HashSet<Vector2i> exactTiles)
    {
        colors.Clear();
        exactTiles.Clear();
        _renderRoofSet.Clear();
        var size = grid.Comp1.TileSize;
        var queryBounds = localBounds.Enlarged(size + EntityLookupSystem.TileEnlargementRadius);
        _lookup.GetLocalEntitiesIntersecting(grid.Owner, queryBounds, _renderRoofSet,
            EntityLookupSystem.DefaultFlags | LookupFlags.Approximate);
        var inverse = _transform.GetInvWorldMatrix(grid.Owner);
        var transforms = GetEntityQuery<TransformComponent>();
        var fixtures = GetEntityQuery<FixturesComponent>();
        var containers = GetEntityQuery<ContainerManagerComponent>();
        foreach (var (uid, roof) in _renderRoofSet)
        {
            // A contained roof inherits its container's query coverage, even when
            // the container's own roof is disabled. Resolve those tiles normally.
            var container = containers.HasComp(uid);
            if (!roof.Enabled && !container)
                continue;

            var xform = transforms.GetComponent(uid);
            if (_roofContainers.IsEntityOrParentInContainer(uid, xform: xform))
                continue;

            if (container)
            {
                var bounds = inverse.TransformBox(_lookup.GetWorldAABB(uid)).Enlarged(size);
                var left = (int) MathF.Floor(Math.Max(bounds.Left, queryBounds.Left) / size);
                var right = (int) MathF.Floor(Math.Min(bounds.Right, queryBounds.Right) / size);
                var bottom = (int) MathF.Floor(Math.Max(bounds.Bottom, queryBounds.Bottom) / size);
                var top = (int) MathF.Floor(Math.Min(bounds.Top, queryBounds.Top) / size);
                for (var x = left; x <= right; x++)
                for (var y = bottom; y <= top; y++)
                    exactTiles.Add(new Vector2i(x, y));
                continue;
            }

            var physicsTransform = _roofPhysics.GetLocalPhysicsTransform(uid, xform);
            var color = roof.Color ?? grid.Comp2.Color;
            if (fixtures.TryGetComponent(uid, out var fixture) && fixture.FixtureCount > 0)
            {
                foreach (var shapeFixture in fixture.Fixtures.Values)
                {
                    var shape = shapeFixture.Shape;
                    for (var child = 0; child < shape.ChildCount; child++)
                    {
                        var bounds = shape.ComputeAABB(physicsTransform, child).Enlarged(PhysicsConstants.LinearSlop);
                        var left = (int) MathF.Floor(Math.Max(bounds.Left, queryBounds.Left) / size);
                        var right = (int) MathF.Floor(Math.Min(bounds.Right, queryBounds.Right) / size);
                        var bottom = (int) MathF.Floor(Math.Max(bounds.Bottom, queryBounds.Bottom) / size);
                        var top = (int) MathF.Floor(Math.Min(bounds.Top, queryBounds.Top) / size);
                        for (var x = left; x <= right; x++)
                        for (var y = bottom; y <= top; y++)
                        {
                            var index = new Vector2i(x, y);
                            var tile = _lookup.GetLocalBounds(index, size).Enlarged(EntityLookupSystem.TileEnlargementRadius);
                            if (!bounds.Intersects(tile))
                                continue;

                            // A point in both shapes proves intersection without reproducing
                            // the collision solver. A failed probe cannot prove separation.
                            if (shape.ShapeType is ShapeType.Polygon or ShapeType.Circle &&
                                (_roofFixtures.TestPoint(shape, physicsTransform, tile.Center) ||
                                 _roofFixtures.TestPoint(shape, physicsTransform, tile.BottomLeft) ||
                                 _roofFixtures.TestPoint(shape, physicsTransform, tile.BottomRight) ||
                                 _roofFixtures.TestPoint(shape, physicsTransform, tile.TopLeft) ||
                                 _roofFixtures.TestPoint(shape, physicsTransform, tile.TopRight)))
                                AddColor(index, color);
                            else
                                exactTiles.Add(index);
                        }
                    }
                }
                continue;
            }

            // The engine treats entities without fixtures as points. Its default
            // tile query shrinks the tile, excluding points along tile edges.
            var position = physicsTransform.Position;
            var pointIndex = new Vector2i((int) MathF.Floor(position.X / size), (int) MathF.Floor(position.Y / size));
            var pointTile = _lookup.GetLocalBounds(pointIndex, size).Enlarged(EntityLookupSystem.TileEnlargementRadius);
            if (!pointTile.Contains(position))
                continue;

            AddColor(pointIndex, color);
        }
        _renderRoofSet.Clear();

        void AddColor(Vector2i index, Color color)
        {
            if (colors.TryGetValue(index, out var previous) && previous != color)
                exactTiles.Add(index); // Preserve the lookup's tie-breaking order.
            colors[index] = color;
        }
    }
}
