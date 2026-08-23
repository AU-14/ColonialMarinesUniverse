using Content.Server._CMU14.ZLevels.Core;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared._CMU14.ZLevels.Core.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14;

[TestFixture]
public sealed class ZLevelNetworkTest
{
    [Test]
    public async Task GetAllNetworkMapsReturnsWholeNetwork()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        EntityUid network = default;
        EntityUid lowerMap = default;
        EntityUid shipMap = default;
        EntityUid upperMap = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var zLevels = entMan.System<CMUZLevelsSystem>();

                lowerMap = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
                shipMap = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
                upperMap = entMan.SpawnEntity(null, MapCoordinates.Nullspace);

                var networkEnt = zLevels.CreateZNetwork();
                network = networkEnt;

                Assert.That(zLevels.TryAddMapsIntoZNetwork(networkEnt, new()
                {
                    [lowerMap] = 0,
                    [shipMap] = 1,
                    [upperMap] = 2,
                }), Is.True);

                var result = zLevels.GetAllNetworkMaps(shipMap);
                Assert.That(result, Is.EquivalentTo(new[] { lowerMap, shipMap, upperMap }));
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                foreach (var uid in new[] { network, lowerMap, shipMap, upperMap })
                {
                    if (server.EntMan.EntityExists(uid))
                        server.EntMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task GetAllNetworkMapsWithoutNetworkReturnsOnlySelf()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        EntityUid loner = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                loner = entMan.SpawnEntity(null, MapCoordinates.Nullspace);

                var result = entMan.System<CMUSharedZLevelsSystem>().GetAllNetworkMaps(loner);
                Assert.That(result, Is.EquivalentTo(new[] { loner }));
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                if (server.EntMan.EntityExists(loner))
                    server.EntMan.DeleteEntity(loner);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task GetAllNetworkMapsReturnsConnectedZLevelsForShipMap()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        EntityUid network = default;
        EntityUid lowerMap = default;
        EntityUid shipMap = default;
        EntityUid upperMap = default;
        EntityUid higherMap = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var zLevels = entMan.System<CMUZLevelsSystem>();

                lowerMap = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
                shipMap = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
                upperMap = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
                higherMap = entMan.SpawnEntity(null, MapCoordinates.Nullspace);

                var networkEnt = zLevels.CreateZNetwork();
                network = networkEnt;

                Assert.That(zLevels.TryAddMapsIntoZNetwork(networkEnt, new()
                {
                    [lowerMap] = 0,
                    [shipMap] = 1,
                    [upperMap] = 2,
                    [higherMap] = 3,
                }), Is.True);

                var result = zLevels.GetAllNetworkMaps(shipMap);
                Assert.That(result, Is.EquivalentTo(new[] { shipMap, lowerMap, upperMap, higherMap }));
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                foreach (var uid in new[] { network, lowerMap, shipMap, upperMap, higherMap })
                {
                    if (server.EntMan.EntityExists(uid))
                        server.EntMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }
}
