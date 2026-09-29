#pragma warning disable RA0002 // Regression tests intentionally arrange component damage and faults.

using System.Linq;
using System.Numerics;
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Examine;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Components;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.IntegrationTests._RMC14;

[TestFixture]
public sealed class VehicleDamageRegressionTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: VehicleDamageRegressionModule
          name: test support module
          components:
          - type: HardpointItem
            hardpointType: Support
          - type: HardpointIntegrity
            maxIntegrity: 100

        - type: entity
          id: VehicleDamageRegressionChassis
          components:
          - type: Vehicle
            movementKind: Grid
            transferDamage: false
          - type: Tag
            tags: [VehicleHeavy]
          - type: GridVehicleMover
            canSmashWalls: true
            wallSmashMinSpeed: 1.5
            wallSmashHullDamage: 12
          - type: Physics
            bodyType: Dynamic
          - type: Fixtures
            fixtures:
              body:
                shape: !type:PhysShapeAabb
                  bounds: '-1,-1,1,1'
                density: 1500
                mask: [Impassable, LowImpassable, MidImpassable, BarricadeImpassable]
                layer: [LargeMobLayer]
          - type: HardpointIntegrity
          - type: HardpointSlots
            slots:
            - id: first
              hardpointType: Support
              required: false
            - id: second
              hardpointType: Support
              required: false
          - type: ItemSlots
            slots:
              first:
                startingItem: VehicleDamageRegressionModule
              second:
                startingItem: VehicleDamageRegressionModule
        """;

    [TestCase("CMBarricadeMetalDoor")]
    [TestCase("WallXenoResin")]
    [TestCase("DoorXenoResin")]
    [TestCase("AU14TallFloodlight")]
    [TestCase("AU14Streetlight")]
    [TestCase("RMCTallFloodlight")]
    // CMU14: vehicle damage and conscious controls.
    public async Task HeavyVehiclePaysOneDamageBudgetToCrushLightObstacles(string obstaclePrototype)
    {
        var map = await Pair.CreateTestMap();
        EntityUid obstacle = default;
        await Server.WaitAssertion(() =>
        {
            var vehicle = SEntMan.SpawnEntity("VehicleDamageRegressionChassis", map.GridCoords);
            obstacle = SEntMan.SpawnEntity(obstaclePrototype, map.GridCoords.Offset(new Vector2(0.5f, 0)));
            var mover = SEntMan.GetComponent<GridVehicleMoverComponent>(vehicle);
            mover.CurrentSpeed = 2f;
            var grid = SEntMan.GetComponent<TransformComponent>(vehicle).GridUid!.Value;
            var position = SEntMan.GetComponent<TransformComponent>(vehicle).LocalPosition;
            var collision = typeof(GridVehicleMoverSystem).GetMethod("CanOccupyTransform", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var system = SEntMan.System<GridVehicleMoverSystem>();
            var modules = SEntMan.System<VehicleTopologySystem>().GetMountedSlots(vehicle)
                .Select(slot => slot.Item!.Value).ToArray();
            Assert.That(modules, Has.Length.EqualTo(2));

            for (var i = 0; i < 20; i++)
            {
                Assert.That(collision.Invoke(system,
                    [vehicle, mover, grid, position, Angle.Zero, 0f, true, false, null, null, null, null]), Is.True);
            }

            Assert.Multiple(() =>
            {
                foreach (var module in modules)
                {
                    // CMU14: vehicle damage and conscious controls.
                    Assert.That(SEntMan.GetComponent<HardpointIntegrityComponent>(module).Integrity, Is.LessThan(100f));
                    Assert.That(SEntMan.HasComponent<VehicleHardpointFailureComponent>(module), Is.False);
                }
                Assert.That(modules.Sum(uid => 100f - SEntMan.GetComponent<HardpointIntegrityComponent>(uid).Integrity),
                    Is.InRange(0.01f, 12.001f), "one destroyed obstacle charges at most one collision budget");
            });
        });
        await Server.WaitRunTicks(1);
        await Server.WaitAssertion(() => Assert.That(SEntMan.Deleted(obstacle), Is.True,
            "the obstacle must actually break instead of remaining under the vehicle"));
    }

    [Test]
    public async Task CollisionHullDamageIsOneSharedBudgetAndMinorDamageNeverRollsFaults()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var vehicle = SEntMan.SpawnEntity("VehicleDamageRegressionChassis", map.GridCoords);
            var modules = SEntMan.System<VehicleTopologySystem>().GetMountedSlots(vehicle)
                .Select(slot => slot.Item!.Value).ToArray();
            var hardpoints = SEntMan.System<HardpointSystem>();
            Assert.That(hardpoints.DamageVehicleHull(vehicle, 12f), Is.True);
            Assert.That(modules.Sum(uid => 100f - SEntMan.GetComponent<HardpointIntegrityComponent>(uid).Integrity),
                Is.EqualTo(12f).Within(0.001f));

            var first = SEntMan.GetComponent<HardpointIntegrityComponent>(modules[0]);
            first.Integrity = 30f;
            first.FailureChance = 1f;
            for (var i = 0; i < 100; i++)
                hardpoints.DamageHardpoint(vehicle, modules[0], 0.1f);
            Assert.That(SEntMan.HasComponent<VehicleHardpointFailureComponent>(modules[0]), Is.False,
                "even guaranteed random rolls must not turn tiny damage into malfunctions");
        });
    }

    // CMU14: vehicle damage and conscious controls.
    [TestCase("CMWallMetal", 2f, 12f)]
    [TestCase("CMWallReinforced", 2f, 12f)]
    [TestCase("CMWallReinforced", 0.2f, 0f)]
    public async Task ObstaclesChargeOneCollisionDamageBudget(string wallPrototype, float speed, float expectedDamage)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var vehicle = SEntMan.SpawnEntity("VehicleDamageRegressionChassis", map.GridCoords);
            var wall = SEntMan.SpawnEntity(wallPrototype, map.GridCoords.Offset(new Vector2(0.5f, 0)));
            var mover = SEntMan.GetComponent<GridVehicleMoverComponent>(vehicle);
            mover.CurrentSpeed = speed;
            mover.WallSmashCooldown = 0f;
            var impact = typeof(GridVehicleMoverSystem).GetMethod("ApplyHeavySmashSelfDamage",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            for (var i = 0; i < 20; i++)
                impact.Invoke(SEntMan.System<GridVehicleMoverSystem>(), [vehicle, mover, wall, 1f]);

            var remaining = SEntMan.System<VehicleTopologySystem>().GetMountedSlots(vehicle)
                .Sum(slot => SEntMan.GetComponent<HardpointIntegrityComponent>(slot.Item!.Value).Integrity);
            Assert.That(200f - remaining, Is.EqualTo(expectedDamage).Within(0.001f));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task SeriousHitsRespectVehicleWideCooldownAndFaultLimit(bool useFaultLimit)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var vehicle = SEntMan.SpawnEntity("VehicleDamageRegressionChassis", map.GridCoords);
            var modules = SEntMan.System<VehicleTopologySystem>().GetMountedSlots(vehicle)
                .Select(slot => slot.Item!.Value).ToArray();
            var hardpoints = SEntMan.System<HardpointSystem>();
            foreach (var module in modules)
            {
                var integrity = SEntMan.GetComponent<HardpointIntegrityComponent>(module);
                integrity.Integrity = 40f;
                integrity.FailureChance = 1f;
                hardpoints.DamageHardpoint(vehicle, module, 10f);
                if (useFaultLimit)
                {
                    var frame = SEntMan.GetComponent<HardpointIntegrityComponent>(vehicle);
                    frame.MaxVehicleFailures = 1;
                    frame.NextFailureRoll = TimeSpan.Zero;
                }
            }

            Assert.That(SEntMan.GetComponent<VehicleHardpointFailureComponent>(modules[0]).ActiveFailures,
                Is.EquivalentTo(new[] { VehicleHardpointFailure.ElectricalShort }));
            Assert.That(SEntMan.HasComponent<VehicleHardpointFailureComponent>(modules[1]), Is.False);
            if (!useFaultLimit)
            {
                Assert.That(SEntMan.GetComponent<HardpointIntegrityComponent>(vehicle).NextFailureRoll,
                    Is.GreaterThan(SGameTiming.CurTime));
            }
        });
    }

    [Test]
    public async Task RunawayTriggerFiresMountedWeaponWithoutAnOperatorAndStopsWhenCleared()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var vehicle = SEntMan.SpawnEntity("VehicleDamageRegressionChassis", map.GridCoords);
            var module = SEntMan.System<VehicleTopologySystem>().GetMountedSlots(vehicle)[0].Item!.Value;
            var gun = SEntMan.EnsureComponent<GunComponent>(module);
            SEntMan.EnsureComponent<VehicleTurretComponent>(module);
            var ammo = SEntMan.EnsureComponent<BasicEntityAmmoProviderComponent>(module);
            ammo.Proto = "BulletPistol";
            ammo.Capacity = 3;
            ammo.Count = 3;
            var failures = SEntMan.EnsureComponent<VehicleHardpointFailureComponent>(module);
            failures.ActiveFailures.Add(VehicleHardpointFailure.RunawayTrigger);
            failures.NextRunawayFireAt = SGameTiming.CurTime - TimeSpan.FromSeconds(1);

            var weapons = SEntMan.System<VehicleWeaponsSystem>();
            weapons.Update(0f);
            Assert.That(ammo.Count, Is.EqualTo(2), "the runaway fault must actually discharge the mounted weapon");

            failures.ActiveFailures.Clear();
            failures.NextRunawayFireAt = SGameTiming.CurTime - TimeSpan.FromSeconds(1);
            gun.NextFire = TimeSpan.Zero;
            weapons.Update(0f);
            Assert.That(ammo.Count, Is.EqualTo(2), "clearing the fault must stop automatic discharges");
        });
    }

    [Test]
    public async Task DamageExamineListsMountedFaultsAndEffectsWithoutRepairInstructions()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var vehicle = SEntMan.SpawnEntity("VehicleDamageRegressionChassis", map.GridCoords);
            var module = SEntMan.System<VehicleTopologySystem>().GetMountedSlots(vehicle)[0].Item!.Value;
            SEntMan.EnsureComponent<VehicleHardpointFailureComponent>(vehicle).ActiveFailures.Add(VehicleHardpointFailure.EngineMisfire);
            SEntMan.EnsureComponent<VehicleHardpointFailureComponent>(module).ActiveFailures.Add(VehicleHardpointFailure.RunawayTrigger);

            var verbs = new GetVerbsEvent<ExamineVerb>(vehicle, vehicle, null, null, true, true, true, []);
            SEntMan.EventBus.RaiseLocalEvent(vehicle, verbs);
            Assert.That(verbs.Verbs.Any(verb => verb.Text == "Vehicle damage"), Is.True);

            var group = SEntMan.EnsureComponent<GroupExamineComponent>(vehicle);
            group.Group = [new ExamineGroup
            {
                Components = ["HardpointIntegrity"],
                ContextText = "rmc-vehicle-damage-examine-verb",
                HoverMessage = "rmc-vehicle-damage-examine-description",
            }];
            verbs = new GetVerbsEvent<ExamineVerb>(vehicle, vehicle, null, null, true, true, true, []);
            SEntMan.EventBus.RaiseLocalEvent(vehicle, verbs);
            var details = group.Group.Single().Entries.Single().Message.ToMarkup();
            Assert.Multiple(() =>
            {
                Assert.That(details, Does.Contain("Integrity:"));
                Assert.That(details, Does.Contain("test support module"));
                Assert.That(details, Does.Contain("Engine misfire"));
                Assert.That(details, Does.Contain("Runaway trigger"));
                Assert.That(details, Does.Contain("discharge on its own while mounted"));
                Assert.That(details, Does.Contain("acceleration and top speed are reduced"));
                Assert.That(details, Does.Not.Contain("Repair:"));
                Assert.That(details, Does.Not.Contain("multitool"));
                Assert.That(details, Does.Not.Contain("step 1/"));
            });

            var basic = new ExaminedEvent(new FormattedMessage(), vehicle, vehicle, true, false);
            SEntMan.EventBus.RaiseLocalEvent(vehicle, basic);
            Assert.That(basic.GetTotalMessage().ToMarkup(), Does.Not.Contain("Integrity:"));
            Assert.That(basic.GetTotalMessage().ToMarkup(), Does.Not.Contain("Repair:"));
        });
    }
}
