#pragma warning disable RA0002 // Arrange depot ownership, lift completion and vehicle condition.

using System.Linq;
using System.Numerics;
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server._RMC14.Vehicle;
using Content.Server.CMU14.Round;
using Content.Shared._RMC14.Intel.Tech;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared._RMC14.Vehicle;
using Content.Shared._RMC14.Vehicle.Supply;
using Content.Shared._RMC14.Vendors;
using Content.Shared.CMU14;
using Content.Shared.CMU14.util;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.GameTicking;
using Content.Shared.UserInterface;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._RMC14;

// CMU14: exercise real queues, spawning, storage and vendor state across depots.
[TestFixture]
public sealed class VehicleSupplyPlatoonTest : GameTest
{
    private VehicleSupplySystem Supply => Server.System<VehicleSupplySystem>();

    private readonly record struct Depot(Entity<VehicleSupplyConsoleComponent> Console, Entity<VehicleSupplyLiftComponent> Lift);

    [TestCase("USCM", "VehicleTank", "VehicleSPPTank", true, true)]
    [TestCase("LACN", "VehicleAPC", "VehicleAPCCommand", true, true)]
    [TestCase("UPP", "VehicleSPPTank", "VehicleTank", false, false)]
    [TestCase("WEYU", "VehicleTankPMC", "VehicleTank", true, true)]
    [TestCase("CMBCIU", "AU14VehicleCivHSVan", "VehicleHumvee", false, true)]
    [TestCase("HAZOPS", "VehicleAev", "VehicleTank", false, true)]
    [TestCase("ProdigySF", "AU14VehicleCivTruck", "VehicleAPC", false, false)]
    [TestCase("VAIPO", "VehicleAPC", "VehicleTank", true, true)]
    [TestCase("RMC", "VehicleTankTWE", "VehicleHumvee", false, false)]
    public async Task PlatoonCatalogRestrictsChassisVtolAndParts(string platoon, string allowed, string excluded, bool armed, bool transport)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            Configure(map.GridCoords, platoon);
            var depot = CreateDepot(map.GridCoords);
            var ids = State(depot).Available.Select(entry => entry.Id).ToArray();
            Assert.That(ids, Does.Contain(allowed));
            Assert.That(ids, Does.Not.Contain(excluded));
            Assert.That(ids.Contains("VehicleBlackfootDoorGunVariant"), Is.EqualTo(armed));
            Assert.That(ids.Contains("VehicleBlackfootTransport"), Is.EqualTo(transport));
            Assert.That(ids, Does.Not.Contain("VehicleBlackfootRecon"));

            var vendor = SEntMan.SpawnEntity("VehicleHardpointVendor", map.GridCoords.Offset(new Vector2(3, 0)));
            var parts = Parts(vendor);
            Assert.That(parts.Contains("VehicleBlackfootDoorGun"), Is.EqualTo(armed));
            Assert.That(parts.Contains("VehicleBlackfootThrusters"), Is.EqualTo(transport));
            Assert.That(parts, Does.Not.Contain("VehicleHumveeARCCannon"));

