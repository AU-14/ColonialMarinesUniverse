using System.Collections.Generic;
using System.Linq;
using Content.Server._RMC14.Vehicle;
using Content.Shared._RMC14.Vehicle.Supply;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.UserInterface;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._RMC14;

[TestFixture]
public sealed class VehicleSupplyLoadoutTest
{
    private const string ConsoleId = "VehicleSupplyConsole";

    [Test]
    public async Task VehicleSupplyConsoleHasValidLoadoutCategories()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        // Category IDs currently in use across vehicle ents in vehicle_supply.yml
        var validCategories = new HashSet<string>
        {
            "primary",
            "secondary",
            "wheels",
            "armor",
            "support",
            "standardtreads",
            "auxiliary"
        };

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;

            Assert.That(prototypes.TryIndex<EntityPrototype>(ConsoleId, out var consoleProto), Is.True);
            Assert.That(consoleProto!.TryGetComponent<VehicleSupplyConsoleComponent>(out var console, factory), Is.True);

            foreach (var entry in console!.Vehicles)
            {
                Assert.That(entry.LoadoutCategories, Is.Not.Empty, $"{entry.Vehicle.Id} has no loadout categories");

                foreach (var cat in entry.LoadoutCategories)
                {
                    Assert.That(cat.Id, Is.Not.Empty, $"{entry.Vehicle.Id} has a category with an empty ID");
                    Assert.That(validCategories, Does.Contain(cat.Id), $"{entry.Vehicle.Id} uses unknown category '{cat.Id}'");
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task UnsupportedArmorCategoriesOnlyExposeNone()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;

            Assert.That(prototypes.TryIndex<EntityPrototype>(ConsoleId, out var consoleProto), Is.True);
            Assert.That(consoleProto!.TryGetComponent<VehicleSupplyConsoleComponent>(out var console, factory), Is.True);

            foreach (var entry in console!.Vehicles)
            {
                var vehicleId = entry.Vehicle.Id;
                if (!vehicleId.StartsWith("VehicleHumvee") &&
                    !vehicleId.StartsWith("VehicleAPC") &&
                    !vehicleId.StartsWith("VehicleBlackfoot"))
                {
                    continue;
                }

                var armor = entry.LoadoutCategories.FirstOrDefault(c => c.Id == "armor");
                if (armor != null)
                    Assert.That(armor.Options, Is.Empty, vehicleId);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BlackfootEntriesRaisePackedSupportBundle()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;

            Assert.That(prototypes.TryIndex<EntityPrototype>(ConsoleId, out var consoleProto), Is.True);
            Assert.That(consoleProto!.TryGetComponent<VehicleSupplyConsoleComponent>(out var console, factory), Is.True);

            var blackfoots = console!.Vehicles
                .Where(v => v.Vehicle.Id.StartsWith("VehicleBlackfoot"))
                .ToList();

            Assert.That(blackfoots, Is.Not.Empty);
            foreach (var entry in blackfoots)
            {
                Assert.That(entry.Bundle.Select(id => id.Id), Is.EquivalentTo(new[]
                {
                    "CMUBlackfootLandingPadFoldedProp",
                    "CMUBlackfootFuelPumpCrate",
                    "CMUBlackfootFlightComputerCrate",
                    "CMUBlackfootAerospaceTug",
                }), entry.Vehicle.Id);
                Assert.That(entry.Bundle.Select(id => id.Id), Does.Not.Contain("CMUBlackfootLandingPad"));
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task VehicleSupplyCatalogIncludesAllVehicleVariantsAndHardpoints()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;

            Assert.That(prototypes.TryIndex<EntityPrototype>(ConsoleId, out var consoleProto), Is.True);
            Assert.That(consoleProto!.TryGetComponent<VehicleSupplyConsoleComponent>(out var console, factory), Is.True);

            var entries = console!.Vehicles.ToDictionary(v => v.Vehicle.Id);
            Assert.That(entries.Keys, Is.SupersetOf(new[]
            {
                "VehicleHumvee",
                // "VehicleHumveeMedical",
                // "VehicleHumveeTransport",
                // "VehicleHumveeARC",
                // "VehicleAPC",
                "VehicleAPCMed",
                // "VehicleAPCCommand",
                // "VehicleSPPAPC",
                // "VehicleTank",
                // "VehicleSPPTank",
                // "VehicleSPPTankCommand",
                // "VehicleBlackfoot",
                // "VehicleBlackfootDoorGunVariant",
                "VehicleBlackfootRecon",
                // "VehicleBlackfootTransport",
            }));

            AssertHardpoints(entries, "VehicleHumvee", HumveeArmedHardpoints);
            // AssertHardpoints(entries, "VehicleHumveeMedical", HumveeSupportHardpoints);
            // AssertHardpoints(entries, "VehicleHumveeTransport", HumveeSupportHardpoints);
            // AssertHardpoints(entries, "VehicleHumveeARC", HumveeArmedHardpoints);
            //
            // AssertHardpoints(entries, "VehicleAPC", ApcHardpoints);
            AssertHardpoints(entries, "VehicleAPCMed", ApcHardpoints);
            // AssertHardpoints(entries, "VehicleAPCCommand", ApcHardpoints);
            // AssertHardpoints(entries, "VehicleSPPAPC", SppApcHardpoints);
            //
            // AssertHardpoints(entries, "VehicleTank", TankHardpoints);
            // AssertHardpoints(entries, "VehicleSPPTank", SppTankHardpoints);
            // AssertHardpoints(entries, "VehicleSPPTankCommand", SppTankHardpoints);
            //
            // AssertHardpoints(entries, "VehicleBlackfoot", BlackfootBaseHardpoints);
            // AssertHardpoints(entries, "VehicleBlackfootDoorGunVariant", BlackfootDoorGunHardpoints);
            AssertHardpoints(entries, "VehicleBlackfootRecon", BlackfootReconHardpoints);
            // AssertHardpoints(entries, "VehicleBlackfootTransport", BlackfootBaseHardpoints);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MobilityHardpointsAreSelectableSupportLoadouts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;

            Assert.That(prototypes.TryIndex<EntityPrototype>(ConsoleId, out var consoleProto), Is.True);
            Assert.That(consoleProto!.TryGetComponent<VehicleSupplyConsoleComponent>(out var console, factory), Is.True);

            var entries = console!.Vehicles.ToDictionary(v => v.Vehicle.Id);

            AssertLoadoutOption(entries, "VehicleHumvee", "wheels", "VehicleHumveeWheel", "wheel-1");
            AssertLoadoutOption(entries, "VehicleAPCMed", "wheels", "RMCAPCWheel", "wheel-1");

            // foreach (var vehicle in new[]
            //          {
            //              "VehicleHumvee",
            //              "VehicleHumveeMedical",
            //              "VehicleHumveeTransport",
            //              "VehicleHumveeARC",
            //          })
            // {
            //     AssertLoadoutOption(entries, vehicle, "support", "VehicleHumveeWheel", "wheel-1");
            // }

            // foreach (var vehicle in new[] { "VehicleAPC", "VehicleAPCMed", "VehicleAPCCommand" })
            // {
            //     AssertLoadoutOption(entries, vehicle, "support", "RMCAPCWheel", "wheel-1");
            // }

            // AssertLoadoutOption(entries, "VehicleSPPAPC", "support", "VehicleSPPAPCWheel", "wheel-1");
            //
            // foreach (var vehicle in new[] { "VehicleTank", "VehicleSPPTank", "VehicleSPPTankCommand" })
            // {
            //     AssertLoadoutOption(entries, vehicle, "support", "VehicleTankTreads", "wheel-1");
            //     AssertLoadoutOption(entries, vehicle, "support", "VehicleTankReinforcedTreads", "wheel-1");
            // }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LiftSeedsConfiguredCatalogWithoutTechUnlocks()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid consoleUid = default;
        EntityUid lift = default;
        List<string> vehicleIds = new();

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var techQuery = entMan.EntityQueryEnumerator<VehicleSupplyTechComponent>();
            while (techQuery.MoveNext(out var techUid, out var tech))
            {
                tech.Unlocked.Clear();
                entMan.Dirty(techUid, tech);
            }

            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = entMan.ComponentFactory;

            Assert.That(prototypes.TryIndex<EntityPrototype>(ConsoleId, out var consoleProto), Is.True);
            Assert.That(consoleProto!.TryGetComponent<VehicleSupplyConsoleComponent>(out var console, factory), Is.True);

            vehicleIds = console!.Vehicles.Select(v => v.Vehicle.Id.ToLowerInvariant()).ToList();

            consoleUid = entMan.SpawnEntity(ConsoleId, map.GridCoords);
            lift = entMan.SpawnEntity("VehicleLift", map.GridCoords);
        });

        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            var ev = new BeforeActivatableUIOpenEvent(consoleUid);
            server.EntMan.EventBus.RaiseLocalEvent(consoleUid, ev);
        });

