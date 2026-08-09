using System.Collections.Generic;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.AU14.Round;
using Content.Shared._RMC14.Dropship;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.Round;

[TestFixture]
[TestOf(typeof(PlatoonSpawnRuleSystem))]
public sealed class PlatoonSpawnRuleInventoryTest : GameTest
{
    [Test]
    public async Task InitialSetupInventoryKeepsMultipleShipsIsolated()
    {
        var govforMap = await Pair.CreateTestMap();
        var opforMap = await Pair.CreateTestMap();
        var spawned = new List<EntityUid>();

        await Server.WaitAssertion(() =>
        {
            var govforShip = SEntMan.SpawnEntity(InventoryShip, govforMap.GridCoords);
            var opforShip = SEntMan.SpawnEntity(InventoryShip, opforMap.GridCoords);
            var govforMarker = SEntMan.SpawnEntity(
                InventoryMarker,
                new EntityCoordinates(govforShip, Vector2.Zero));
            var opforMarker = SEntMan.SpawnEntity(
                InventoryMarker,
                new EntityCoordinates(opforShip, Vector2.Zero));
            var govforPhone = SEntMan.SpawnEntity(
                InventoryPhone,
                new EntityCoordinates(govforShip, Vector2.Zero));
            var opforPhone = SEntMan.SpawnEntity(
                InventoryPhone,
                new EntityCoordinates(opforShip, Vector2.Zero));
            spawned.Add(govforShip);
            spawned.Add(opforShip);
            spawned.Add(govforMarker);
            spawned.Add(opforMarker);
            spawned.Add(govforPhone);
            spawned.Add(opforPhone);

            var system = Server.System<PlatoonSpawnRuleSystem>();
            var inventory = system.CaptureInitialSetupInventory(true);

            Assert.Multiple(() =>
            {
                Assert.That(inventory.Ships, Does.Contain(govforShip));
                Assert.That(inventory.Ships, Does.Contain(opforShip));
                Assert.That(inventory.VendorMarkers.IndexOf(govforMarker),
                    Is.LessThan(inventory.VendorMarkers.IndexOf(opforMarker)));
                Assert.That(inventory.GetShipMarkers(govforShip), Does.Contain(govforMarker));
                Assert.That(inventory.GetShipMarkers(govforShip), Does.Not.Contain(opforMarker));
                Assert.That(inventory.GetShipMarkers(opforShip), Does.Contain(opforMarker));
                Assert.That(inventory.GetShipMarkers(opforShip), Does.Not.Contain(govforMarker));
                Assert.That(inventory.GetShipPhones(govforShip), Does.Contain(govforPhone));
                Assert.That(inventory.GetShipPhones(govforShip), Does.Not.Contain(opforPhone));
                Assert.That(inventory.GetShipPhones(opforShip), Does.Contain(opforPhone));
                Assert.That(inventory.GetShipPhones(opforShip), Does.Not.Contain(govforPhone));
            });
        });

        await Server.WaitPost(() =>
        {
            foreach (var entity in spawned)
            {
                if (SEntMan.EntityExists(entity))
                    SDeleteNow(entity);
            }
        });
        await Pair.RunUntilSynced();
    }

