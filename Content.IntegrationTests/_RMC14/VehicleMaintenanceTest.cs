#pragma warning disable RA0002 // Arrange vehicle damage and inspect repair progress.
using System.Linq;
using System.Numerics;
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.IntegrationTests._RMC14;

[TestFixture]
public sealed class VehicleMaintenanceTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: MaintenanceTestModule
          components:
          - type: HardpointItem
            hardpointType: Support
          - type: HardpointIntegrity
            maxIntegrity: 100
          - type: VehicleTurret
            rotateToCursor: true
            rotationSpeed: 90

        - type: entity
          id: MaintenanceTestTank
          components:
          - type: Vehicle
            movementKind: Grid
            transferDamage: false
          - type: VehicleMaintenance
            panelDelay: 0.1
          - type: GridVehicleMover
          - type: HardpointIntegrity
          - type: HardpointSlots
            slots:
            - id: module
              hardpointType: Support
              required: false
          - type: ItemSlots
            slots:
              module:
                startingItem: MaintenanceTestModule
        """;

    [Test]
    public async Task PanelsLockEngineTurretAndGunUntilClosed()
    {
        var map = await Pair.CreateTestMap();
        map.GridCoords = new Robust.Shared.Map.EntityCoordinates(map.Grid.Owner,
            new Vector2(map.Tile.GridIndices.X + 0.5f, map.Tile.GridIndices.Y + 0.5f));
        EntityUid tank = default, user = default, module = default;
        await Server.WaitAssertion(() =>
        {
            tank = SEntMan.SpawnEntity("MaintenanceTestTank", map.GridCoords);
            user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            module = Server.System<VehicleTopologySystem>().GetMountedSlots(tank).Single().Item!.Value;
            var maintenance = SEntMan.GetComponent<VehicleMaintenanceComponent>(tank);
            var mover = SEntMan.GetComponent<GridVehicleMoverComponent>(tank);
            mover.CurrentSpeed = 1;
            Assert.That(Server.System<VehicleMaintenanceSystem>().TryToggle((tank, maintenance), user), Is.False);
            mover.CurrentSpeed = 0;
            Assert.That(Server.System<VehicleMaintenanceSystem>().TryToggle((tank, maintenance), user), Is.True);
            Assert.That(maintenance.Mode, Is.EqualTo(VehicleMaintenanceMode.Opening));
            AssertControls(tank, module, user, false);
        });
        await Pair.RunTicksSync(15);
        await Server.WaitAssertion(() =>
        {
            var maintenance = SEntMan.GetComponent<VehicleMaintenanceComponent>(tank);
            Assert.That(maintenance.Mode, Is.EqualTo(VehicleMaintenanceMode.Maintenance),
                $"user at {SEntMan.GetComponent<TransformComponent>(user).Coordinates}, damage {Server.System<DamageableSystem>().GetTotalDamage(user)}");
            var turret = SEntMan.GetComponent<VehicleTurretComponent>(module);
            turret.TargetRotation = Angle.FromDegrees(90);
            var before = turret.WorldRotation;
            typeof(VehicleTurretSystem).GetMethod("UpdateTurretRotation", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(Server.System<VehicleTurretSystem>(), [module, turret, tank, 1f]);
            Assert.That(turret.WorldRotation, Is.EqualTo(before));
            AssertControls(tank, module, user, false);
            Assert.That(Server.System<VehicleMaintenanceSystem>().TryToggle((tank, maintenance), user), Is.True);
            Assert.That(maintenance.Mode, Is.EqualTo(VehicleMaintenanceMode.Closing));
            AssertControls(tank, module, user, false);
        });
        await Pair.RunTicksSync(15);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<VehicleMaintenanceComponent>(tank).Mode, Is.EqualTo(VehicleMaintenanceMode.Operational));
            var turret = SEntMan.GetComponent<VehicleTurretComponent>(module);
            turret.TargetRotation = Angle.FromDegrees(90);
            Server.System<VehicleTurretSystem>().Update(1f);
            Assert.That(turret.WorldRotation, Is.Not.EqualTo(Angle.Zero), "traverse resumes after closing");
            turret.TargetRotation = turret.WorldRotation;
            AssertControls(tank, module, user, true);
        });
    }

    [Test]
    public async Task DriverReceivesActionAndCrewCanRecoverADeletedPanelOperator()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            foreach (var prototype in new[] { "VehicleTank", "VehicleTankPMC", "VehicleTankTWE", "VehicleSPPTank", "VehicleSPPTankCommand", "VehicleAev" })
                Assert.That(SProtoMan.Index<Robust.Shared.Prototypes.EntityPrototype>(prototype).Components.ContainsKey("VehicleMaintenance"), Is.True, prototype);
            var tank = SEntMan.SpawnEntity("MaintenanceTestTank", map.GridCoords);
            var driver = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var vehicle = SEntMan.GetComponent<VehicleComponent>(tank);
            var maintenance = SEntMan.GetComponent<VehicleMaintenanceComponent>(tank);
            maintenance.PanelDelay = TimeSpan.FromSeconds(5);
            var driving = Server.System<Content.Shared.Vehicle.Systems.VehicleSystem>();
            Assert.That(driving.TrySetOperator((tank, vehicle), driver), Is.True);
            Assert.That(maintenance.DriverAction, Is.Not.Null);
            Assert.That(Server.System<VehicleMaintenanceSystem>().TryToggle((tank, maintenance), driver, fromDriver: true), Is.True);
            Assert.That(driving.TryRemoveOperator((tank, vehicle)), Is.True);
            Assert.That(maintenance.DriverAction, Is.Null);
            SEntMan.DeleteEntity(driver);
            var mechanic = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            Assert.That(Server.System<VehicleMaintenanceSystem>().TryToggle((tank, maintenance), mechanic), Is.True,
                "a deleted user's timed action must not permanently lock the panels");
        });
    }

    private void AssertControls(EntityUid tank, EntityUid module, EntityUid user, bool available)
    {
        var run = new VehicleCanRunEvent((tank, SEntMan.GetComponent<VehicleComponent>(tank)));
        SEntMan.EventBus.RaiseLocalEvent(tank, ref run);
        Assert.That(run.CanRun, Is.EqualTo(available));
        var shoot = new AttemptShootEvent(user, null, SEntMan.GetComponent<TransformComponent>(tank).Coordinates, null);
        SEntMan.EventBus.RaiseLocalEvent(module, ref shoot);
        Assert.That(shoot.Cancelled, Is.EqualTo(!available));
    }

    [Test]
    public async Task OpenPanelsIncreaseDamageAndClosingCancelsRepairs()
    {
        var map = await Pair.CreateTestMap();
        map.GridCoords = new Robust.Shared.Map.EntityCoordinates(map.Grid.Owner,
            new Vector2(map.Tile.GridIndices.X + 0.5f, map.Tile.GridIndices.Y + 0.5f));
        EntityUid tank = default, user = default, module = default, welder = default;
        await Server.WaitAssertion(() =>
        {
            tank = SEntMan.SpawnEntity("MaintenanceTestTank", map.GridCoords);
            user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            welder = SEntMan.SpawnEntity("RMCWelderAdmin", map.GridCoords);
            Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(user, welder), Is.True);
            Assert.That(Server.System<ItemToggleSystem>().TryActivate((welder, null), user), Is.True);
            module = Server.System<VehicleTopologySystem>().GetMountedSlots(tank).Single().Item!.Value;
            var integrity = SEntMan.GetComponent<HardpointIntegrityComponent>(module);
            integrity.Integrity = 99;
            Weld(user, welder, module);
            Assert.That(integrity.Repairing, Is.False, "operational tanks cannot be repaired");
            SEntMan.GetComponent<VehicleMaintenanceComponent>(tank).Mode = VehicleMaintenanceMode.Maintenance;
            Weld(user, welder, module);
            Assert.That(integrity.Repairing, Is.True);
            // A hit during the short finishing weld must not increase that weld's repair amount.
            integrity.Integrity = 50;
        });
        await Pair.RunTicksSync(80);
        await Server.WaitAssertion(() =>
        {
            var integrity = SEntMan.GetComponent<HardpointIntegrityComponent>(module);
            Assert.That(integrity.Integrity, Is.EqualTo(51).Within(0.01));
            Assert.That(integrity.Repairing, Is.True, "the repeating weld must retain its per-part lock");
            var repeat = SEntMan.GetComponent<DoAfterComponent>(user).DoAfters.Values.Single(d => !d.Completed && !d.Cancelled);
            Assert.That(repeat.Args.Delay.TotalSeconds, Is.GreaterThan(1), "a full chunk must receive its full repair time");
            var secondUser = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            Weld(secondUser, welder, module);
            Assert.That(SEntMan.GetComponent<DoAfterComponent>(secondUser).DoAfters, Is.Empty);

            var naked = SEntMan.SpawnEntity(null, map.GridCoords);
            var panels = SEntMan.AddComponent<VehicleMaintenanceComponent>(naked);
            panels.Mode = VehicleMaintenanceMode.Maintenance;
            var damage = new DamageSpecifier { DamageDict = { ["Blunt"] = 10 } };
            var modify = new DamageModifyEvent(damage, null);
            SEntMan.EventBus.RaiseLocalEvent(naked, modify);
            Assert.That(modify.Damage.GetTotal().Float(), Is.EqualTo(15));
            Assert.That(Server.System<VehicleMaintenanceSystem>().TryToggle(
                (tank, SEntMan.GetComponent<VehicleMaintenanceComponent>(tank)), secondUser), Is.True);
        });
        await Pair.RunTicksSync(15);
        await Server.WaitAssertion(() =>
        {
            var integrity = SEntMan.GetComponent<HardpointIntegrityComponent>(module);
            Assert.That(integrity.Repairing, Is.False);
            Assert.That(integrity.Integrity, Is.EqualTo(51).Within(0.01));
        });
    }

    private void Weld(EntityUid user, EntityUid welder, EntityUid module)
    {
        var ev = new InteractUsingEvent(user, welder, module, SEntMan.GetComponent<TransformComponent>(module).Coordinates);
        SEntMan.EventBus.RaiseLocalEvent(module, ev);
        Assert.That(ev.Handled, Is.True);
    }
}
