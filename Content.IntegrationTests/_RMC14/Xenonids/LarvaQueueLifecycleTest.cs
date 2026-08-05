using Content.IntegrationTests.Pair;
using Content.Server.Mind;
using Content.Shared.Mind.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._RMC14.Xenonids;

[TestFixture]
public sealed class LarvaQueueLifecycleTest
{
    [Test]
    public async Task DeletingPossessedXenoDoesNotQueueTerminatingEntity()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
            DummyTicker = true,
        });

        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entityManager = server.EntMan;
        var mindSystem = entityManager.System<MindSystem>();
        var player = server.PlayerMan.Sessions.Single();

        EntityUid xeno = default;
        await server.WaitAssertion(() =>
        {
            xeno = entityManager.SpawnEntity("CMUXenoBurrowerHL", map.GridCoords);

            var mind = mindSystem.CreateMind(player.UserId, "Young Burrower");
            mindSystem.TransferTo(mind, xeno);

            Assert.That(entityManager.GetComponent<MindContainerComponent>(xeno).HasMind, Is.True);
        });

        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            entityManager.DeleteEntity(xeno);
            Assert.That(entityManager.Deleted(xeno), Is.True);
        });

        await pair.RunTicksSync(5);
        await pair.CleanReturnAsync();
    }
}