        await server.WaitAssertion(() =>
        {
            var ui = server.EntMan.System<SharedUserInterfaceSystem>();
            Assert.That(ui.TryGetUiState<VehicleSupplyBuiState>(consoleUid, VehicleSupplyUIKey.Key, out var state), Is.True);
            Assert.That(state!.Available.Select(v => v.Id.ToLowerInvariant()), Is.SupersetOf(vehicleIds));

            var liftComp = server.EntMan.GetComponent<VehicleSupplyLiftComponent>(lift);
            Assert.That(liftComp.Stored.Keys, Is.SupersetOf(vehicleIds));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ConsoleBackfillsLiftWhenLiftInitializedFirst()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid consoleUid = default;
        EntityUid lift = default;
        List<string> vehicleIds = new();

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var techQuery = entMan.EntityQueryEnumerator<VehicleSupplyTechComponent>();
            while (techQuery.MoveNext(out var techUid, out var tech))
            {
                tech.Unlocked.Clear();
                entMan.Dirty(techUid, tech);
            }

            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = entMan.ComponentFactory;

            Assert.That(prototypes.TryIndex<EntityPrototype>(ConsoleId, out var consoleProto), Is.True);
            Assert.That(consoleProto!.TryGetComponent<VehicleSupplyConsoleComponent>(out var console, factory), Is.True);

            vehicleIds = console!.Vehicles.Select(v => v.Vehicle.Id.ToLowerInvariant()).ToList();
            lift = entMan.SpawnEntity("VehicleLift", map.GridCoords);
        });

        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            consoleUid = server.EntMan.SpawnEntity(ConsoleId, map.GridCoords);
        });

        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            var ev = new BeforeActivatableUIOpenEvent(consoleUid);
            server.EntMan.EventBus.RaiseLocalEvent(consoleUid, ev);
        });

        await server.WaitAssertion(() =>
        {
            var ui = server.EntMan.System<SharedUserInterfaceSystem>();
            Assert.That(ui.TryGetUiState<VehicleSupplyBuiState>(consoleUid, VehicleSupplyUIKey.Key, out var state), Is.True);
            Assert.That(state!.Available.Select(v => v.Id.ToLowerInvariant()), Is.SupersetOf(vehicleIds));

            var liftComp = server.EntMan.GetComponent<VehicleSupplyLiftComponent>(lift);
            Assert.That(liftComp.Stored.Keys, Is.SupersetOf(vehicleIds));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BlackfootBundleSpawnsPackedSupportObjects()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid lift = default;

        await server.WaitPost(() =>
        {
            lift = server.EntMan.SpawnEntity("VehicleLift", map.GridCoords);

            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;
            var supply = server.EntMan.System<VehicleSupplySystem>();

            Assert.That(prototypes.TryIndex<EntityPrototype>(ConsoleId, out var consoleProto), Is.True);
            Assert.That(consoleProto!.TryGetComponent<VehicleSupplyConsoleComponent>(out var console, factory), Is.True);

            // NOTE: VehicleBlackfoot was commented out in vehicle_supply.yml - so it's throwing
            //var entry = console!.Vehicles.Single(v => v.Vehicle.Id == "VehicleBlackfoot");

            var entry = console!.Vehicles.Single(v => v.Vehicle.Id == "VehicleBlackfootRecon");
            Assert.That(supply.DebugSpawnBundleForTest(lift, entry), Is.True);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.That(CountPrototype(entMan, "CMUBlackfootLandingPadFoldedProp"), Is.EqualTo(1));
            Assert.That(CountPrototype(entMan, "CMUBlackfootFuelPumpCrate"), Is.EqualTo(1));
            Assert.That(CountPrototype(entMan, "CMUBlackfootFlightComputerCrate"), Is.EqualTo(1));
            Assert.That(CountPrototype(entMan, "CMUBlackfootAerospaceTug"), Is.EqualTo(1));
            Assert.That(CountPrototype(entMan, "CMUBlackfootLandingPad"), Is.Zero);
        });

        await pair.CleanReturnAsync();
    }

    [Ignore("Currently commented out in the catalog - ignore")]
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
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;
            var entMan = server.EntMan;
            var supply = entMan.System<VehicleSupplySystem>();
            var itemSlots = entMan.System<ItemSlotsSystem>();

            Assert.That(prototypes.TryIndex<EntityPrototype>(ConsoleId, out var consoleProto), Is.True);
            Assert.That(consoleProto!.TryGetComponent<VehicleSupplyConsoleComponent>(out var console, factory), Is.True);

            var entry = console!.Vehicles.Single(v => v.Vehicle.Id == "VehicleTank");
            var selections = new Dictionary<string, string>
            {
                ["primary"] = "VehicleTankLTBCannon",
                ["armor"] = "VehicleTankArmorBallistic",
                ["support"] = "VehicleTankWarningArray",
            };

            Assert.That(supply.DebugApplyLoadoutForTest(vehicle, entry, selections), Is.True);

            AssertSlotItem(entMan, itemSlots, vehicle, "armor", "VehicleTankArmorBallistic");
            AssertSlotItem(entMan, itemSlots, vehicle, "support", "VehicleTankWarningArray");

            Assert.That(itemSlots.TryGetSlot(vehicle, "primary", out var turretSlot), Is.True);
            Assert.That(turretSlot!.Item, Is.Not.Null);

            AssertSlotItem(entMan, itemSlots, turretSlot.Item!.Value, "turret-cannon", "VehicleTankLTBCannon");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BlackfootLoadoutOptionsInstallIntoDeclaredSlots()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        // EntityUid doorGunBlackfoot = default;
        EntityUid reconBlackfoot = default;

        await server.WaitPost(() =>
        {
            // doorGunBlackfoot = server.EntMan.SpawnEntity("VehicleBlackfootDoorGunVariant", map.GridCoords);
            reconBlackfoot = server.EntMan.SpawnEntity("VehicleBlackfootRecon", map.GridCoords);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;
            var entMan = server.EntMan;
            var supply = entMan.System<VehicleSupplySystem>();
            var itemSlots = entMan.System<ItemSlotsSystem>();

            Assert.That(prototypes.TryIndex<EntityPrototype>(ConsoleId, out var consoleProto), Is.True);
            Assert.That(consoleProto!.TryGetComponent<VehicleSupplyConsoleComponent>(out var console, factory), Is.True);

            // var doorGunEntry = console!.Vehicles.Single(v => v.Vehicle.Id == "VehicleBlackfootDoorGunVariant");
            // Assert.That(supply.DebugApplyLoadoutForTest(
            //     doorGunBlackfoot,
            //     doorGunEntry,
            //     new Dictionary<string, string> { ["primary"] = "VehicleBlackfootDoorGun" }),
            //     Is.True);
            // AssertSlotItem(entMan, itemSlots, doorGunBlackfoot, "door-gun", "VehicleBlackfootDoorGun");

            var reconEntry = console.Vehicles.Single(v => v.Vehicle.Id == "VehicleBlackfootRecon");
            Assert.That(supply.DebugApplyLoadoutForTest(
                reconBlackfoot,
                reconEntry,
                new Dictionary<string, string> { ["secondary"] = "VehicleBlackfootReconSystem" }),
                Is.True);
            AssertSlotItem(entMan, itemSlots, reconBlackfoot, "recon", "VehicleBlackfootReconSystem");

            Assert.That(supply.DebugApplyLoadoutForTest(
                reconBlackfoot,
                reconEntry,
                new Dictionary<string, string> { ["support"] = "VehicleBlackfootSensorArray" }),
                Is.True);
            AssertSlotItem(entMan, itemSlots, reconBlackfoot, "sensors", "VehicleBlackfootSensorArray");
        });

        await pair.CleanReturnAsync();
    }

    private static readonly string[] HumveeSupportHardpoints =
    {
        "VehicleHumveeWheel",
        "VehicleHumveeSnowplow",
        "VehicleHumveeOverlight",
        "VehicleHumveeHatch",
    };

    private static readonly string[] HumveeArmedHardpoints =
    {
        "VehicleHumveeWheel",
        "VehicleHumveeTurret",
        "VehicleHumveeTurretArmed",
        "VehicleHumveeARCTurret",
        "VehicleHumveeCannon",
        "VehicleHumveeARCCannon",
        "VehicleHumveeLauncher",
        "VehicleHumveeSnowplow",
        "VehicleHumveeOverlight",
        "VehicleHumveeHatch",
    };

    private static readonly string[] ApcHardpoints =
    {
        "RMCAPCWheel",
        "RMCAPCFrontCannon",
        "RMCAPCDualCannon",
        "RMCAPCCommsRelay",
        "RMCAPCFlareLauncher",
        "RMCAPCFreightStorage",
    };

    private static readonly string[] SppApcHardpoints =
    {
        "VehicleSPPAPCWheel",
        "VehicleSPPAPCTurret",
        "VehicleSPPAPCMinigun",
        "VehicleSPPAPCAutocannon",
        "VehicleSPPAPCHJ35Launcher",
        "VehicleSPPAPCFlareLauncher",
    };

    private static readonly string[] TankHardpoints =
    {
        "VehicleTankTurret",
        "VehicleTankTreads",
        "VehicleTankReinforcedTreads",
        "VehicleTankArmorBallistic",
        "VehicleTankArmorConcussive",
        "VehicleTankArmorCaustic",
        "VehicleTankArmorPaladin",
        "VehicleTankSnowplow",
        "VehicleTankLTBCannon",
        "VehicleTankLTAAAPMinigun",
        "VehicleTankAceAutocannon",
        "VehicleTankDragonFlamer",
        "VehicleTankFlamer",
        "VehicleTankGrenadeLauncher",
        "VehicleTankTowLauncher",
        "VehicleTankM56Cupola",
        "VehicleTankRERE700",
        "VehicleTankWarningArray",
        "VehicleTankOverdriveEnhancer",
        "VehicleTankRocketLauncher",
        "VehicleTankFlareModule",
        "VehicleTankArtilleryModule",
    };

    private static readonly string[] SppTankHardpoints =
    {
        "VehicleSPPTankTurret",
        "VehicleTankTreads",
        "VehicleTankReinforcedTreads",
        "VehicleSPPTankP17702",
        "VehicleSPPTankRailgun",
        "VehicleSPPTankHJ35TLauncher",
        "VehicleSPPTankCupola",
        "VehicleSPPTankReactiveArmor",
        "VehicleSPPTankRocketLauncher",
        "VehicleSPPTankFlareModule",
        "VehicleSPPTankWarningArray",
        "VehicleSPPTankOverdriveEnhancer",
        "VehicleSPPTankArtilleryModule",
    };

    private static readonly string[] BlackfootBaseHardpoints =
    {
        "VehicleBlackfootThrusters",
        "VehicleBlackfootLaunchers",
        "VehicleBlackfootReconSystem",
    };

    private static readonly string[] BlackfootDoorGunHardpoints =
    {
        "VehicleBlackfootThrusters",
        "VehicleBlackfootLaunchers",
        "VehicleBlackfootDoorGun",
        "VehicleBlackfootReconSystem",
    };

    private static readonly string[] BlackfootReconHardpoints =
    {
        "VehicleBlackfootThrusters",
        "VehicleBlackfootLaunchers",
        "VehicleBlackfootDoorGun",
        "VehicleBlackfootReconSystem",
        "VehicleBlackfootSensorArray",
    };

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

    private static void AssertHardpoints(
        IReadOnlyDictionary<string, VehicleSupplyEntry> entries,
        string vehicleId,
        IEnumerable<string> hardpoints)
    {
        Assert.That(entries.TryGetValue(vehicleId, out var entry), Is.True, vehicleId);
        Assert.That(entry!.Hardpoints.Select(h => h.Id), Is.EquivalentTo(hardpoints), vehicleId);
    }

    private static void AssertLoadoutOption(
        IReadOnlyDictionary<string, VehicleSupplyEntry> entries,
        string vehicleId,
        string categoryId,
        string optionId,
        string slotId)
    {
        Assert.That(entries.TryGetValue(vehicleId, out var entry), Is.True, vehicleId);
        var category = entry!.LoadoutCategories.Single(c => c.Id == categoryId);
        var option = category.Options.SingleOrDefault(o => o.Id == optionId);
        Assert.That(option, Is.Not.Null, $"{vehicleId} {categoryId} {optionId}");
        Assert.That(option!.Item.Id, Is.EqualTo(optionId), $"{vehicleId} {categoryId} {optionId}");
        Assert.That(option.Slot, Is.EqualTo(slotId), $"{vehicleId} {categoryId} {optionId}");
    }

    private static int CountPrototype(IEntityManager entMan, string prototypeId)
    {
        var count = 0;
        var query = entMan.EntityQueryEnumerator<MetaDataComponent>();
        while (query.MoveNext(out _, out var metadata))
        {
            if (metadata.EntityPrototype?.ID == prototypeId)
                count++;
        }

        return count;
    }
}
