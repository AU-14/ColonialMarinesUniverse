#pragma warning disable RA0002 // Explicitly arrange platoon and depot ownership.
using Content.Server.CMU14.Round;
using Content.Shared.CMU14;
using Content.Shared.CMU14.Fighter;
using Content.Shared.CMU14.util;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Content.Server._RMC14.Vehicle;
using Content.Shared._RMC14.Vehicle.Supply;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.GameTicking;
using Content.Shared.UserInterface;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests._RMC14;

[TestFixture]
public sealed class VehicleSupplyLoadoutTest
{

    [Test]
    public async Task SelectedLoadoutInstallsItemsIntoVehicleSlots()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid vehicle = default;

        await server.WaitPost(() =>
        {
            vehicle = server.EntMan.SpawnEntity("VehicleTank", map.GridCoords);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var supply = entMan.System<VehicleSupplySystem>();
            var itemSlots = entMan.System<ItemSlotsSystem>();

            ClearVehicleTechUnlocks(entMan);

            var entry = CreateLoadoutEntry(
                "VehicleTank",
                LoadoutCategory("primary", LoadoutOption("VehicleTankAceAutocannon", "primary::turret-cannon")),
                LoadoutCategory("armor", LoadoutOption("VehicleTankArmorBallistic", "armor")),
                LoadoutCategory("support", LoadoutOption("VehicleTankWarningArray", "support")));
            var selections = new Dictionary<string, string>
            {
                ["primary"] = "VehicleTankAceAutocannon",
                ["armor"] = "VehicleTankArmorBallistic",
                ["support"] = "VehicleTankWarningArray",
            };

            Assert.That(supply.DebugApplyLoadoutForTest(vehicle, entry, selections), Is.True);

            AssertSlotItem(entMan, itemSlots, vehicle, "armor", "VehicleTankArmorBallistic");
            AssertSlotItem(entMan, itemSlots, vehicle, "support", "VehicleTankWarningArray");

            Assert.That(itemSlots.TryGetSlot(vehicle, "primary", out var turretSlot), Is.True);
            Assert.That(turretSlot!.Item, Is.Not.Null);

            AssertSlotItem(entMan, itemSlots, turretSlot.Item!.Value, "turret-cannon", "VehicleTankAceAutocannon");
        });

        await pair.CleanReturnAsync();
    }

    private static void AssertSlotItem(
        IEntityManager entMan,
        ItemSlotsSystem itemSlots,
        EntityUid owner,
        string slotId,
        string expectedPrototype)
    {
        Assert.That(itemSlots.TryGetSlot(owner, slotId, out var slot), Is.True, slotId);
        Assert.That(slot!.Item, Is.Not.Null, slotId);

        var metadata = entMan.GetComponent<MetaDataComponent>(slot.Item!.Value);
        Assert.That(metadata.EntityPrototype?.ID, Is.EqualTo(expectedPrototype), slotId);
    }

    private static VehicleSupplyEntry CreateLoadoutEntry(
        string vehicleId,
        params VehicleSupplyLoadoutCategory[] categories)
    {
        return new VehicleSupplyEntry
        {
            Vehicle = vehicleId,
            LoadoutCategories = categories.ToList(),
        };
    }

    private static VehicleSupplyLoadoutCategory LoadoutCategory(
        string id,
        params VehicleSupplyLoadoutOption[] options)
    {
        return new VehicleSupplyLoadoutCategory
        {
            Id = id,
            Options = options.ToList(),
        };
    }

    private static VehicleSupplyLoadoutOption LoadoutOption(string id, string slot)
    {
        return new VehicleSupplyLoadoutOption
        {
            Id = id,
            Item = id,
            Slot = slot,
        };
    }

    private static void ClearVehicleTechUnlocks(IEntityManager entMan)
    {
        var techQuery = entMan.EntityQueryEnumerator<VehicleSupplyTechComponent>();
        while (techQuery.MoveNext(out var techUid, out var tech))
        {
            tech.Unlocked.Clear();
            entMan.Dirty(techUid, tech);
        }
    }
}