            // A stale or forged client choice must also be rejected server-side.
            Queue(depot, excluded);
            Assert.That(depot.Lift.Comp.PendingVehicle, Is.Empty);
            Assert.That(depot.Lift.Comp.ActiveVehicle, Is.Null);
        });
    }

    [Test]
    public async Task ConcurrentLiftsShareTwoReservationsButOpposingSidesDoNot()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            Configure(map.GridCoords);
            var a = CreateDepot(map.GridCoords);
            var b = CreateDepot(map.GridCoords.Offset(new Vector2(30, 0)));
            var c = CreateDepot(map.GridCoords.Offset(new Vector2(60, 0)));
            var enemy = CreateDepot(map.GridCoords.Offset(new Vector2(90, 0)), "opfor");
            Queue(a, "VehicleHumvee");
            Queue(b, "VehicleHumveeTransport");
            Queue(c, "VehicleAPC");
            Queue(enemy, "VehicleSPPVan");
            Assert.That(a.Lift.Comp.PendingVehicle, Is.EqualTo("VehicleHumvee"));
            Assert.That(b.Lift.Comp.PendingVehicle, Is.EqualTo("VehicleHumveeTransport"));
            Assert.That(c.Lift.Comp.PendingVehicle, Is.Empty);
            Assert.That(enemy.Lift.Comp.PendingVehicle, Is.EqualTo("VehicleSPPVan"));
            Complete(a);
            Complete(b);
            Assert.That(State(c).IssuedVehicles, Has.Count.EqualTo(2));
            Assert.That(State(c).Available, Is.Empty);
            Assert.That(State(enemy).IssuedVehicles, Is.Empty);
        });
    }

    [TestCase("VehicleTank", "VehicleTank")]
    [TestCase("VehicleBlackfootDoorGunVariant", "VehicleBlackfootTransport")]
    [TestCase("CMUFighterGround", "CMUFighterGround")]
    public async Task TankAndVtolLimitsIncludePendingOrders(string first, string second)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            Configure(map.GridCoords);
            var a = CreateDepot(map.GridCoords);
            var b = CreateDepot(map.GridCoords.Offset(new Vector2(30, 0)));
            Queue(a, first);
            Queue(b, second);
            Assert.That(a.Lift.Comp.PendingVehicle, Is.EqualTo(first));
            Assert.That(b.Lift.Comp.PendingVehicle, Is.Empty);
            Complete(a);
            ResetLift(b);
            Queue(b, second);
            Assert.That(b.Lift.Comp.PendingVehicle, Is.Empty, "Issued vehicles also consume their category limit");
            ResetLift(b);
            Queue(b, "VehicleHumvee");
            Assert.That(b.Lift.Comp.PendingVehicle, Is.EqualTo("VehicleHumvee"));
            if (first == "CMUFighterGround")
                SEntMan.DeleteEntity(a.Lift.Comp.ActiveVehicle!.Value);
        });
    }

    [Test]
    public async Task FighterAllowanceIsSharedAcrossDepotsButSeparateForEachFaction()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            Configure(map.GridCoords);
            var first = CreateDepot(map.GridCoords);
            var second = CreateDepot(map.GridCoords.Offset(new Vector2(30, 0)));
            var enemy = CreateDepot(map.GridCoords.Offset(new Vector2(60, 0)), "opfor");
            Queue(first, "CMUFighterGround");
            Queue(second, "CMUFighterGround");
            Queue(enemy, "CMUFighterGround");
            Assert.That(first.Lift.Comp.PendingVehicle, Is.EqualTo("CMUFighterGround"));
            Assert.That(second.Lift.Comp.PendingVehicle, Is.Empty);
            Assert.That(enemy.Lift.Comp.PendingVehicle, Is.EqualTo("CMUFighterGround"));
            Complete(first);
            ResetLift(second);
            Assert.That(State(second).Available.Select(entry => entry.Id), Does.Not.Contain("CMUFighterGround"));
            Queue(second, "CMUFighterGround");
            Assert.That(second.Lift.Comp.PendingVehicle, Is.Empty);
            // Delete the aircraft while its landing map still exists, so its
            // cockpit is cleaned up before GameTest removes the map.
            SEntMan.DeleteEntity(first.Lift.Comp.ActiveVehicle!.Value);
        });
    }

    [Test]
    public async Task DestroyedVehiclesAndResearchCannotRefundAllowanceButPartsRemain()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            Configure(map.GridCoords);
            var depot = CreateDepot(map.GridCoords);
            var vendor = SEntMan.SpawnEntity("VehicleHardpointVendor", map.GridCoords.Offset(new Vector2(3, 0)));
            for (var i = 0; i < 2; i++)
            {
                ResetLift(depot);
                State(depot);
                Queue(depot, "VehicleHumvee");
                Complete(depot);
                Assert.That(depot.Lift.Comp.ActiveVehicle, Is.Not.Null);
                SEntMan.DeleteEntity(depot.Lift.Comp.ActiveVehicle!.Value);
            }

            SEntMan.EventBus.RaiseEvent(EventSource.Local, new TechUnlockVehicleEvent("VehicleHumvee") { Additional = true });
            SEntMan.EventBus.RaiseEvent(EventSource.Local, new TechUnlockVehicleEvent("all"));
            SEntMan.DeleteEntity(depot.Console);
            SEntMan.DeleteEntity(depot.Lift);
            var replacement = CreateDepot(map.GridCoords);
            var state = State(replacement);
            Assert.That(state.IssuedVehicles, Has.Count.EqualTo(2));
            Assert.That(state.Available, Is.Empty);
            Assert.That(Parts(vendor), Does.Contain("VehicleHumveeWheel"));
            Assert.That(Parts(vendor), Does.Not.Contain("VehicleHumveeARCCannon"));
            Queue(replacement, "VehicleHumvee");
            Assert.That(replacement.Lift.Comp.PendingVehicle, Is.Empty);

            Invoke("OnSupplyRoundRestart", new RoundRestartCleanupEvent());
            Assert.That(State(replacement).IssuedVehicles, Is.Empty);
            Assert.That(State(replacement).Available, Is.Not.Empty);
        });
    }

    [Test]
    public async Task ReturnedBlackfootKeepsConditionAndEquipmentWithoutAnotherChargeOrBundle()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            Configure(map.GridCoords);
            var depot = CreateDepot(map.GridCoords);
            var second = CreateDepot(map.GridCoords.Offset(new Vector2(30, 0)));
            Queue(depot, "VehicleBlackfootTransport");
            Complete(depot);
            var vehicle = depot.Lift.Comp.ActiveVehicle!.Value;
            var integrity = SEntMan.GetComponent<HardpointIntegrityComponent>(vehicle);
            integrity.Integrity = 42;
            var slots = Server.System<ItemSlotsSystem>();
            Assert.That(slots.TryGetSlot(vehicle, "launchers", out var launcher), Is.True);
            var originalLauncher = launcher!.Item;
            Assert.That(originalLauncher, Is.Not.Null);
            var count = BundleCount();
            Assert.That(count, Is.EqualTo(4));

            Queue(second, "VehicleHumvee");
            Complete(second);
            Invoke("StoreVehicle", depot.Lift);
            ResetLift(depot);
            Assert.That(State(depot).Available.Select(entry => entry.Id), Does.Contain("VehicleBlackfootTransport"));
            depot.Console.Comp.SelectedLoadouts["primary"] = "VehicleBlackfootLaunchers";
            Queue(depot, "VehicleBlackfootTransport");
            Assert.That(depot.Lift.Comp.PendingVehicleEntity, Is.EqualTo(vehicle));
            Assert.That(depot.Lift.Comp.PendingLoadouts, Is.Empty);
            Assert.That(depot.Lift.Comp.PendingBundle, Is.Empty);
            Complete(depot);
            Assert.That(depot.Lift.Comp.ActiveVehicle, Is.EqualTo(vehicle));
            Assert.That(integrity.Integrity, Is.EqualTo(42));
            Assert.That(launcher.Item, Is.EqualTo(originalLauncher));
            Assert.That(BundleCount(), Is.EqualTo(count));
            Assert.That(State(depot).IssuedVehicles, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public async Task ExhaustedAllowanceListsOnlyRealStoredVehicles()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            Configure(map.GridCoords);
            var depot = CreateDepot(map.GridCoords);
            var second = CreateDepot(map.GridCoords.Offset(new Vector2(30, 0)));
            Queue(depot, "VehicleHumvee");
            Complete(depot);
            State(depot); // A second new Humvee is still available at this point.
            var vehicle = depot.Lift.Comp.ActiveVehicle!.Value;
            Invoke("StoreVehicle", depot.Lift);
            ResetLift(depot);
            Queue(second, "VehicleAPC");
            Complete(second);
            var available = State(depot).Available.Single();
            Assert.That(available.Id, Is.EqualTo("VehicleHumvee"));
            Assert.That(available.Count, Is.EqualTo(1));
            Queue(depot, "VehicleHumvee");
            Complete(depot);
            Assert.That(depot.Lift.Comp.ActiveVehicle, Is.EqualTo(vehicle));
            Assert.That(State(depot).IssuedVehicles, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public async Task UnassignedDepotStaysClosedUntilPlatoonIsSelectedAndPendingOrdersRevalidate()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            Server.System<PlatoonSpawnRuleSystem>().SelectedGovforPlatoon = null;
            var depot = CreateDepot(map.GridCoords);
            Assert.That(State(depot).Available, Is.Empty);
            Configure(map.GridCoords);
            Assert.That(State(depot).Available.Select(entry => entry.Id), Does.Contain("VehicleTank"));
            Queue(depot, "VehicleTank");
            Server.System<PlatoonSpawnRuleSystem>().SelectedGovforPlatoon = SProtoMan.Index<PlatoonPrototype>("CMBCIU");
            Complete(depot);
            Assert.That(depot.Lift.Comp.ActiveVehicle, Is.Null);
            Assert.That(State(depot).IssuedVehicles, Is.Empty);
            Assert.That(State(depot).Available.Select(entry => entry.Id), Does.Not.Contain("VehicleTank"));
        });
    }

    [Test]
    public async Task GroundsideDepotsInheritNearestRequisitionsSide()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            Configure(map.GridCoords);
            SEntMan.RemoveComponent<ShipFactionComponent>(map.GridCoords.EntityId);
            var req = SEntMan.SpawnEntity(null, map.GridCoords);
            SEntMan.AddComponent<RequisitionsComputerComponent>(req).Faction = "opfor";
            var depot = CreateDepot(map.GridCoords, null);
            Assert.That(Supply.GetSupplySide(depot.Console), Is.EqualTo("opfor"));
            Assert.That(State(depot).Available.Select(entry => entry.Id), Does.Contain("VehicleSPPVan"));
            Assert.That(State(depot).Available.Select(entry => entry.Id), Does.Not.Contain("VehicleHumvee"));
        });
    }

    private void Configure(EntityCoordinates coordinates, string platoon = "USCM")
    {
        // Pooled tests delete maps without restarting the round's systems.
        Invoke("OnSupplyRoundRestart", new RoundRestartCleanupEvent());
        // The default test grid is one tile. Keep depots and their vendors on the ship grid.
        var maps = Server.System<SharedMapSystem>();
        for (var x = -6; x < 100; x++)
        for (var y = -6; y < 8; y++)
            maps.SetTile(coordinates.EntityId, Pair.TestMap!.Grid.Comp, coordinates.Offset(new Vector2(x, y)), Pair.TestMap.Tile.Tile);
        SEntMan.EnsureComponent<ShipFactionComponent>(coordinates.EntityId).Faction = "govfor";
        var platoons = Server.System<PlatoonSpawnRuleSystem>();
        platoons.SelectedGovforPlatoon = SProtoMan.Index<PlatoonPrototype>(platoon);
        platoons.SelectedOpforPlatoon = SProtoMan.Index<PlatoonPrototype>("UPP");
    }

    private Depot CreateDepot(EntityCoordinates coordinates, string? side = "govfor")
    {
        var lift = SEntMan.SpawnEntity("VehicleLift", coordinates);
        var console = SEntMan.SpawnEntity("VehicleSupplyConsole", coordinates.Offset(new Vector2(0, 3)));
        var comp = SEntMan.GetComponent<VehicleSupplyConsoleComponent>(console);
        comp.Faction = side;
        var depot = new Depot((console, comp), (lift, SEntMan.GetComponent<VehicleSupplyLiftComponent>(lift)));
        State(depot);
        return depot;
    }

    private VehicleSupplyBuiState State(Depot depot)
    {
        var ev = new BeforeActivatableUIOpenEvent(depot.Console);
        SEntMan.EventBus.RaiseLocalEvent(depot.Console, ev);
        Assert.That(Server.System<SharedUserInterfaceSystem>().TryGetUiState<VehicleSupplyBuiState>(depot.Console.Owner, VehicleSupplyUIKey.Key, out var state), Is.True);
        return state!;
    }

    private string[] Parts(EntityUid vendor)
    {
        Assert.That(Supply.GetSupplySide(vendor), Is.EqualTo("govfor"), "The parts vendor must be on its owning ship");
        var ev = new BeforeActivatableUIOpenEvent(vendor);
        SEntMan.EventBus.RaiseLocalEvent(vendor, ev);
        return SEntMan.GetComponent<CMAutomatedVendorComponent>(vendor).Sections.SelectMany(section => section.Entries).Select(entry => entry.Id.Id).ToArray();
    }

    private void Queue(Depot depot, string vehicle)
    {
        depot.Console.Comp.SelectedVehicle = vehicle;
        Invoke("TryToggleLift", depot.Console, depot.Lift, true, null!);
    }

    private void Complete(Depot depot)
    {
        Invoke("SpawnVehicle", depot.Lift);
        depot.Lift.Comp.Mode = VehicleSupplyLiftMode.Raised;
        depot.Lift.Comp.NextMode = null;
        depot.Lift.Comp.ToggledAt = null;
        depot.Lift.Comp.Busy = false;
    }

    private static void ResetLift(Depot depot)
    {
        depot.Lift.Comp.Mode = VehicleSupplyLiftMode.Lowered;
        depot.Lift.Comp.NextMode = null;
        depot.Lift.Comp.ToggledAt = null;
        depot.Lift.Comp.Busy = false;
    }

    private int BundleCount()
    {
        var count = 0;
        var query = SEntMan.EntityQueryEnumerator<MetaDataComponent>();
        while (query.MoveNext(out _, out var meta))
        {
            if (meta.EntityPrototype?.ID is "CMUBlackfootLandingPadFoldedProp" or "CMUBlackfootFuelPumpCrate" or "CMUBlackfootFlightComputerCrate" or "CMUBlackfootAerospaceTug")
                count++;
        }
        return count;
    }

    private void Invoke(string method, params object[] args) =>
        typeof(VehicleSupplySystem).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(Supply, args);
}
