using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server._RMC14.Dropship;
using Content.Shared._RMC14.Dropship;
using Content.Shared.CMU14.Round;
using Content.Shared.Shuttles.Components;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class HunterShipDropshipDestinationIsolationTest
{

    [Test]
    public async Task NonYautjaFlyToRequestsAreRejectedBeforeFtl()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid destination = default;
        EntityUid ertConsole = default;
        EntityUid ordinaryConsole = default;
        EntityUid yautjaConsole = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            entMan.EnsureComponent<ShuttleComponent>(map.Grid.Owner);

            destination = entMan.SpawnEntity("CMUHunterShipYautjaLandingPadAFTLBeacon", map.GridCoords.Offset(new Vector2(8, 0)));
            ertConsole = entMan.SpawnEntity("CMComputerDropshipNavigationThirdParty", map.GridCoords);
            ordinaryConsole = entMan.SpawnEntity("CMComputerDropshipNavigationOpfor", map.GridCoords.Offset(new Vector2(2, 0)));
            yautjaConsole = entMan.SpawnEntity("CMUYautjaHunterShuttleConsole", map.GridCoords.Offset(new Vector2(4, 0)));

            var transform = entMan.System<SharedTransformSystem>();
            transform.SetCoordinates(ertConsole, map.GridCoords);
            transform.SetCoordinates(ordinaryConsole, map.GridCoords.Offset(new Vector2(2, 0)));
            transform.SetCoordinates(yautjaConsole, map.GridCoords.Offset(new Vector2(4, 0)));
        });

        await pair.RunTicksSync(1);

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var dropship = entMan.System<DropshipSystem>();
            var ertComputer = (ertConsole, entMan.GetComponent<DropshipNavigationComputerComponent>(ertConsole));
            var ordinaryComputer = (ordinaryConsole, entMan.GetComponent<DropshipNavigationComputerComponent>(ordinaryConsole));
            var yautjaComputer = (yautjaConsole, entMan.GetComponent<DropshipNavigationComputerComponent>(yautjaConsole));

            Assert.That(entMan.GetComponent<TransformComponent>(yautjaConsole).GridUid, Is.EqualTo(map.Grid.Owner));
            Assert.That(entMan.HasComponent<ShuttleComponent>(map.Grid.Owner), Is.True);

            Assert.That(dropship.FlyTo(ertComputer, destination, null), Is.False);
            Assert.That(dropship.FlyTo(ordinaryComputer, destination, null), Is.False);
            Assert.That(entMan.HasComponent<FTLComponent>(map.Grid.Owner), Is.False);
            Assert.That(entMan.GetComponent<DropshipDestinationComponent>(destination).Ship, Is.Null);

            Assert.That(dropship.FlyTo(yautjaComputer, destination, null), Is.True);
            Assert.That(entMan.GetComponent<DropshipDestinationComponent>(destination).Ship, Is.EqualTo(map.Grid.Owner));
        });

        await pair.CleanReturnAsync();
    }
}
