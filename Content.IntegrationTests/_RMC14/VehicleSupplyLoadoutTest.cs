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
            Assert.That(consoleProto!.TryComp<VehicleSupplyConsoleComponent>(out var console, factory), Is.True);

            foreach (var entry in console!.Vehicles)
            {
                Assert.That(prototypes.TryIndex<EntityPrototype>(entry.Vehicle.Id, out var vehicle), Is.True);
                if (vehicle!.TryComp<FighterGroundComponent>(out _, factory))
                {
                    // Fighter pylons are serviced with a power loader, rather than ItemSlots loadouts.
                    Assert.That(entry.LoadoutCategories, Is.Empty);
                    Assert.That(entry.Bundle.Select(id => id.Id), Does.Contain("CMUFighterLandingPadFolded"));
                    Assert.That(entry.Hardpoints.Select(id => id.Id), Is.SupersetOf(new[]
                    {
                        "RMCDropshipAttachmentRocketPod", "RMCDropshipAttachmentAmmoGAU",
                        "RMCDropshipAttachmentAmmoRocketMiniMike", "RMCDropshipAttachmentAmmoRocketKeeper",
                        "RMCDropshipAttachmentAmmoRocketWidowmaker", "RMCDropshipAttachmentAmmoRocketHarpoon",
                        "RMCDropshipAttachmentAmmoRocketNapalm",
                    }));
                    continue;
                }

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
            Assert.That(consoleProto!.TryComp<VehicleSupplyConsoleComponent>(out var console, factory), Is.True);

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
    public async Task BlackfootCatalogOnlyEnablesArmedAndTransport()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;

            Assert.That(prototypes.TryIndex<EntityPrototype>(ConsoleId, out var consoleProto), Is.True);
            Assert.That(consoleProto!.TryComp<VehicleSupplyConsoleComponent>(out var console, factory), Is.True);

            Assert.That(console!.Vehicles.Where(entry => entry.Vehicle.Id.StartsWith("VehicleBlackfoot")).Select(e => e.Vehicle.Id),
                Is.EquivalentTo(new[] { "VehicleBlackfootTransport", "VehicleBlackfootDoorGunVariant" }));
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

            ClearVehicleTechUnlocks(server.EntMan);

            Assert.That(prototypes.TryIndex<EntityPrototype>(ConsoleId, out var consoleProto), Is.True);
            Assert.That(consoleProto!.TryComp<VehicleSupplyConsoleComponent>(out var console, factory), Is.True);

            var entries = console!.Vehicles.ToDictionary(v => v.Vehicle.Id);
            Assert.That(entries.Keys, Is.SupersetOf(new[]
            {
                "VehicleHumvee", "VehicleHumveeMedical", "VehicleHumveeTransport",
                "VehicleAPC", "VehicleAPCMed", "VehicleAPCCommand", "VehicleSPPAPC", "VehicleAev",
                "VehicleTank", "VehicleSPPTank",
                "VehicleSPPVan", "VehicleSPPVanArmed", "VehicleSPPVanMedical", "VehicleSPPVanPrisoner", "VehicleSPPVanLogistics",
            }));
            Assert.That(entries.Keys, Does.Contain("VehicleBlackfootDoorGunVariant"));

            AssertHardpoints(entries, "VehicleHumvee", HumveeArmedHardpoints);
            AssertHardpoints(entries, "VehicleHumveeMedical", HumveeSupportHardpoints);
            AssertHardpoints(entries, "VehicleHumveeTransport", HumveeSupportHardpoints);

            AssertHardpoints(entries, "VehicleAPC", ApcHardpoints);
            AssertHardpoints(entries, "VehicleAPCMed", ApcHardpoints);
            AssertHardpoints(entries, "VehicleAPCCommand", ApcHardpoints);
            AssertHardpoints(entries, "VehicleSPPAPC", SppApcHardpoints);

            AssertHardpoints(entries, "VehicleTank", TankHardpoints);
            AssertHardpoints(entries, "VehicleSPPTank", SppTankHardpoints);


            AssertEntryGroup(entries, "VehicleHumvee", "vehicle-support");
            AssertEntryGroup(entries, "VehicleHumveeMedical", "vehicle-support");
            AssertEntryGroup(entries, "VehicleHumveeTransport", "vehicle-support");
            AssertEntryGroup(entries, "VehicleSPPAPC", "vehicle-apc");
            AssertEntryGroup(entries, "VehicleAPC", "vehicle-apc");
            AssertEntryGroup(entries, "VehicleAPCMed", "vehicle-apc");
            AssertEntryGroup(entries, "VehicleAPCCommand", "vehicle-apc");
            AssertEntryGroup(entries, "VehicleTank", "vehicle-tank");
            AssertEntryGroup(entries, "VehicleSPPTank", "vehicle-tank");

            Assert.That(TankHardpoints, Does.Not.Contain("VehicleTankLTBCannon"));
            Assert.That(SppTankHardpoints, Does.Not.Contain("VehicleSPPTankRailgun"));
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

            ClearVehicleTechUnlocks(server.EntMan);

            Assert.That(prototypes.TryIndex<EntityPrototype>(ConsoleId, out var consoleProto), Is.True);
            Assert.That(consoleProto!.TryComp<VehicleSupplyConsoleComponent>(out var console, factory), Is.True);

            var entries = console!.Vehicles.ToDictionary(v => v.Vehicle.Id);

            AssertLoadoutOption(entries, "VehicleHumvee", "wheels", "VehicleHumveeWheel", "wheel-1");
            AssertLoadoutOption(entries, "VehicleHumveeMedical", "wheels", "VehicleHumveeWheel", "wheel-1");
            AssertLoadoutOption(entries, "VehicleHumveeTransport", "wheels", "VehicleHumveeWheel", "wheel-1");
            AssertLoadoutOption(entries, "VehicleAPC", "wheels", "RMCAPCWheel", "wheel-1");
            AssertLoadoutOption(entries, "VehicleAPCMed", "wheels", "RMCAPCWheel", "wheel-1");
            AssertLoadoutOption(entries, "VehicleAPCCommand", "wheels", "RMCAPCWheel", "wheel-1");
            AssertLoadoutOption(entries, "VehicleSPPAPC", "wheels", "VehicleSPPAPCWheel", "wheel-1");

            AssertLoadoutOption(entries, "VehicleTank", "standardtreads", "VehicleTankTreads", "wheel-1");
            AssertLoadoutOption(entries, "VehicleTank", "auxiliary", "VehicleTankReinforcedTreads", "wheel-1");
            AssertLoadoutOption(entries, "VehicleSPPTank", "standardtreads", "VehicleTankTreads", "wheel-1");
            AssertLoadoutOption(entries, "VehicleSPPTank", "auxiliary", "VehicleTankReinforcedTreads", "wheel-1");
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
            ClearVehicleTechUnlocks(entMan);

            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = entMan.ComponentFactory;

            Assert.That(prototypes.TryIndex<EntityPrototype>(ConsoleId, out var consoleProto), Is.True);
            Assert.That(consoleProto!.TryComp<VehicleSupplyConsoleComponent>(out var console, factory), Is.True);

            vehicleIds = console!.Vehicles
                .Where(v => prototypes.Index<PlatoonPrototype>("USCM").VehicleSupplyCatalog.Contains(v.Vehicle))
                .Select(v => v.Vehicle.Id.ToLowerInvariant()).ToList();

            ConfigureUSCM(entMan, map.GridCoords.EntityId);
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
            ClearVehicleTechUnlocks(entMan);

            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = entMan.ComponentFactory;

            Assert.That(prototypes.TryIndex<EntityPrototype>(ConsoleId, out var consoleProto), Is.True);
            Assert.That(consoleProto!.TryComp<VehicleSupplyConsoleComponent>(out var console, factory), Is.True);

            vehicleIds = console!.Vehicles
                .Where(v => prototypes.Index<PlatoonPrototype>("USCM").VehicleSupplyCatalog.Contains(v.Vehicle))
                .Select(v => v.Vehicle.Id.ToLowerInvariant()).ToList();
            lift = entMan.SpawnEntity("VehicleLift", map.GridCoords);
        });

        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            ConfigureUSCM(server.EntMan, map.GridCoords.EntityId);
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
            Assert.That(consoleProto!.TryComp<VehicleSupplyConsoleComponent>(out var console, factory), Is.True);

            var entry = console!.Vehicles.Single(v => v.Vehicle.Id == "VehicleBlackfootTransport");
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

            ClearVehicleTechUnlocks(entMan);

            Assert.That(prototypes.TryIndex<EntityPrototype>(ConsoleId, out var consoleProto), Is.True);
            Assert.That(consoleProto!.TryComp<VehicleSupplyConsoleComponent>(out var console, factory), Is.True);

            var entry = console!.Vehicles.Single(v => v.Vehicle.Id == "VehicleTank");
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

    [Test]
    public async Task SppTankLauncherLoadoutInstallsIntoLauncherSlot()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid vehicle = default;

        await server.WaitPost(() =>
        {
            vehicle = server.EntMan.SpawnEntity("VehicleSPPTank", map.GridCoords);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;
            var entMan = server.EntMan;
            var supply = entMan.System<VehicleSupplySystem>();
            var itemSlots = entMan.System<ItemSlotsSystem>();

            ClearVehicleTechUnlocks(entMan);

            Assert.That(prototypes.TryIndex<EntityPrototype>(ConsoleId, out var consoleProto), Is.True);
            Assert.That(consoleProto!.TryComp<VehicleSupplyConsoleComponent>(out var console, factory), Is.True);

            var entry = console!.Vehicles.Single(v => v.Vehicle.Id == "VehicleSPPTank");
            var entries = new Dictionary<string, VehicleSupplyEntry>
            {
                ["VehicleSPPTank"] = entry,
            };

            AssertLoadoutOption(
                entries,
                "VehicleSPPTank",
                "secondary",
                "VehicleSPPTankHJ35TLauncher",
                "primary::turret-launcher");

            Assert.That(supply.DebugApplyLoadoutForTest(
                vehicle,
                entry,
                new Dictionary<string, string> { ["secondary"] = "VehicleSPPTankHJ35TLauncher" }),
                Is.True);

            Assert.That(itemSlots.TryGetSlot(vehicle, "primary", out var turretSlot), Is.True);
            Assert.That(turretSlot!.Item, Is.Not.Null);

            AssertSlotItem(entMan, itemSlots, turretSlot.Item!.Value, "turret-cannon", "VehicleSPPTankP17702");
            AssertSlotItem(entMan, itemSlots, turretSlot.Item!.Value, "turret-launcher", "VehicleSPPTankHJ35TLauncher");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BlackfootLoadoutOptionsInstallIntoDeclaredSlots()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid armedBlackfoot = default;

        await server.WaitPost(() =>
        {
            armedBlackfoot = server.EntMan.SpawnEntity("VehicleBlackfootDoorGunVariant", map.GridCoords);
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
            Assert.That(consoleProto!.TryComp<VehicleSupplyConsoleComponent>(out var console, factory), Is.True);

            var armedEntry = console!.Vehicles.Single(v => v.Vehicle.Id == "VehicleBlackfootDoorGunVariant");
            Assert.That(supply.DebugApplyLoadoutForTest(
                armedBlackfoot,
                armedEntry,
                new Dictionary<string, string> { ["primary"] = "VehicleBlackfootDoorGun" }),
                Is.True);
            AssertSlotItem(entMan, itemSlots, armedBlackfoot, "door-gun", "VehicleBlackfootDoorGun");

            Assert.That(supply.DebugApplyLoadoutForTest(
                armedBlackfoot,
                armedEntry,
                new Dictionary<string, string> { ["secondary"] = "VehicleBlackfootLaunchers" }),
                Is.True);
            AssertSlotItem(entMan, itemSlots, armedBlackfoot, "launchers", "VehicleBlackfootLaunchers");
        });

        await pair.CleanReturnAsync();
    }

    private static readonly string[] HumveeSupportHardpoints =
    {
        "VehicleHumveeWheel",
        "VehicleHumveeSnowplow",
        "VehicleHumveeOverlight",
    };

    private static readonly string[] HumveeArmedHardpoints =
    {
        "VehicleHumveeWheel",
        "VehicleHumveeTurret",
        "VehicleHumveeTurretArmed",
        "VehicleHumveeCannon",
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

    private static readonly string[] BlackfootReconHardpoints =
    {
        "VehicleBlackfootThrusters",
        "VehicleBlackfootLaunchers",
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

    private static void AssertEntryGroup(
        IReadOnlyDictionary<string, VehicleSupplyEntry> entries,
        string vehicleId,
        string group)
    {
        Assert.That(entries.TryGetValue(vehicleId, out var entry), Is.True, vehicleId);
        Assert.That(entry!.Group, Is.EqualTo(group), vehicleId);
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

    private static void ConfigureUSCM(IEntityManager entMan, EntityUid grid)
    {
        // Pooled tests delete maps without restarting the round's systems.
        typeof(VehicleSupplySystem).GetMethod("OnSupplyRoundRestart", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(entMan.System<VehicleSupplySystem>(), new object[] { new RoundRestartCleanupEvent() });
        var maps = entMan.System<SharedMapSystem>();
        var comp = entMan.GetComponent<MapGridComponent>(grid);
        var tile = maps.GetAllTiles(grid, comp).First().Tile;
        for (var x = -6; x < 8; x++)
        for (var y = -6; y < 8; y++)
            maps.SetTile(grid, comp, new EntityCoordinates(grid, new Vector2(x, y)), tile);
        entMan.EnsureComponent<ShipFactionComponent>(grid).Faction = "govfor";
        entMan.System<PlatoonSpawnRuleSystem>().SelectedGovforPlatoon =
            Robust.Shared.IoC.IoCManager.Resolve<IPrototypeManager>().Index<PlatoonPrototype>("USCM");
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
