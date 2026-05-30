using Content.Shared.NodeContainer;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.NodeContainer.Nodes
{
    [DataDefinition]
    public sealed partial class PortablePipeNode : PipeNode
    {
        public override IEnumerable<Node> GetReachableNodes(TransformComponent xform,
            EntityQuery<NodeContainerComponent> nodeQuery,
            EntityQuery<TransformComponent> xformQuery,
            Entity<MapGridComponent>? grid,
            IEntityManager entMan)
        {
            if (!xform.Anchored || grid == null)
                yield break;

            var gridIndex = NodeHelpers.TileIndicesFor(entMan, grid.Value, xform.Coordinates);

            foreach (var node in NodeHelpers.GetNodesInTile(nodeQuery, entMan, grid.Value, gridIndex))
            {
                if (node is PortPipeNode)
                    yield return node;
            }

            foreach (var node in base.GetReachableNodes(xform, nodeQuery, xformQuery, grid, entMan))
            {
                yield return node;
            }
        }
    }
}
