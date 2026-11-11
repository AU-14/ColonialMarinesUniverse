using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Round;
using Content.Shared.CMU14.Kitchen;
using Content.Shared.CMU14.util;
using Content.Shared.CMU14.Roles;
using Content.Shared.Doors.Components;
using Content.Shared.Access.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Interaction;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Kitchen;

[TestFixture]
public sealed class CMUFoodlineTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    /// <summary>Raising the kitchen shutters shuts off the NutriCo machines on the map, and lowering them turns them back on.</summary>
    [Test]
    public async Task KitchenShuttersShutOffNutriCo()
    {
        var map = await Pair.CreateTestMap();
        EntityUid shutters = default;
        EntityUid food = default;
        EntityUid drink = default;
        await Server.WaitAssertion(() =>
        {
            shutters = SEntMan.SpawnEntity("CMUShutterKitchen", map.GridCoords);
            food = SEntMan.SpawnEntity("RMCNutriCoVendor", map.GridCoords);
            drink = SEntMan.SpawnEntity("RMCVendorNutriCoDrink", map.GridCoords);
            Assert.That(SEntMan.GetComponent<CMUFoodlineVendorComponent>(food).FoodlineOpen, Is.False);

            SEntMan.System<SharedDoorSystem>().StartOpening(shutters);
        });

        await RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<DoorComponent>(shutters).State, Is.EqualTo(DoorState.Open));
            Assert.That(SEntMan.GetComponent<CMUFoodlineVendorComponent>(food).FoodlineOpen, Is.True);
            Assert.That(SEntMan.GetComponent<CMUFoodlineVendorComponent>(drink).FoodlineOpen, Is.True);
            Assert.That(SEntMan.System<CMUFoodlineSystem>().IsClosedForFoodline(food, null), Is.True);

            SEntMan.System<SharedDoorSystem>().StartClosing(shutters);
        });

        await RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<CMUFoodlineVendorComponent>(food).FoodlineOpen, Is.False);
        });
    }

    /// <summary>The open for business button opens the foodline, and pressing it again closes it.</summary>
    [Test]
    public async Task ButtonTogglesTheFoodline()
    {
        var map = await Pair.CreateTestMap();
        EntityUid button = default;
        EntityUid user = default;
        EntityUid shutters = default;
        EntityUid food = default;
        await Server.WaitAssertion(() =>
        {
            button = SEntMan.SpawnEntity("CMUButtonKitchenOpenForBusiness", map.GridCoords);
            shutters = SEntMan.SpawnEntity("CMUShutterKitchen", map.GridCoords);
            food = SEntMan.SpawnEntity("RMCNutriCoVendor", map.GridCoords);
            user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            SEntMan.System<SharedPowerReceiverSystem>().SetNeedsPower(button, false);
            SEntMan.RemoveComponent<AccessReaderComponent>(button);
            SEntMan.EventBus.RaiseLocalEvent(button, new ActivateInWorldEvent(user, button, true));
        });

        await RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<DoorComponent>(shutters).State, Is.EqualTo(DoorState.Open));
            Assert.That(SEntMan.GetComponent<CMUFoodlineVendorComponent>(food).FoodlineOpen, Is.True);
            SEntMan.EventBus.RaiseLocalEvent(button, new ActivateInWorldEvent(user, button, true));
        });

        await RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<DoorComponent>(shutters).State, Is.EqualTo(DoorState.Closed), "Pressing it again closes the shutters.");
            Assert.That(SEntMan.GetComponent<CMUFoodlineVendorComponent>(food).FoodlineOpen, Is.False, "and turns the NutriCo machines back on.");
        });
    }

    /// <summary>
    ///     Every machine the ship's mess markers (CMUVMarkerShipMessFood and CMUVMarkerShipMessDrink) can place, for any
    ///     platoon, shuts off while the foodline is open.
    /// </summary>

    /// <summary>Only characters with a religion can pick chaplain.</summary>

}
