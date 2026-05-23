using System.Diagnostics.CodeAnalysis;
using Content.Shared.NodeContainer;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.NodeContainer.Nodes
{
    /// <summary>
    ///     Helper utilities for implementing <see cref="Node"/>.
    /// </summary>
    public static class NodeHelpers
    {
        public static Vector2i TileIndicesFor(IEntityManager entMan, Entity<MapGridComponent> grid, EntityCoordinates coords)
        {
            return entMan.System<SharedMapSystem>().TileIndicesFor(grid, coords);
        }

        public static IEnumerable<Node> GetNodesInTile(
            EntityQuery<NodeContainerComponent> nodeQuery,
            IEntityManager entMan,
            Entity<MapGridComponent> grid,
            Vector2i coords)
        {
            var map = entMan.System<SharedMapSystem>();
            foreach (var entityUid in map.GetAnchoredEntities(grid, coords))
            {
                if (!nodeQuery.TryGetComponent(entityUid, out var container))
                    continue;

                foreach (var node in container.Nodes.Values)
                {
                    yield return node;
                }
            }
        }

        public static IEnumerable<(Direction dir, Node node)> GetCardinalNeighborNodes(
            EntityQuery<NodeContainerComponent> nodeQuery,
            IEntityManager entMan,
            Entity<MapGridComponent> grid,
            Vector2i coords,
            bool includeSameTile = true)
        {
            foreach (var (dir, entityUid) in GetCardinalNeighborCells(entMan, grid, coords, includeSameTile))
            {
                if (!nodeQuery.TryGetComponent(entityUid, out var container))
                    continue;

                foreach (var node in container.Nodes.Values)
                {
                    yield return (dir, node);
                }
            }
        }

        [SuppressMessage("ReSharper", "EnforceForeachStatementBraces")]
        public static IEnumerable<(Direction dir, EntityUid entity)> GetCardinalNeighborCells(
            IEntityManager entMan,
            Entity<MapGridComponent> grid,
            Vector2i coords,
            bool includeSameTile = true)
        {
            var map = entMan.System<SharedMapSystem>();
            if (includeSameTile)
            {
                foreach (var uid in map.GetAnchoredEntities(grid, coords))
                    yield return (Direction.Invalid, uid);
            }

            foreach (var uid in map.GetAnchoredEntities(grid, coords + (0, 1)))
                yield return (Direction.North, uid);

            foreach (var uid in map.GetAnchoredEntities(grid, coords + (0, -1)))
                yield return (Direction.South, uid);

            foreach (var uid in map.GetAnchoredEntities(grid, coords + (1, 0)))
                yield return (Direction.East, uid);

            foreach (var uid in map.GetAnchoredEntities(grid, coords + (-1, 0)))
                yield return (Direction.West, uid);
        }
    }
}
