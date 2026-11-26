using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Government;
using Content.Server.CMU14.WorldMarket;
using Content.Shared.CMU14.ColonyEconomy;
using Robust.Shared.Containers;
using Content.Shared._RMC14.Dropship;
using Content.Shared.CMU14.Government;

namespace Content.IntegrationTests.CMU.Shuttles;

public sealed class CMUTradeShuttleReservationTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    [Test]
    public async Task CityOrderCapsOrdinaryGlassAcrossCratesAndPersonalDeliveries()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var state = new ColonyGovernmentComponent { Shuttle = map.Grid.Owner };
            var city = new WorldMarketCityInfo("test", "test", [], "", 0);
            state.WorldOrders.Add(new WorldOrder(1, city.Id, "RUMCGoodGlass", 100, 100, SGameTiming.CurTime + TimeSpan.FromMinutes(5)));
            state.Shipment.Add(new ShipmentLine("RUMCGoodGlass", 10, "owner", map.MapUid, unmarked: true));
            var crate = SSpawnAtPosition("RMCCrateBase", map.GridCoords);
            var containers = Server.System<SharedContainerSystem>();
            var storage = containers.GetContainer(crate, "entity_storage");
            var purchased = SSpawnAtPosition("CMSheetGlass", map.GridCoords);
            var produced = SSpawnAtPosition("CMSheetGlass1", map.GridCoords);
            SEntMan.EnsureComponent<ASRSSellableComponent>(produced);
            Assert.That(containers.Insert(purchased, storage), Is.True);
            Assert.That(containers.Insert(produced, storage), Is.True);

            // The produced sheet earns 19 plus its 100% bonus, the 60 ordinary sheets earn 5 each.
            var method = typeof(ColonyGovernmentSystem).GetMethod("Appraise", BindingFlags.NonPublic | BindingFlags.Instance)!;
            int Appraise(bool settle) => (int) method.Invoke(Server.System<ColonyGovernmentSystem>(), [state, city, settle, null])!;
            Assert.That(Appraise(false), Is.EqualTo(338));
            Assert.That(Appraise(false), Is.EqualTo(338));
            Assert.That(Appraise(true), Is.EqualTo(338));
            Assert.That(state.WorldOrders[0].Filled, Is.EqualTo(61));
        });
    }

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
