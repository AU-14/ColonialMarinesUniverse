using System.Numerics;
using Content.IntegrationTests.Pair;
using Content.Shared._RMC14.Xenonids.Hive;

namespace Content.IntegrationTests._RMC14;

[TestFixture]
public sealed class BurrowedLarvaSpawnPointTest
{
    [TestCase("CMXenoLesserDrone")]
    [TestCase("RMCXenoLesserCarrier")]
    [TestCase("CMXenoParasite")]
    [TestCase("CMXenoLarva")]
    public async Task TierZeroXenosDoNotProvideBurrowedLarvaSpawnPoint(string xenoPrototype)
    {
        await AssertBurrowedLarvaSpawnPoint(xenoPrototype, false);
    }

    [TestCase("CMXenoRunner")]
    [TestCase("CMXenoQueen")]
    public async Task ValidXenosProvideBurrowedLarvaSpawnPoint(string xenoPrototype)
    {
        await AssertBurrowedLarvaSpawnPoint(xenoPrototype, true);
    }

    private static async Task AssertBurrowedLarvaSpawnPoint(string xenoPrototype, bool expected)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hiveSystem = entMan.System<SharedXenoHiveSystem>();
            var hive = entMan.SpawnEntity("CMXenoHive", map.GridCoords);
            var xeno = entMan.SpawnEntity(xenoPrototype, map.GridCoords.Offset(new Vector2(1, 0)));

            try
            {
                hiveSystem.SetHive(xeno, hive);
                var hiveComp = entMan.GetComponent<HiveComponent>(hive);

                Assert.That(hiveSystem.HasBurrowedLarvaSpawnPoint((hive, hiveComp)), Is.EqualTo(expected));
            }
            finally
            {
                entMan.DeleteEntity(xeno);
                entMan.DeleteEntity(hive);
            }
        });

        await pair.CleanReturnAsync();
    }
}
