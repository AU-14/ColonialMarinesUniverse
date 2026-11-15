#nullable enable
using System.Linq;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Pair;

public sealed partial class TestPair
{
    public async Task DeleteAllEntitiesLeafFirst(Func<EntityUid, bool>? preserve = null)
    {
        while (true)
        {
            var deleted = 0;

            await Server.WaitPost(() =>
            {
                var entMan = Server.EntMan;
                var xforms = entMan.GetEntityQuery<TransformComponent>();
                var leaves = entMan.GetEntities()
                    .Where(ent => preserve?.Invoke(ent) != true && IsLeaf(ent, xforms))
                    .ToArray();

                deleted = leaves.Length;
                foreach (var ent in leaves)
                {
                    entMan.DeleteEntity(ent);
                }
            });

            if (deleted == 0)
            {
                await Server.WaitAssertion(() => Assert.That(Server.EntMan.GetEntities()
                    .Where(ent => preserve?.Invoke(ent) != true), Is.Empty,
                    "Unable to find leaf entities while deleting all entities."));
                return;
            }
            await RunTicksSync(5);
        }
    }

    // CMU14 Begin: the whole cascade runs in one WaitPost. The upstream shape pumped
    // RunTicksSync between leaf waves, so a PVS state went out while a parent (gas tank) was
    // still alive but its networked child reference (toggle action) was already deleted, and
    // teardown reported the unresolvable-metadata error as a test failure.
    public async Task DeleteEntityTreeLeafFirst(EntityUid root)
    {
        await Server.WaitPost(() =>
        {
            var entMan = Server.EntMan;
            var xforms = entMan.GetEntityQuery<TransformComponent>();

            while (entMan.EntityExists(root))
            {
                var leaves = entMan.GetEntities()
                    .Where(ent => IsInTree(ent, root, xforms) && IsLeaf(ent, xforms))
                    .ToArray();

                Assert.That(leaves.Length, Is.GreaterThan(0),
                    $"Unable to find leaf entities while deleting entity tree rooted at {root}.");

                foreach (var ent in leaves)
                {
                    entMan.DeleteEntity(ent);
                }
            }
        });
    }
    // CMU14 End

    // CMU14: upstream wave-based body, kept for downmerge flagging.
    // public async Task DeleteEntityTreeLeafFirst(EntityUid root)
    // {
    //     while (Server.EntMan.EntityExists(root))
    //     {
    //         var deleted = 0;
    //
    //         await Server.WaitPost(() =>
    //         {
    //             var entMan = Server.EntMan;
    //             if (!entMan.EntityExists(root))
    //                 return;
    //
    //             var xforms = entMan.GetEntityQuery<TransformComponent>();
    //             var leaves = entMan.GetEntities()
    //                 .Where(ent => IsInTree(ent, root, xforms) && IsLeaf(ent, xforms))
    //                 .ToArray();
    //
    //             deleted = leaves.Length;
    //             foreach (var ent in leaves)
    //             {
    //                 entMan.DeleteEntity(ent);
    //             }
    //         });
    //
    //         Assert.That(deleted, Is.GreaterThan(0), $"Unable to find leaf entities while deleting entity tree rooted at {root}.");
    //         await RunTicksSync(5);
    //     }
    // }

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
