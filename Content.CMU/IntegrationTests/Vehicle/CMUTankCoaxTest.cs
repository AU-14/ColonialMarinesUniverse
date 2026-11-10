using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Vehicle;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Vehicle;

[TestFixture]
public sealed class CMUTankCoaxTest : GameTest
{
    [Test]
    public async Task DriverCanInstallAndFireCoaxWithinItsArc()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var tank = SEntMan.SpawnEntity("VehicleTank", map.GridCoords);
            var coax = SEntMan.SpawnEntity("CMUVehicleTankCoaxMachineGun", map.GridCoords);
            var slots = Server.System<ItemSlotsSystem>();

            Assert.That(slots.TryGetSlot(tank, "support", out var slot), Is.True);
            Assert.That(slots.TryInsert(tank, slot, coax, null), Is.True);
            Assert.That(SEntMan.GetComponent<HardpointItemComponent>(coax).HardpointType, Is.EqualTo("Support"));
            Assert.That(SEntMan.GetComponent<GunFireArcComponent>(coax).Arc.Degrees, Is.EqualTo(145).Within(0.001));

            var driverSeat = SEntMan.SpawnEntity("RMCSeatTankDriver", map.GridCoords);
            Assert.That(
                SEntMan.GetComponent<VehicleWeaponsSeatComponent>(driverSeat).AllowedHardpointTypes,
                Does.Contain("Support"));
        });
    }
}
