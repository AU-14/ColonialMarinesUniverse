using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using Content.Shared._CMU14.Blackfoot;
using Content.Shared._RMC14.Vehicle;
using Content.Shared._RMC14.Weapons.Ranged.Ammo.BulletBox;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Item;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Physics;
using Content.Shared.UserInterface;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Components;
using Robust.Shared.ContentPack;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.UnitTesting;
using YamlDotNet.RepresentationModel;

namespace Content.IntegrationTests._CMU14.Blackfoot;

[TestFixture]
public sealed class BlackfootPrototypeTest
{
    private static readonly EntProtoId BlackfootId = "VehicleBlackfoot";
    private static readonly EntProtoId DoorGunVariantId = "VehicleBlackfootDoorGunVariant";
    private static readonly EntProtoId ReconId = "VehicleBlackfootRecon";
    private static readonly EntProtoId TransportId = "VehicleBlackfootTransport";
    private static readonly EntProtoId FuelPumpId = "CMUBlackfootFuelPump";
    private static readonly EntProtoId FoldedLandingPadId = "CMUBlackfootLandingPadFoldedProp";
    private static readonly EntProtoId LandingPadId = "CMUBlackfootLandingPad";
    private static readonly EntProtoId FlightComputerId = "CMUBlackfootFlightComputer";
    private static readonly EntProtoId PadLightId = "CMUBlackfootLandingPadLight";
    private static readonly EntProtoId PadLightOnId = "CMUBlackfootLandingPadLightOn";
    private static readonly EntProtoId ShadowId = "CMUBlackfootShadow";
    private static readonly EntProtoId FuelPumpCrateId = "CMUBlackfootFuelPumpCrate";
    private static readonly EntProtoId FlightComputerCrateId = "CMUBlackfootFlightComputerCrate";
    private static readonly EntProtoId TugId = "CMUBlackfootAerospaceTug";
    private static readonly EntProtoId FlareAmmoBoxId = "CMUBlackfootAmmoBoxFlareLauncher";
    private static readonly EntProtoId DoorGunAmmoBoxId = "CMUBlackfootAmmoBoxDoorGun";
    private static readonly EntProtoId FlareLauncherBulletType = "VehicleAmmoBoxFlareLauncher";
    private static readonly EntProtoId DoorGunBulletType = "VehicleAmmoBoxGrenadeLauncher";

    private static readonly EntProtoId[] SupportPeripheralIds =
    {
        FuelPumpId,
        FoldedLandingPadId,
        PadLightId,
        PadLightOnId,
        FuelPumpCrateId,
        FlightComputerCrateId,
        FlareAmmoBoxId,
        DoorGunAmmoBoxId,
        TugId,
    };

    private static readonly (EntProtoId Id, bool RearDoor, bool Recon)[] Variants =
    {
        (BlackfootId, true, false),
        (DoorGunVariantId, true, false),
        (ReconId, true, true),
        (TransportId, true, false),
    };

    [Test]
    public async Task BlackfootVariantsHaveFlightInteriorAndWeaponContracts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;

