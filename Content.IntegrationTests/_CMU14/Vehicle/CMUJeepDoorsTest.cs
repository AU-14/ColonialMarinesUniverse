using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Buckle.Components;
using Content.Shared.CMU14.Vehicle.Jeep;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared._RMC14.Vehicle;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Vehicle;

/// <summary>
/// The jeep's doors: climbing in over a shut door takes a while and through an open one is instant;
/// a door comes off screwdriver, wrench, hand and goes back on hand, wrench, screwdriver. And the
/// headlight switch cycles off, low, high.
/// </summary>
[TestFixture]
public sealed class CMUJeepDoorsTest : GameTest
{
    private async Task<(EntityUid Jeep, EntityUid User)> SpawnJeep(string prototype)
    {
        var map = await Pair.CreateTestMap();
        EntityUid jeep = default;
        EntityUid user = default;
        await Server.WaitPost(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            for (var x = -4; x <= 4; x++)
            {
                for (var y = -4; y <= 4; y++)
                {
                    maps.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(x, y), map.Tile.Tile);
                }
            }

            // Facing south, the driver's side is +x; the user stands at the driver's door.
            jeep = SEntMan.SpawnEntity(prototype, new EntityCoordinates(map.Grid, 0.5f, 0.5f));
            user = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 1.45f, 0.75f));
        });
        await RunTicksSync(5);
        return (jeep, user);
    }

    private EntityUid Part(EntityUid jeep, string id)
    {
        return SEntMan.GetComponent<CMUJeepComponent>(jeep).PartEntities
            .First(part => SEntMan.GetComponent<CMUVehiclePartComponent>(part).Part == id);
    }

    private EntityUid DriverSeat(EntityUid jeep)
    {
        return SEntMan.GetComponent<CMUVehicleSeatsComponent>(jeep).SeatEntities
            .First(seat => SEntMan.GetComponent<CMUVehicleSeatComponent>(seat).Driver);
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

    private bool IsSeated(EntityUid user, EntityUid seat)
    {
        return SEntMan.GetComponent<BuckleComponent>(user).BuckledTo == seat;
    }

    [Test]
    public async Task ShutDoorDelaysClimbingInOpenDoorDoesNot()
    {
        var (jeep, user) = await SpawnJeep("CMUVehicleJeepCargo");
        EntityUid seat = default;

        await Server.WaitPost(() =>
        {
            seat = DriverSeat(jeep);
            SEntMan.System<CMUVehicleSeatSystem>().ClickSeat(seat, user);
        });
        await RunTicksSync(5);
        await Server.WaitAssertion(() =>
            Assert.That(IsSeated(user, seat), Is.False, "Climbing in over a shut door should take a while."));

        await RunSeconds(2.5f);
        await Server.WaitAssertion(() =>
            Assert.That(IsSeated(user, seat), Is.True, "The climb over the shut door should finish."));

        // Out through the opened door: at once.
        await Server.WaitPost(() =>
        {
            SEntMan.System<CMUJeepSystem>()
                .SetPanel((jeep, SEntMan.GetComponent<CMUJeepComponent>(jeep)), CMUJeepSystem.DriverDoor, true);
        });
        await RunSeconds(1f);
        await Server.WaitPost(() => SEntMan.System<CMUVehicleSeatSystem>().ClickSeat(seat, user));
        await RunTicksSync(5);
        await Server.WaitAssertion(() =>
            Assert.That(IsSeated(user, seat), Is.False, "Getting out through an open door should be instant."));
    }

    [Test]
    public async Task DoorComesOffAndGoesBackOnInSteps()
    {
        var (jeep, user) = await SpawnJeep("CMUVehicleJeepTransport");
        EntityUid door = default;
        const string slot = "jeep-door-passenger";

        // The passenger's door is on the far side; the steps don't care which way the door faces.
        await Server.WaitPost(() =>
        {
            door = Part(jeep, CMUJeepSystem.PassengerDoor);
            Hold(user, "CMScrewdriver");
            Click(user, door);
        });
        await RunSeconds(3f);
        await Server.WaitPost(() =>
        {
            var jeepComp = SEntMan.GetComponent<CMUJeepComponent>(jeep);
            Assert.That(CMUJeepSystem.GetFastening(jeepComp, slot), Is.EqualTo(CMUJeepSystem.Bolted));
            Hold(user, "CMWrench");
            Click(user, door);
        });
        await RunSeconds(3f);
        await Server.WaitPost(() =>
        {
            var jeepComp = SEntMan.GetComponent<CMUJeepComponent>(jeep);
            Assert.That(CMUJeepSystem.GetFastening(jeepComp, slot), Is.EqualTo(CMUJeepSystem.Loose));
            Hold(user, null);
            Click(user, door);
        });
        await RunTicksSync(5);

        EntityUid item = default;
        await Server.WaitPost(() =>
        {
            Assert.That(SEntMan.System<ItemSlotsSystem>().GetItemOrNull(jeep, slot), Is.Null,
                "An unbolted door should lift off by hand.");
            item = SEntMan.System<SharedHandsSystem>().EnumerateHeld(user).Single();
            Assert.That(SEntMan.System<CMUJeepSystem>().IsPanelOpen(jeep, CMUJeepSystem.PassengerDoor), Is.True,
                "With its door off, a seat's opening is open.");

            // Hung back on by hand: loose until bolted and screwed down.
            Click(user, door);
        });
        await RunTicksSync(5);
        await Server.WaitPost(() =>
        {
            var jeepComp = SEntMan.GetComponent<CMUJeepComponent>(jeep);
            Assert.That(SEntMan.System<ItemSlotsSystem>().GetItemOrNull(jeep, slot), Is.EqualTo(item));
            Assert.That(CMUJeepSystem.GetFastening(jeepComp, slot), Is.EqualTo(CMUJeepSystem.Loose));
            Hold(user, "CMWrench");
            Click(user, door);
        });
        await RunSeconds(3f);
        await Server.WaitPost(() =>
        {
            Hold(user, "CMScrewdriver");
            Click(user, door);
        });
        await RunSeconds(3f);
        await Server.WaitAssertion(() =>
        {
            var jeepComp = SEntMan.GetComponent<CMUJeepComponent>(jeep);
            Assert.That(CMUJeepSystem.GetFastening(jeepComp, slot), Is.EqualTo(CMUJeepSystem.Fastened));
        });
    }

    [Test]
    public async Task HeadlightSwitchCyclesTheBeams()
    {
        var (jeep, user) = await SpawnJeep("CMUVehicleJeepGunner");
        await Server.WaitAssertion(() =>
        {
            var jeeps = SEntMan.System<CMUJeepSystem>();
            var lights = SEntMan.GetComponent<CMUVehicleHeadlightsComponent>(jeep);
            // A low and a high beam for each headlight.
            Assert.That(lights.Beams, Has.Count.EqualTo(4));

            var lightSystem = SEntMan.System<SharedPointLightSystem>();
            int Lit(CMUHeadlightMode mode) => lights.Beams.Count(beam =>
                SEntMan.GetComponent<CMUVehicleHeadlightBeamComponent>(beam).Mode == mode &&
                lightSystem.TryGetLight(beam, out var light) && light.Enabled);

            Assert.That(Lit(CMUHeadlightMode.Low) + Lit(CMUHeadlightMode.High), Is.Zero);
            jeeps.CycleHeadlights((jeep, lights), user);
            Assert.That(lights.Mode, Is.EqualTo(CMUHeadlightMode.Low));
            Assert.That((Lit(CMUHeadlightMode.Low), Lit(CMUHeadlightMode.High)), Is.EqualTo((2, 0)));
            Assert.That(SEntMan.GetComponent<VehicleSpotlightComponent>(jeep).Enabled, Is.True);
            jeeps.CycleHeadlights((jeep, lights), user);
            Assert.That(lights.Mode, Is.EqualTo(CMUHeadlightMode.High));
            Assert.That((Lit(CMUHeadlightMode.Low), Lit(CMUHeadlightMode.High)), Is.EqualTo((0, 2)));
            jeeps.CycleHeadlights((jeep, lights), user);
            Assert.That(lights.Mode, Is.EqualTo(CMUHeadlightMode.Off));
            Assert.That(Lit(CMUHeadlightMode.Low) + Lit(CMUHeadlightMode.High), Is.Zero);
            Assert.That(SEntMan.GetComponent<VehicleSpotlightComponent>(jeep).Enabled, Is.False);
        });
    }
}
