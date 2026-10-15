#pragma warning disable RA0002 // Regression assertions inspect vehicle, hardpoint and console state.

using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Vehicle;
using Content.Shared._RMC14.Vehicle.Supply;
using Content.Shared.CMU14.util;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage.Components;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Components;
using Robust.Client.GameObjects;
using Robust.Client.ResourceManagement;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations;

namespace Content.IntegrationTests.CMU14.Vehicle;

[TestFixture]
[TestOf(typeof(HardpointSystem))]
public sealed class CMUWolfpackTest : GameTest
{
    private const string WolfpackId = "CMUVehicleAPCWolfpack";
    private const string TurretId = "CMUVehicleAPCWolfpackTurret";
    private const string AutocannonId = "CMUVehicleAPCWolfpackAutocannon";
    private const string FrontCannonId = "CMUVehicleAPCWolfpackFrontCannon";
    private const string FlareLauncherId = "CMUVehicleAPCWolfpackFlareLauncher";
    private const string WheelId = "RMCAPCWheel";
    private const string ConsoleId = "VehicleSupplyConsole";
    private const string WolfpackRsi = "/Textures/CMU14/Structures/vehicles/apc_wolfpack.rsi";

    private static readonly string[] ExpectedHardpoints =
    {
        TurretId,
        AutocannonId,
        FrontCannonId,
        FlareLauncherId,
        WheelId,
    };

    private static readonly ProtoId<PlatoonPrototype>[] ApcPlatoons = ["USCM", "LACN", "CMBCIU", "HAZOPS", "VAIPO"];
    private static readonly ProtoId<PlatoonPrototype> WeyuPlatoon = "WEYU";

    private static readonly string[] HullStates =
    {
        "apc_base_wolfpack",
        "damaged_frame",
        "PRIMARY",
        "SECONDARY",
        "SUPPORT",
        "wheels_0",
        "wheels_1",
    };

    public override PoolSettings PoolSettings => new() { Connected = false };