            foreach (var variant in Variants)
            {
                var variantName = variant.Id.ToString();
                Assert.That(prototypes.TryIndex<EntityPrototype>(variant.Id, out var proto), Is.True);
                Assert.That(proto!.TryGetComponent<BlackfootFlightComponent>(out var flight, factory), Is.True, variantName);
                Assert.That(flight!.State, Is.EqualTo(BlackfootFlightState.Stowed), variantName);
                Assert.That(flight.VTOLSpeedMultiplier, Is.GreaterThan(1f), variantName);
                Assert.That(flight.FlightSpeedMultiplier, Is.GreaterThan(flight.VTOLSpeedMultiplier), variantName);
                Assert.That(
                    flight.FootprintOffsets,
                    Is.EquivalentTo(new[]
                    {
                        new Vector2i(-1, 1),
                        new Vector2i(0, 1),
                        new Vector2i(1, 1),
                        new Vector2i(-1, 0),
                        new Vector2i(0, 0),
                        new Vector2i(1, 0),
                        new Vector2i(0, -1),
                    }),
                    variantName);

                Assert.That(proto.TryGetComponent<BlackfootFuelPowerComponent>(out var fuel, factory), Is.True, variantName);
                Assert.That(fuel!.FuelLeakDrain, Is.GreaterThan(0f), variantName);

                AssertBlackfootXenoProjectileTarget(proto, factory, variantName);

                Assert.That(proto.TryGetComponent<VehicleWeaponsComponent>(out _, factory), Is.True, variantName);
                Assert.That(proto.TryGetComponent<VehicleEnterComponent>(out var enter, factory), Is.True, variantName);
                Assert.That(enter.EntryPoints, Has.Count.EqualTo(3), variantName);
                AssertEntryPoint(enter.EntryPoints[0], new Vector2(0f, 2f), new Vector2(8f, 9.5f), variantName);
                AssertEntryPoint(enter.EntryPoints[1], new Vector2(-1f, -1f), new Vector2(6.75f, 8.75f), variantName);
                AssertEntryPoint(enter.EntryPoints[2], new Vector2(1f, -1f), new Vector2(9.25f, 8.75f), variantName);
                Assert.That(enter.MaxPassengers, Is.EqualTo(9), variantName);
                Assert.That(enter.MaxXenos, Is.EqualTo(5), variantName);

                Assert.That(proto.TryGetComponent<ItemSlotsComponent>(out var itemSlots, factory), Is.True, variantName);
                Assert.That(itemSlots!.Slots["thrusters"].StartingItem, Is.EqualTo("VehicleBlackfootThrusters"), variantName);
                Assert.That(itemSlots.Slots["launchers"].StartingItem, Is.EqualTo("VehicleBlackfootLaunchers"), variantName);

                Assert.That(proto.TryGetComponent<HardpointSlotsComponent>(out var hardpoints, factory), Is.True, variantName);
                Assert.That(hardpoints!.VehicleFamily?.ToString(), Is.EqualTo("Blackfoot"), variantName);
                Assert.That(hardpoints.Slots.Any(slot => slot.Id == "thrusters" && slot.HardpointType == "Thruster"), Is.True, variantName);
                Assert.That(hardpoints.Slots.Any(slot => slot.Id == "launchers" && slot.HardpointType == "Launcher"), Is.True, variantName);

                Assert.That(proto.TryGetComponent<BlackfootRearDoorComponent>(out var rearDoor, factory), Is.EqualTo(variant.RearDoor), variantName);
                Assert.That(rearDoor!.Open, Is.False, variantName);
                Assert.That(rearDoor.RearEntryIndex, Is.EqualTo(0), variantName);
                Assert.That(proto.TryGetComponent<BlackfootStealthComponent>(out _, factory), Is.EqualTo(variant.Recon), variantName);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BlackfootSupportPeripheralsResolveGameplayContracts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;

            foreach (var id in SupportPeripheralIds)
            {
                Assert.That(prototypes.TryIndex<EntityPrototype>(id, out _), Is.True, id.ToString());
            }

            Assert.That(prototypes.TryIndex<EntityPrototype>(TugId, out var tugProto), Is.True);
            Assert.That(tugProto!.TryGetComponent<BlackfootTowComponent>(out var tow, factory), Is.True);
            Assert.That(tow!.CanTow, Is.True);
            Assert.That(tow.CanBeTowed, Is.False);
            Assert.That(tow.AllowAirborneTowing, Is.False);
            Assert.That(tow.TowHardpointId, Is.EqualTo("tow-hitch"));
            Assert.That(tow.AttachRange, Is.EqualTo(1.25f).Within(0.001f));
            Assert.That(tow.AttachOffset.X, Is.EqualTo(0f).Within(0.001f));
            Assert.That(tow.AttachOffset.Y, Is.EqualTo(-1f).Within(0.001f));
            Assert.That(tow.TaxiSpeedMultiplier, Is.GreaterThan(0));
            Assert.That(tow.TaxiAccelerationMultiplier, Is.GreaterThan(0));

            Assert.That(prototypes.TryIndex<EntityPrototype>(FuelPumpId, out var fuelPumpProto), Is.True);
            Assert.That(fuelPumpProto!.TryGetComponent<BlackfootFuelPumpComponent>(out _, factory), Is.True);
            Assert.That(fuelPumpProto.TryGetComponent<PhysicsComponent>(out var fuelPumpPhysics, factory), Is.True);
            Assert.That(fuelPumpPhysics!.CanCollide, Is.False);
            AssertMountedPadSupport(prototypes, factory, FuelPumpId);
            AssertPackable(prototypes, factory, FuelPumpId, FuelPumpCrateId);

            Assert.That(prototypes.TryIndex<EntityPrototype>(FlightComputerId, out var flightComputerProto), Is.True);
            Assert.That(flightComputerProto!.TryGetComponent<ActivatableUIComponent>(out var activatableUi, factory), Is.True);
            Assert.That(activatableUi!.Key, Is.EqualTo(BlackfootFlightComputerUiKey.Key));
            Assert.That(flightComputerProto.TryGetComponent<UserInterfaceComponent>(out _, factory), Is.True);
            AssertMountedPadSupport(prototypes, factory, FlightComputerId);
            AssertPackable(prototypes, factory, FlightComputerId, FlightComputerCrateId);

            Assert.That(prototypes.TryIndex<EntityPrototype>(LandingPadId, out var landingPadProto), Is.True);
            Assert.That(landingPadProto!.TryGetComponent<BlackfootLandingPadComponent>(out var landingPad, factory), Is.True);
            Assert.That(landingPad!.State, Is.EqualTo(BlackfootLandingPadState.Deployed));
            Assert.That(landingPad.FuelPumpOffset.X, Is.EqualTo(-1.15625f).Within(0.001f));
            Assert.That(landingPad.FuelPumpOffset.Y, Is.EqualTo(0f).Within(0.001f));
            Assert.That(landingPad.FlightComputerOffset.X, Is.EqualTo(-1.5f).Within(0.001f));
            Assert.That(landingPad.FlightComputerOffset.Y, Is.EqualTo(-1f).Within(0.001f));
            AssertPackable(prototypes, factory, LandingPadId, FoldedLandingPadId);

            Assert.That(prototypes.TryIndex<EntityPrototype>(PadLightId, out var padLightProto), Is.True);
            Assert.That(padLightProto!.TryGetComponent<BlackfootLandingPadLightComponent>(out var padLight, factory), Is.True);
            Assert.That(padLight!.State, Is.EqualTo(BlackfootLandingPadLightState.Off));

            Assert.That(prototypes.TryIndex<EntityPrototype>(PadLightOnId, out var padLightOnProto), Is.True);
            Assert.That(padLightOnProto!.TryGetComponent<BlackfootLandingPadLightComponent>(out var padLightOn, factory), Is.True);
            Assert.That(padLightOn!.State, Is.EqualTo(BlackfootLandingPadLightState.Servicing));

            AssertBlackfootShadowVisualOnly(prototypes, factory);

            AssertDeployable(prototypes, factory, FoldedLandingPadId, LandingPadId);
            AssertDeployable(prototypes, factory, FuelPumpCrateId, FuelPumpId, BlackfootLandingPadAttachment.FuelPump);
            AssertDeployable(prototypes, factory, FlightComputerCrateId, FlightComputerId, BlackfootLandingPadAttachment.FlightComputer);
            AssertUnpickableSupport(prototypes, factory, FoldedLandingPadId);
            AssertUnpickableSupport(prototypes, factory, FuelPumpCrateId);
            AssertUnpickableSupport(prototypes, factory, FlightComputerCrateId);

            AssertBlackfootAmmoBox(prototypes, factory, FlareAmmoBoxId, FlareLauncherBulletType, 10);
            AssertBlackfootAmmoBox(prototypes, factory, DoorGunAmmoBoxId, DoorGunBulletType, 10);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DoorGunInteriorPassengerSeatsStayBesidePilot()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var resources = server.ResolveDependency<IResourceManager>();
            var (passengerSeats, pilot, m866) = ReadDoorGunSeatLayout(resources);

            Assert.That(pilot, Is.Not.Null);
            Assert.That(m866, Is.Not.Null);
            Assert.That(passengerSeats, Has.Count.EqualTo(4));
            Assert.That(passengerSeats.All(pos => pos.Y < m866!.Value.Y - 1f), Is.True);
            Assert.That(passengerSeats.All(pos => Vector2.Distance(pos, pilot!.Value) < 2f), Is.True);
        });

        await pair.CleanReturnAsync();
    }

    private static (List<Vector2> PassengerSeats, Vector2? Pilot, Vector2? M866) ReadDoorGunSeatLayout(IResourceManager resources)
    {
        using var file = resources.ContentFileRead(new ResPath("/Maps/_CMU14/Vehicles/Blackfoot/blackfoot_doorgun.yml"));
        using var reader = new StreamReader(file);
        var yamlStream = new YamlStream();
        yamlStream.Load(reader);

        var root = yamlStream.Documents[0].RootNode;
        var yamlEntities = (YamlSequenceNode) root["entities"];
        var passengerSeats = new List<Vector2>();
        Vector2? pilot = null;
        Vector2? m866 = null;

        foreach (var group in yamlEntities.Cast<YamlMappingNode>())
        {
            var proto = group["proto"].AsString();
            if (proto != "CMUSeatBlackfootPassenger" &&
                proto != "CMUSeatBlackfootPilot" &&
                proto != "CMUSeatBlackfootDoorGunner")
                continue;

            foreach (var entity in ((YamlSequenceNode) group["entities"]).Cast<YamlMappingNode>())
            {
                var position = ReadEntityPosition(entity);
                if (proto == "CMUSeatBlackfootPassenger")
                {
                    passengerSeats.Add(position);
                    continue;
                }

                if (proto == "CMUSeatBlackfootPilot")
                    pilot = position;
                else
                    m866 = position;
            }
        }

        return (passengerSeats, pilot, m866);
    }

    private static Vector2 ReadEntityPosition(YamlMappingNode entity)
    {
        var components = (YamlSequenceNode) entity["components"];
        foreach (var component in components.Cast<YamlMappingNode>())
        {
            if (component["type"].AsString() != "Transform")
                continue;

            var pos = component["pos"].AsString();
            var coordinates = pos.Split(',');
            Assert.That(coordinates, Has.Length.EqualTo(2));
            return new Vector2(
                float.Parse(coordinates[0], CultureInfo.InvariantCulture),
                float.Parse(coordinates[1], CultureInfo.InvariantCulture));
        }

        Assert.Fail("Seat entity has no Transform position.");
        return default;
    }

    private static void AssertDeployable(
        IPrototypeManager prototypes,
        IComponentFactory factory,
        EntProtoId id,
        EntProtoId prototype,
        BlackfootLandingPadAttachment attachment = BlackfootLandingPadAttachment.None)
    {
        Assert.That(prototypes.TryIndex<EntityPrototype>(id, out var proto), Is.True, id.ToString());
        Assert.That(proto!.TryGetComponent<BlackfootDeployableSupportComponent>(out var deploy, factory), Is.True, id.ToString());
        Assert.That(deploy!.Prototype, Is.EqualTo(prototype), id.ToString());
        Assert.That(deploy.DeployTool.ToString(), Is.EqualTo("Anchoring"), id.ToString());
        Assert.That(deploy.DeployDelay, Is.GreaterThan(0), id.ToString());
        Assert.That(deploy.LandingPadAttachment, Is.EqualTo(attachment), id.ToString());

        if (attachment == BlackfootLandingPadAttachment.None)
        {
            Assert.That(deploy.RequireLandingPad, Is.False, id.ToString());
            return;
        }

        Assert.That(deploy.RequireLandingPad, Is.True, id.ToString());
        Assert.That(deploy.RequireClearFootprint, Is.True, id.ToString());
        Assert.That(deploy.ClearFootprint, Is.EqualTo(new Vector2i(1, 1)), id.ToString());
    }

    private static void AssertMountedPadSupport(
        IPrototypeManager prototypes,
        IComponentFactory factory,
        EntProtoId id)
    {
        Assert.That(prototypes.TryIndex<EntityPrototype>(id, out var proto), Is.True, id.ToString());
        Assert.That(proto!.TryGetComponent<TransformComponent>(out var xform, factory), Is.True, id.ToString());
        Assert.That(xform!.Anchored, Is.False, id.ToString());
        Assert.That(proto.TryGetComponent<PullableComponent>(out _, factory), Is.False, id.ToString());
    }

    private static void AssertPackable(
        IPrototypeManager prototypes,
        IComponentFactory factory,
        EntProtoId id,
        EntProtoId packedPrototype)
    {
        Assert.That(prototypes.TryIndex<EntityPrototype>(id, out var proto), Is.True, id.ToString());
        Assert.That(proto!.TryGetComponent<BlackfootPackableSupportComponent>(out var packable, factory), Is.True, id.ToString());
        Assert.That(packable!.PackedPrototype, Is.EqualTo(packedPrototype), id.ToString());
        Assert.That(packable.InitialTool.ToString(), Is.EqualTo("Anchoring"), id.ToString());
        Assert.That(packable.PanelTool.ToString(), Is.EqualTo("Screwing"), id.ToString());
        Assert.That(packable.FinalTool.ToString(), Is.EqualTo("Anchoring"), id.ToString());
        Assert.That(packable.InitialDelay, Is.GreaterThan(0), id.ToString());
        Assert.That(packable.PanelDelay, Is.GreaterThan(0), id.ToString());
        Assert.That(packable.FinalDelay, Is.GreaterThan(0), id.ToString());
    }

    private static void AssertUnpickableSupport(
        IPrototypeManager prototypes,
        IComponentFactory factory,
        EntProtoId id)
    {
        Assert.That(prototypes.TryIndex<EntityPrototype>(id, out var proto), Is.True, id.ToString());
        Assert.That(proto!.TryGetComponent<ItemComponent>(out _, factory), Is.False, id.ToString());
    }

    private static void AssertBlackfootXenoProjectileTarget(
        EntityPrototype proto,
        IComponentFactory factory,
        string context)
    {
        Assert.That(proto.TryGetComponent<FixturesComponent>(out var fixtures, factory), Is.True, context);
        Assert.That(fixtures!.Fixtures.Values.Any(fixture =>
            (fixture.CollisionLayer & (int) CollisionGroup.XenoProjectileImpassable) != 0), Is.True, context);
    }

    private static void AssertBlackfootShadowVisualOnly(
        IPrototypeManager prototypes,
        IComponentFactory factory)
    {
        Assert.That(prototypes.TryIndex<EntityPrototype>(ShadowId, out var proto), Is.True);
        Assert.That(proto!.TryGetComponent<DamageableComponent>(out _, factory), Is.False);
        Assert.That(proto.TryGetComponent<RequireProjectileTargetComponent>(out _, factory), Is.False);
        Assert.That(proto.TryGetComponent<FixturesComponent>(out _, factory), Is.False);
    }

    private static void AssertEntryPoint(VehicleEntryPoint entry, Vector2 offset, Vector2 interiorCoords, string context)
    {
        Assert.That(entry.Offset.X, Is.EqualTo(offset.X).Within(0.001f), context);
        Assert.That(entry.Offset.Y, Is.EqualTo(offset.Y).Within(0.001f), context);
        Assert.That(entry.Radius, Is.EqualTo(0.75f).Within(0.001f), context);
        Assert.That(entry.InteriorCoords, Is.Not.Null, context);
        Assert.That(entry.InteriorCoords!.Value.X, Is.EqualTo(interiorCoords.X).Within(0.001f), context);
        Assert.That(entry.InteriorCoords.Value.Y, Is.EqualTo(interiorCoords.Y).Within(0.001f), context);
    }

    private static void AssertBlackfootAmmoBox(
        IPrototypeManager prototypes,
        IComponentFactory factory,
        EntProtoId id,
        EntProtoId bulletType,
        int amount)
    {
        Assert.That(prototypes.TryIndex<EntityPrototype>(id, out var proto), Is.True, id.ToString());
        Assert.That(proto!.TryGetComponent<BulletBoxComponent>(out var bulletBox, factory), Is.True, id.ToString());
        Assert.That(bulletBox!.BulletType, Is.EqualTo(bulletType), id.ToString());
        Assert.That(bulletBox.Amount, Is.EqualTo(amount), id.ToString());
        Assert.That(bulletBox.Max, Is.EqualTo(amount), id.ToString());
    }
}
