using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.CMU14.Vehicle.Jeep;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Standing;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Components;
using Content.Shared._RMC14.Medical.Surgery;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared._RMC14.Vehicle;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Vehicle;

/// <summary>
/// The jeep's lamps come off and go on one by one, its key lets it run, the medical jeep lays its
/// patient down on an operating table, the kits are on the platoon catalogs in price order, and a
/// wreck outlasts the fire it leaves under itself.
/// </summary>
[TestFixture]
public sealed class CMUJeepLampsMedicalTest : GameTest
{
    private async Task<(EntityUid Jeep, EntityUid User)> SpawnJeep(string prototype)
    {
        var map = await Pair.CreateTestMap();
        EntityUid jeep = default;
        EntityUid user = default;
        await Server.WaitPost(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            for (var x = -5; x <= 5; x++)
            {
                for (var y = -5; y <= 5; y++)
                {
                    maps.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(x, y), map.Tile.Tile);
                }
            }

            // Facing south, the driver's side is +x and the front is -y; the user stands by the
            // driver's front corner.
            jeep = SEntMan.SpawnEntity(prototype, new EntityCoordinates(map.Grid, 0.5f, 0.5f));
            user = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 1.45f, -0.3f));
        });
        await RunTicksSync(5);
        return (jeep, user);
    }

    private EntityUid Part(EntityUid jeep, string id)
    {
        return SEntMan.GetComponent<CMUJeepComponent>(jeep).PartEntities
            .First(part => SEntMan.GetComponent<CMUVehiclePartComponent>(part).Part == id);
    }

    private void Click(EntityUid user, EntityUid target)
    {
        SEntMan.System<SharedInteractionSystem>()
            .UserInteraction(user, SEntMan.GetComponent<TransformComponent>(target).Coordinates, target);
    }

    private void Hold(EntityUid user, string? prototype)
    {
        var hands = SEntMan.System<SharedHandsSystem>();
        foreach (var held in hands.EnumerateHeld(user).ToList())
        {
            SEntMan.DeleteEntity(held);
        }

        if (prototype == null)
            return;

        var item = SEntMan.SpawnEntity(prototype, SEntMan.GetComponent<TransformComponent>(user).Coordinates);
        Assert.That(hands.TryPickup(user, item), Is.True);
    }

    private bool CanRun(EntityUid jeep)
    {
        var ev = new VehicleCanRunEvent((jeep, SEntMan.GetComponent<VehicleComponent>(jeep)));
        SEntMan.EventBus.RaiseLocalEvent(jeep, ref ev);
        return ev.CanRun;
    }

    [Test]
    public async Task LampComesOffWithAScrewdriverAndItsBeamGoesDark()
    {
        var (jeep, user) = await SpawnJeep("CMUVehicleJeepCargo");
        const string slot = "jeep-headlight-left";
        EntityUid lamp = default;

        // An empty hand leaves a lamp alone; a screwdriver takes it off.
        await Server.WaitPost(() =>
        {
            lamp = Part(jeep, CMUJeepSystem.DriverHeadlight);
            Hold(user, null);
            Click(user, lamp);
            Assert.That(SEntMan.System<ItemSlotsSystem>().GetItemOrNull(jeep, slot), Is.Not.Null);
            Hold(user, "CMScrewdriver");
            Click(user, lamp);
        });
        await RunSeconds(3f);

        await Server.WaitPost(() =>
        {
            var jeeps = SEntMan.System<CMUJeepSystem>();
            Assert.That(SEntMan.System<ItemSlotsSystem>().GetItemOrNull(jeep, slot), Is.Null,
                "A screwdriver should take the headlight off.");
            Assert.That(jeeps.LampWorks(jeep, CMUJeepSystem.DriverHeadlight), Is.False);
            Assert.That(jeeps.LampWorks(jeep, CMUJeepSystem.PassengerHeadlight), Is.True);

            // Only the passenger's beam lights with the driver's headlight gone.
            var lights = SEntMan.GetComponent<CMUVehicleHeadlightsComponent>(jeep);
            jeeps.CycleHeadlights((jeep, lights), user);
            var lightSystem = SEntMan.System<SharedPointLightSystem>();
            var lit = lights.Beams
                .Where(beam => lightSystem.TryGetLight(beam, out var light) && light.Enabled)
                .Select(beam => SEntMan.GetComponent<CMUVehicleHeadlightBeamComponent>(beam).Driver)
                .ToList();
            Assert.That(lit, Is.EqualTo(new[] { false }));

            // A headlight in hand goes back on where it is clicked.
            Hold(user, "CMUJeepHeadlight");
            Click(user, lamp);
        });
        await RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.System<ItemSlotsSystem>().GetItemOrNull(jeep, slot), Is.Not.Null);
            var lights = SEntMan.GetComponent<CMUVehicleHeadlightsComponent>(jeep);
            var lightSystem = SEntMan.System<SharedPointLightSystem>();
            Assert.That(lights.Beams.Count(beam => lightSystem.TryGetLight(beam, out var light) && light.Enabled), Is.EqualTo(2));
        });
    }

    [Test]
    public async Task TheJeepOnlyRunsWithItsKeyIn()
    {
        var (jeep, _) = await SpawnJeep("CMUVehicleJeepCargo");
        await Server.WaitAssertion(() =>
        {
            var slots = SEntMan.System<ItemSlotsSystem>();
            var ignition = SEntMan.GetComponent<CMUVehicleIgnitionComponent>(jeep);
            Assert.That(ignition.KeySlot.HasItem, Is.True, "A jeep comes with its key in.");
            Assert.That(CanRun(jeep), Is.True);

            Assert.That(slots.TryEject(jeep, ignition.KeySlot, null, out var key), Is.True);
            Assert.That(CanRun(jeep), Is.False, "Without its key the jeep can't run.");

            Assert.That(slots.TryInsert(jeep, CMUVehicleIgnitionComponent.SlotId, key!.Value, null), Is.True);
            Assert.That(CanRun(jeep), Is.True);
        });
    }

    [Test]
    public async Task MedicalJeepLaysItsPatientOnAnOperatingTable()
    {
        var (jeep, user) = await SpawnJeep("CMUVehicleJeepMedical");
        EntityUid bed = default;
        EntityUid patient = default;

        await Server.WaitPost(() =>
        {
            var seats = SEntMan.GetComponent<CMUVehicleSeatsComponent>(jeep).SeatEntities;
            Assert.That(seats, Has.Count.EqualTo(3));
            var passenger = seats.Single(seat => SEntMan.GetComponent<CMUVehicleSeatComponent>(seat).Reversed);
            Assert.That(SEntMan.GetComponent<CMUVehicleSeatComponent>(passenger).Driver, Is.False);
            bed = seats.Single(seat => SEntMan.GetComponent<CMUVehicleSeatComponent>(seat).LyingAngles.Count > 0);
            Assert.That(SEntMan.HasComponent<CMOperatingTableComponent>(bed), Is.True);
            Assert.That(SEntMan.GetComponent<StrapComponent>(bed).Position, Is.EqualTo(StrapPosition.Down));

            // Laid on over the lowered tailgate.
            SEntMan.System<CMUJeepSystem>()
                .SetPanel((jeep, SEntMan.GetComponent<CMUJeepComponent>(jeep)), CMUJeepSystem.Tailgate, true);
            // Behind the jeep, on the grid.
            var grid = SEntMan.GetComponent<TransformComponent>(jeep).ParentUid;
            patient = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(grid, 0.5f, 2f));
        });
        await RunTicksSync(5);

        await Server.WaitPost(() =>
        {
            Assert.That(SEntMan.System<SharedBuckleSystem>().TryBuckle(patient, user, bed), Is.True);
        });
        await RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<BuckleComponent>(patient).BuckledTo, Is.EqualTo(bed));
            Assert.That(SEntMan.System<StandingStateSystem>().IsDown(patient), Is.True);
            Assert.That(SEntMan.System<SharedCMSurgerySystem>().IsLyingDown(patient), Is.True,
                "Surgery should see the patient lying down.");
        });

        // Clicking the jeep never puts anyone on the bed.
        EntityUid other = default;
        await Server.WaitPost(() =>
        {
            SEntMan.System<SharedBuckleSystem>().TryUnbuckle(patient, null, popup: false);
            var grid = SEntMan.GetComponent<TransformComponent>(jeep).ParentUid;
            other = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(grid, 0.5f, 2f));
            SEntMan.EventBus.RaiseLocalEvent(jeep, new InteractHandEvent(other, jeep));
        });
        await RunSeconds(3f);
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<BuckleComponent>(other).BuckledTo, Is.Not.EqualTo(bed)));
    }

    [Test]
    public async Task MedicalKitCrateOpensIntoABareChassisWithItsKey()
    {
        var (_, user) = await SpawnJeep("CMUVehicleJeepCargo");
        EntityUid crate = default;
        await Server.WaitPost(() =>
        {
            crate = SEntMan.SpawnEntity("CMUCrateJeepKitMedical", new EntityCoordinates(SEntMan.GetComponent<TransformComponent>(user).ParentUid, 3.5f, 3.5f));
        });
        await RunTicksSync(5);
        await Server.WaitPost(() => SEntMan.System<SharedEntityStorageSystem>().OpenStorage(crate, user));
        await RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(crate), Is.True);
            var chassis = SEntMan.EntityQuery<CMUJeepComponent>(true)
                .Select(c => c.Owner)
                .Single(uid => SEntMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID == "CMUVehicleJeepMedicalChassis");
            var lamps = SEntMan.GetComponent<CMUJeepLampsComponent>(chassis);
            Assert.That(lamps.Lamps.All(lamp => !lamp.Slot.HasItem), Is.True, "The chassis comes without its lamps.");
            Assert.That(SEntMan.GetComponent<CMUVehicleIgnitionComponent>(chassis).KeySlot.HasItem, Is.False);
            var keys = SEntMan.EntityQuery<MetaDataComponent>(true).Count(m => m.EntityPrototype?.ID == "CMUJeepKey");
            // One in the cargo jeep's ignition, one out of the crate.
            Assert.That(keys, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task KitsAreOnEveryPlatoonCatalogInPriceOrder()
    {
        await Server.WaitAssertion(() =>
        {
            var prototypes = Server.ResolveDependency<IPrototypeManager>();
            var factory = SEntMan.ComponentFactory;
            string[] catalogs =
            [
                "CMBCIUCargoCatalog", "HAZOPSCargoCatalog", "LACNCargoCatalog", "ProdigyCargoCatalog",
                "RMCCargoCatalog", "UPPCargoCatalog", "USCMCargoCatalog", "VAIPOCargoCatalog", "WYPMCCargoCatalog",
            ];
            string[] kits = ["CMUCrateJeepKitCargo", "CMUCrateJeepKitMedical", "CMUCrateJeepKitTransport", "CMUCrateJeepKitGunner"];
            foreach (var catalogId in catalogs)
            {
                Assert.That(prototypes.Index<EntityPrototype>(catalogId).TryComp<RequisitionsComputerComponent>(out var catalog, factory), Is.True);
                var vehicles = catalog!.Categories.Single(category => category.Name == "Vehicles");
                var costs = kits.Select(kit => vehicles.Entries.Single(entry => entry.Crate.Id == kit).Cost).ToList();
                Assert.That(costs, Is.Ordered.Ascending, catalogId);
                Assert.That(costs[0], Is.GreaterThanOrEqualTo(10000), catalogId);
            }
        });
    }

    [Test]
    public async Task WreckOutlastsItsFire()
    {
        var (jeep, _) = await SpawnJeep("CMUVehicleJeepCargo");
        await Server.WaitPost(() =>
        {
            var damage = new DamageSpecifier();
            damage.DamageDict["Blunt"] = 5000;
            // Through the jeep's resistances: RMC routes a vehicle's damage into its hull from there.
            SEntMan.System<DamageableSystem>().TryChangeDamage(jeep, damage);
        });
        await RunSeconds(1f);
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<CMUJeepComponent>(jeep).Burning, Is.True, "The engine should burn before the blast."));

        await RunSeconds(7f);
        EntityUid wreck = default;
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(jeep), Is.True, "The jeep should have gone up.");
            wreck = SEntMan.EntityQuery<MetaDataComponent>(true)
                .Single(m => m.EntityPrototype?.ID == "CMUJeepWreckCargo").Owner;
        });

        await RunSeconds(20f);
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.Deleted(wreck), Is.False, "The wreck should outlast the fire under it."));
    }
}