    [Test]
    public async Task CapturesNestedGridSetupFactsAndRevalidatesNavigationComputer()
    {
        var map = await Pair.CreateTestMap();
        var spawned = new List<EntityUid>();

        await Server.WaitAssertion(() =>
        {
            var parent = SEntMan.SpawnEntity(null, map.GridCoords);
            var marker = SEntMan.SpawnEntity(
                InventoryMarker,
                new EntityCoordinates(parent, Vector2.Zero));
            var facts = SEntMan.SpawnEntity(
                InventoryFacts,
                new EntityCoordinates(parent, Vector2.Zero));
            spawned.Add(parent);
            spawned.Add(marker);
            spawned.Add(facts);

            var system = Server.System<PlatoonSpawnRuleSystem>();
            var inventory = system.CaptureGridSetupInventory(map.Grid);

            Assert.Multiple(() =>
            {
                Assert.That(inventory.GetMarkers(InventoryMarker), Is.EqualTo(new[] { marker }));
                Assert.That(inventory.Ladders, Does.Contain(facts));
                Assert.That(inventory.NavigationComputers, Does.Contain(facts));
                Assert.That(inventory.Phones, Does.Contain(facts));
                Assert.That(system.FindNavComputerOnGrid(inventory), Is.EqualTo(facts));
            });

            var transform = Server.System<SharedTransformSystem>();
            transform.SetCoordinates(facts, new EntityCoordinates(map.MapUid, Vector2.Zero));
            Assert.That(system.FindNavComputerOnGrid(inventory), Is.Null,
                "The grid inventory retained a navigation computer that moved off the grid after capture.");

            transform.SetCoordinates(facts, new EntityCoordinates(parent, Vector2.Zero));
            Assert.That(system.FindNavComputerOnGrid(inventory), Is.EqualTo(facts));

            SEntMan.RemoveComponent<DropshipNavigationComputerComponent>(facts);
            Assert.That(system.FindNavComputerOnGrid(inventory), Is.Null,
                "The grid inventory retained a navigation component that was removed after capture.");

            SEntMan.EnsureComponent<DropshipNavigationComputerComponent>(facts);
            Assert.That(system.FindNavComputerOnGrid(inventory), Is.EqualTo(facts));

            SDeleteNow(facts);
            Assert.That(system.FindNavComputerOnGrid(inventory), Is.Null,
                "The grid inventory retained a deleted navigation computer.");
        });

        await Server.WaitPost(() =>
        {
            foreach (var entity in spawned)
            {
                if (SEntMan.EntityExists(entity))
                    SDeleteNow(entity);
            }
        });
        await Pair.RunUntilSynced();
    }

    [Test]
    public async Task DestinationPoolUsesCurrentFactsAndDoesNotReuseDestinations()
    {
        var map = await Pair.CreateTestMap();
        var spawned = new List<EntityUid>();

        await Server.WaitAssertion(() =>
        {
            var changed = SEntMan.SpawnEntity(TestDestination, map.GridCoords);
            var eligible = SEntMan.SpawnEntity(TestDestination, map.GridCoords);
            spawned.Add(changed);
            spawned.Add(eligible);

            var dropship = Server.System<SharedDropshipSystem>();
            var system = Server.System<PlatoonSpawnRuleSystem>();
            var pool = system.CaptureDestinationPool();

            dropship.SetFactionController(changed, "cmu-test-changed");

            Assert.Multiple(() =>
            {
                Assert.That(
                    system.FindDestination(
                        TestFaction,
                        DropshipDestinationComponent.DestinationType.Bigship,
                        pool,
                        map.Grid),
                    Is.EqualTo(eligible));
                Assert.That(
                    system.FindDestination(
                        TestFaction,
                        DropshipDestinationComponent.DestinationType.Bigship,
                        pool,
                        map.Grid),
                    Is.Null,
                    "A destination was reused after being handed to a shuttle.");
            });
        });

        await Server.WaitPost(() =>
        {
            foreach (var entity in spawned)
            {
                if (SEntMan.EntityExists(entity))
                    SDeleteNow(entity);
            }
        });
        await Pair.RunUntilSynced();
    }

    private const string InventoryFacts = "CMUTestPlatoonGridInventoryFacts";
    private const string InventoryMarker = "CMUTestPlatoonGridInventoryMarker";
    private const string InventoryPhone = "CMUTestPlatoonInitialInventoryPhone";
    private const string InventoryShip = "CMUTestPlatoonInitialInventoryShip";
    private const string TestDestination = "CMUTestPlatoonDestination";
    private const string TestFaction = "cmu-test-platoon";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: CMUTestPlatoonGridInventoryMarker
  components:
  - type: VendorMarker

- type: entity
  id: CMUTestPlatoonInitialInventoryPhone
  components:
  - type: RotaryPhone

- type: entity
  id: CMUTestPlatoonInitialInventoryShip
  components:
  - type: ShipFaction
    faction: cmu-test-platoon

- type: entity
  id: CMUTestPlatoonGridInventoryFacts
  components:
  - type: DropshipDestination
    FactionControlling: cmu-test-platoon
    destinationtype: Bigship
  - type: DropshipNavigationComputer
  - type: Ladder
  - type: RotaryPhone

- type: entity
  id: CMUTestPlatoonDestination
  components:
  - type: DropshipDestination
    FactionControlling: cmu-test-platoon
    destinationtype: Bigship
";
}
