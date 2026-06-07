#nullable enable
using System.Linq;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Pair;

public sealed partial class TestPair
{
    public async Task DeleteAllEntitiesLeafFirst()
    {
        while (Server.EntMan.EntityCount > 0)
        {
            var deleted = 0;

            await Server.WaitPost(() =>
            {
                var entMan = Server.EntMan;
                var xforms = entMan.GetEntityQuery<TransformComponent>();
                var leaves = entMan.GetEntities()
                    .Where(ent => IsLeaf(ent, xforms))
                    .ToArray();

                deleted = leaves.Length;
                foreach (var ent in leaves)
                {
                    entMan.DeleteEntity(ent);
                }
            });

            Assert.That(deleted, Is.GreaterThan(0), "Unable to find leaf entities while deleting all entities.");
            await RunTicksSync(5);
        }
    }

    public async Task DeleteEntityTreeLeafFirst(EntityUid root)
    {
        while (Server.EntMan.EntityExists(root))
        {
            var deleted = 0;

            await Server.WaitPost(() =>
            {
                var entMan = Server.EntMan;
                if (!entMan.EntityExists(root))
                    return;

                var xforms = entMan.GetEntityQuery<TransformComponent>();
                var leaves = entMan.GetEntities()
                    .Where(ent => IsInTree(ent, root, xforms) && IsLeaf(ent, xforms))
                    .ToArray();

                deleted = leaves.Length;
                foreach (var ent in leaves)
                {
                    entMan.DeleteEntity(ent);
                }
            });

            Assert.That(deleted, Is.GreaterThan(0), $"Unable to find leaf entities while deleting entity tree rooted at {root}.");
            await RunTicksSync(5);
        }
    }

    private static bool IsLeaf(EntityUid ent, EntityQuery<TransformComponent> xforms)
        => !xforms.TryGetComponent(ent, out var xform) || xform.ChildCount == 0;

    private static bool IsInTree(EntityUid ent, EntityUid root, EntityQuery<TransformComponent> xforms)
    {
        if (ent == root)
            return true;

        while (xforms.TryGetComponent(ent, out var xform))
        {
            var parent = xform.ParentUid;
            if (!parent.IsValid())
                return false;

            if (parent == root)
                return true;

            ent = parent;
        }

        return false;
    }
}