    [Test]
    public async Task SpawnedWolfpackHasTurretAutocannonAndWheelAndCanRun()
    {
        var map = await Pair.CreateTestMap();
        EntityUid vehicle = default;
        await Server.WaitPost(() => vehicle = SEntMan.SpawnEntity(WolfpackId, map.GridCoords));
        await Pair.RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            var itemSlots = Server.System<ItemSlotsSystem>();
            var turret = AssertSlotItem(itemSlots, vehicle, "primary", TurretId);
            AssertSlotItem(itemSlots, turret, "turret-cannon", AutocannonId);
            AssertSlotItem(itemSlots, vehicle, "wheel-1", WheelId);
            Assert.That(itemSlots.TryGetSlot(vehicle, "port-gun-north", out _), Is.False,
                "The MGS carries no side port guns.");
            Assert.That(itemSlots.TryGetSlot(vehicle, "port-gun-south", out _), Is.False,
                "The MGS carries no side port guns.");
            var stockApc = SEntMan.SpawnEntity("VehicleAPC", map.GridCoords);
            Assert.That(itemSlots.TryGetSlot(stockApc, "port-gun-north", out _), Is.True,
                "Stock APCs keep their port guns.");
            SEntMan.DeleteEntity(stockApc);

            var vehicleComp = SEntMan.GetComponent<VehicleComponent>(vehicle);
            var canRun = new VehicleCanRunEvent((vehicle, vehicleComp));
            SEntMan.EventBus.RaiseLocalEvent(vehicle, ref canRun);
            Assert.That(canRun.CanRun, Is.True, "The starting wheel must satisfy the required wheel slot.");

            SEntMan.DeleteEntity(vehicle);
        });
    }

    [Test]
    public async Task WolfpackKeepsApcFrameIntegrityAndArmor()
    {
        var map = await Pair.CreateTestMap();
        EntityUid vehicle = default;
        EntityUid apc = default;
        await Server.WaitPost(() =>
        {
            vehicle = SEntMan.SpawnEntity(WolfpackId, map.GridCoords);
            apc = SEntMan.SpawnEntity("VehicleAPC", map.GridCoords);
        });
        await Pair.RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            try
            {
                // Frame capacity is the sum of mounted parts, so APC parity means the turret and its cannon
                // together must match the APC's dual cannon, and the native (empty) frame must match the APC's.
                var integrity = SEntMan.GetComponent<HardpointIntegrityComponent>(vehicle);
                var damageable = SEntMan.GetComponent<DamageableComponent>(vehicle);
                var turret = PartIntegrity(TurretId);
                var cannon = PartIntegrity(AutocannonId);
                var wheel = PartIntegrity(WheelId);
                var dualCannon = PartIntegrity("RMCAPCDualCannon");
                Assert.Multiple(() =>
                {
                    Assert.That(integrity.NativeMaxIntegrity,
                        Is.EqualTo(SEntMan.GetComponent<HardpointIntegrityComponent>(apc).NativeMaxIntegrity));
                    Assert.That(turret.MaxIntegrity + cannon.MaxIntegrity, Is.EqualTo(dualCannon.MaxIntegrity),
                        "The turret and autocannon replace the APC dual cannon and must not add hull capacity.");
                    Assert.That(integrity.MaxIntegrity,
                        Is.EqualTo(turret.MaxIntegrity + cannon.MaxIntegrity + wheel.MaxIntegrity),
                        "Spawned frame capacity follows the starting parts.");
                    Assert.That(PartItem(AutocannonId).DamageMultiplier,
                        Is.EqualTo(PartItem("RMCAPCDualCannon").DamageMultiplier),
                        "The autocannon must take APC-grade part damage, not the tank's.");
                    Assert.That(PartItem(TurretId).DamageMultiplier,
                        Is.EqualTo(PartItem("RMCAPCDualCannon").DamageMultiplier));
                    Assert.That(damageable.DamageModifierSetId?.Id, Is.EqualTo("VehicleFrameAPC"));
                    Assert.That(SEntMan.GetComponent<VehicleEnterComponent>(vehicle).InteriorPath,
                        Is.EqualTo(SEntMan.GetComponent<VehicleEnterComponent>(apc).InteriorPath),
                        "The Wolfpack reuses the APC transport interior.");
                });
            }
            finally
            {
                SEntMan.DeleteEntity(vehicle);
                SEntMan.DeleteEntity(apc);
            }
        });
    }

    [Test]
    public async Task WolfpackInteriorEntriesResolve()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var vehicles = Server.System<VehicleSystem>();
            var vehicle = SEntMan.SpawnEntity(WolfpackId, map.GridCoords);
            try
            {
                Assert.That(vehicles.TryGetInteriorEntryCoordinates(vehicle, 0, out _), Is.True);
                Assert.That(vehicles.TryGetInteriorEntryCoordinates(vehicle, 1, out _), Is.True);
                Assert.That(vehicles.TryGetInteriorMapId(vehicle, out _), Is.True);
            }
            finally
            {
                SEntMan.DeleteEntity(vehicle);
            }
        });
    }

    [Test]
    public async Task WolfpackSlotsOnlyAcceptPartsWithWolfpackArt()
    {
        var map = await Pair.CreateTestMap();
        EntityUid vehicle = default;
        await Server.WaitPost(() => vehicle = SEntMan.SpawnEntity(WolfpackId, map.GridCoords));
        await Pair.RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            var hardpoints = Server.System<HardpointSystem>();
            var itemSlots = Server.System<ItemSlotsSystem>();
            var vehicleSlots = SEntMan.GetComponent<HardpointSlotsComponent>(vehicle);
            var turret = AssertSlotItem(itemSlots, vehicle, "primary", TurretId);
            var turretSlots = SEntMan.GetComponent<HardpointSlotsComponent>(turret);
            var spawned = new List<EntityUid>();

            bool Fits(string itemId, HardpointSlotsComponent slots, string slotId)
            {
                Assert.That(hardpoints.TryGetSlot(slots, slotId, out var slot), Is.True, slotId);
                var item = SEntMan.SpawnEntity(itemId, map.GridCoords);
                spawned.Add(item);
                return hardpoints.IsValidHardpoint(item, slots, slot!);
            }

            try
            {
                Assert.Multiple(() =>
                {
                    Assert.That(Fits(TurretId, vehicleSlots, "primary"), Is.True);
                    Assert.That(Fits(AutocannonId, turretSlots, "turret-cannon"), Is.True);
                    Assert.That(Fits(FrontCannonId, vehicleSlots, "secondary"), Is.True);
                    Assert.That(Fits(FlareLauncherId, vehicleSlots, "support"), Is.True);
                    Assert.That(Fits(WheelId, vehicleSlots, "wheel-1"), Is.True);

                    // The Wolfpack RSI has no dualcannon_0, comms_relay_0 or freight_0 state.
                    Assert.That(Fits("RMCAPCDualCannon", vehicleSlots, "primary"), Is.False);
                    Assert.That(Fits("RMCAPCCommsRelay", vehicleSlots, "support"), Is.False);
                    Assert.That(Fits("RMCAPCFreightStorage", vehicleSlots, "support"), Is.False);
                    Assert.That(Fits("VehicleSPPAPCAutocannon", vehicleSlots, "secondary"), Is.False);

                    // Tank-family and foreign turret guns stay out of the Wolfpack turret.
                    Assert.That(Fits("VehicleTankAceAutocannon", turretSlots, "turret-cannon"), Is.False);
                    Assert.That(Fits("VehicleTankAceAutocannon", vehicleSlots, "primary"), Is.False);
                    Assert.That(Fits("VehicleSPPAPCMinigun", turretSlots, "turret-cannon"), Is.False);
                });
            }
            finally
            {
                foreach (var item in spawned)
                {
                    SEntMan.DeleteEntity(item);
                }

                SEntMan.DeleteEntity(vehicle);
            }
        });
    }

    [Test]
    public async Task SupplyConsoleOffersWolfpackToApcPlatoons()
    {
        await Server.WaitAssertion(() =>
        {
            var factory = SEntMan.ComponentFactory;
            Assert.That(SProtoMan.Index<EntityPrototype>(ConsoleId)
                .TryComp<VehicleSupplyConsoleComponent>(out var console, factory), Is.True);

            var entry = console!.Vehicles.Single(v => v.Vehicle.Id == WolfpackId);
            Assert.Multiple(() =>
            {
                Assert.That(entry.Group, Is.EqualTo("vehicle-apc"));
                Assert.That(entry.Hardpoints.Select(h => h.Id), Is.EquivalentTo(ExpectedHardpoints));
                Assert.That(entry.LoadoutCategories.Select(c => c.Id),
                    Is.EquivalentTo(new[] { "primary", "secondary", "wheels", "support" }));
                AssertOption(entry, "primary", AutocannonId, "primary::turret-cannon");
                AssertOption(entry, "secondary", FrontCannonId, "secondary");
                AssertOption(entry, "wheels", WheelId, "wheel-1");
                AssertOption(entry, "support", FlareLauncherId, "support");
            });

            Assert.Multiple(() =>
            {
                foreach (var platoon in ApcPlatoons)
                {
                    Assert.That(SProtoMan.Index(platoon).VehicleSupplyCatalog.Select(id => id.Id),
                        Does.Contain(WolfpackId), platoon.Id);
                }

                Assert.That(SProtoMan.Index(WeyuPlatoon).VehicleSupplyCatalog.Select(id => id.Id),
                    Does.Not.Contain(WolfpackId));
            });
        });
    }

    [Test]
    public async Task WolfpackHardpointArtExistsInVehicleRsi()
    {
        var hardpoints = new List<string>();
        await Server.WaitPost(() =>
        {
            Assert.That(SProtoMan.Index<EntityPrototype>(ConsoleId)
                .TryComp<VehicleSupplyConsoleComponent>(out var console, SEntMan.ComponentFactory), Is.True);
            hardpoints.AddRange(console!.Vehicles.Single(v => v.Vehicle.Id == WolfpackId).Hardpoints.Select(h => h.Id));
        });

        await Client.WaitAssertion(() =>
        {
            var factory = CEntMan.ComponentFactory;
            var cache = Client.ResolveDependency<IResourceCache>();
            Assert.That(cache.TryGetResource<RSIResource>(new ResPath(WolfpackRsi), out var vehicleRsi), Is.True);

            Assert.That(CProtoMan.Index<EntityPrototype>(WolfpackId).TryComp<SpriteComponent>(out var sprite, factory), Is.True);
            Assert.That(sprite!.BaseRSI?.Path, Is.EqualTo(vehicleRsi!.RSI.Path),
                "Hardpoint layer states are resolved on the vehicle's own RSI.");

            Assert.Multiple(() =>
            {
                foreach (var state in HullStates)
                {
                    Assert.That(vehicleRsi.RSI.TryGetState(state, out _), Is.True, state);
                }

                foreach (var id in hardpoints)
                {
                    var proto = CProtoMan.Index<EntityPrototype>(id);
                    if (proto.TryComp<HardpointVisualComponent>(out var visual, factory))
                    {
                        AssertState(vehicleRsi.RSI, visual.VehicleState, $"{id} vehicleState");
                        AssertState(vehicleRsi.RSI, visual.DamagedVehicleState, $"{id} damagedVehicleState");
                    }

                    if (!proto.TryComp<VehicleTurretComponent>(out var turret, factory) || !turret.ShowOverlay)
                        continue;

                    var overlayRsi = vehicleRsi.RSI;
                    if (!string.IsNullOrWhiteSpace(turret.OverlayRsi))
                    {
                        Assert.That(cache.TryGetResource<RSIResource>(
                            SpriteSpecifierSerializer.TextureRoot / turret.OverlayRsi, out var resource), Is.True, id);
                        overlayRsi = resource!.RSI;
                    }

                    AssertState(overlayRsi, turret.OverlayState, $"{id} overlayState");
                    AssertState(overlayRsi, turret.OverlayDamagedState, $"{id} overlayDamagedState");
                }
            });
        });
    }

    private EntityUid AssertSlotItem(ItemSlotsSystem itemSlots, EntityUid owner, string slotId, string expectedPrototype)
    {
        Assert.That(itemSlots.TryGetSlot(owner, slotId, out var slot), Is.True, slotId);
        Assert.That(slot!.Item, Is.Not.Null, slotId);
        Assert.That(SEntMan.GetComponent<MetaDataComponent>(slot.Item!.Value).EntityPrototype?.ID,
            Is.EqualTo(expectedPrototype), slotId);
        return slot.Item.Value;
    }

    private HardpointIntegrityComponent PartIntegrity(string prototype)
    {
        Assert.That(SProtoMan.Index<EntityPrototype>(prototype)
            .TryComp<HardpointIntegrityComponent>(out var integrity, SEntMan.ComponentFactory), Is.True, prototype);
        return integrity!;
    }

    private HardpointItemComponent PartItem(string prototype)
    {
        Assert.That(SProtoMan.Index<EntityPrototype>(prototype)
            .TryComp<HardpointItemComponent>(out var item, SEntMan.ComponentFactory), Is.True, prototype);
        return item!;
    }

    private static void AssertOption(VehicleSupplyEntry entry, string categoryId, string optionId, string slotId)
    {
        var category = entry.LoadoutCategories.Single(c => c.Id == categoryId);
        var option = category.Options.SingleOrDefault(o => o.Id == optionId);
        Assert.That(option, Is.Not.Null, $"{categoryId} {optionId}");
        Assert.That(option!.Item.Id, Is.EqualTo(optionId), $"{categoryId} {optionId}");
        Assert.That(option.Slot, Is.EqualTo(slotId), $"{categoryId} {optionId}");
    }

    private static void AssertState(Robust.Client.Graphics.RSI rsi, string state, string context)
    {
        if (string.IsNullOrWhiteSpace(state))
            return;

        Assert.That(rsi.TryGetState(state, out _), Is.True, $"{context}: {state} is missing from {rsi.Path}");
    }
}
