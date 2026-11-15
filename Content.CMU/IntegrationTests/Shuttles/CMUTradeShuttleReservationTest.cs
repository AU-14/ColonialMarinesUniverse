using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Government;
using Content.Shared._RMC14.Dropship;
using Content.Shared.CMU14.Government;

namespace Content.IntegrationTests.CMU.Shuttles;

public sealed class CMUTradeShuttleReservationTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    [Test]
    public async Task RetiringATradeShuttleReleasesItsLandingZoneReservation()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var grid = Server.System<SharedMapSystem>().CreateGridEntity(map.MapId);
            var marker = SEntMan.SpawnEntity("CMDropshipDestinationThirdPartyReturn", map.GridCoords);
            Server.System<SharedDropshipSystem>().SetDestinationShip(marker, grid);
            var government = SEntMan.SpawnEntity(null, map.GridCoords);
            var state = SEntMan.AddComponent<ColonyGovernmentComponent>(government);
            state.Shuttle = grid;
            typeof(ColonyGovernmentSystem).GetMethod("ClearShuttle", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(Server.System<ColonyGovernmentSystem>(), new object[] { state });
            Assert.That(SComp<DropshipDestinationComponent>(marker).Ship, Is.Null,
                "Timeout or abort cleanup must not leave a deleted trade ship reserving a landing zone.");
        });
    }
}
