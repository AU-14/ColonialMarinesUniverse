using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using VehicleSystem = Content.Shared.Vehicle.Systems.VehicleSystem;

namespace Content.IntegrationTests.Tests.Vehicle;

[TestFixture]
[TestOf(typeof(VehicleOperatorDamageSystem))]
public sealed class VehicleOperatorSafetyTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: VehicleOperatorSafetyTestVehicle
          components:
          - type: Vehicle
            movementKind: Grid
            requiresHands: false
            transferDamage: false
          - type: VehicleOperatorDamage
          - type: GridVehicleMover

        - type: entity
          id: VehicleOperatorSafetyTestDriver
          components:
          - type: Damageable
          - type: Injurable
            damageContainer: Biological
          - type: MobState
        """;

    [Test]
    public async Task ExteriorDamageUsesConfiguredOperatorFractions()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            _ = Server.System<VehicleOperatorDamageSystem>();
            var vehicles = Server.System<VehicleSystem>();
            var transform = Server.System<SharedTransformSystem>();
            var vehicle = SEntMan.SpawnEntity("VehicleOperatorSafetyTestVehicle", map.GridCoords);
            var rammer = SEntMan.SpawnEntity("VehicleOperatorSafetyTestVehicle", map.GridCoords);
            var driver = SEntMan.SpawnEntity("VehicleOperatorSafetyTestDriver", map.GridCoords);
            var vehicleComp = SEntMan.GetComponent<VehicleComponent>(vehicle);

            Assert.That(SEntMan.HasComponent<VehicleOperatorDamageComponent>(vehicle), Is.True);
            Assert.That(vehicles.TrySetOperator((vehicle, vehicleComp), driver), Is.True);
            Assert.That(vehicleComp.Operator, Is.EqualTo(driver));

            var damage = new DamageSpecifier { DamageDict = { ["Blunt"] = 100 } };
            var vehicleCoordinates = transform.GetMapCoordinates(vehicle);
            var nearbyExplosion = new ExplosionReceivedEvent(
                "RMC",
                new MapCoordinates(vehicleCoordinates.Position + new System.Numerics.Vector2(4f, 0f), vehicleCoordinates.MapId),
                damage);
            SEntMan.EventBus.RaiseLocalEvent(vehicle, ref nearbyExplosion);
            AssertDamage(driver, 10f, "nearby explosions should transfer 10% damage");

            var directExplosion = new ExplosionReceivedEvent("RMC", vehicleCoordinates, damage);
            SEntMan.EventBus.RaiseLocalEvent(vehicle, ref directExplosion);
            AssertDamage(driver, 45f, "direct explosions should add 35% damage");

            var rammingDamage = new BeforeDamageChangedEvent(damage, rammer, rammer);
            SEntMan.EventBus.RaiseLocalEvent(vehicle, ref rammingDamage);
            AssertDamage(driver, 60f, "vehicle ramming should add 15% damage");
        });
    }

    [Test]
    public async Task DeadOperatorCannotRunGridVehicle()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var vehicles = Server.System<VehicleSystem>();
            var vehicle = SEntMan.SpawnEntity("VehicleOperatorSafetyTestVehicle", map.GridCoords);
            var driver = SEntMan.SpawnEntity("VehicleOperatorSafetyTestDriver", map.GridCoords);
            var vehicleComp = SEntMan.GetComponent<VehicleComponent>(vehicle);

            Assert.That(vehicles.TrySetOperator((vehicle, vehicleComp), driver), Is.True);

            var canRun = new VehicleCanRunEvent((vehicle, vehicleComp));
            SEntMan.EventBus.RaiseLocalEvent(vehicle, ref canRun);
            Assert.That(canRun.CanRun, Is.True);

            Server.System<MobStateSystem>().ChangeMobState(driver, MobState.Dead);

            canRun = new VehicleCanRunEvent((vehicle, vehicleComp));
            SEntMan.EventBus.RaiseLocalEvent(vehicle, ref canRun);
            Assert.That(canRun.CanRun, Is.False);
        });
    }

    private void AssertDamage(EntityUid entity, float expected, string message)
    {
        var damageable = SEntMan.GetComponent<DamageableComponent>(entity);
        var damage = Server.System<DamageableSystem>().GetPositiveDamage((entity, damageable)).GetTotal().Float();
        Assert.That(damage, Is.EqualTo(expected).Within(0.01f), message);
    }
}
