using Content.Shared.Actions.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._CMU14.Xenonids;

[TestFixture]
public sealed class CMUXenoZJumpIntegrationTest
{
    [Test]
    public async Task XenoReceivesZJumpActionOnMapInit()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var xeno = entMan.SpawnEntity("CMXenoRunner", map.GridCoords);

            try
            {
                Assert.That(HasAction(entMan, xeno, "CMUActionXenoZJump"), Is.True);
            }
            finally
            {
                entMan.DeleteEntity(xeno);
            }
        });

        await pair.CleanReturnAsync();
    }

    private static bool HasAction(IEntityManager entMan, EntityUid user, string prototype)
    {
        if (!entMan.TryGetComponent<ActionsComponent>(user, out var actions))
            return false;

        foreach (var action in actions.Actions)
        {
            if (entMan.GetComponent<MetaDataComponent>(action).EntityPrototype?.ID == prototype)
                return true;
        }

        return false;
    }
}
