using Content.IntegrationTests.Fixtures;
using Content.Shared.Movement.Components;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Components;
using Content.Shared.Vehicle.Systems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Vehicle;

[TestFixture]
[TestOf(typeof(VehicleSystem))]
public sealed class VehicleOperatorLifecycleTest : GameTest
{
    [Test]
    public async Task StandardOperatorUsesRelayAndCleansUpExactlyOnce()
    {
        var map = await Pair.CreateTestMap();
        EntityUid vehicle = default;
        EntityUid driver = default;

        await Server.WaitAssertion(() =>
        {
            _ = Server.System<VehicleEventProbeSystem>();
            var vehicles = Server.System<VehicleSystem>();
            vehicle = SEntMan.SpawnEntity("ChairOfficeLight", map.GridCoords);
            driver = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var vehicleProbe = SEntMan.EnsureComponent<VehicleEventProbeComponent>(vehicle);
            var driverProbe = SEntMan.EnsureComponent<VehicleEventProbeComponent>(driver);
            var component = SEntMan.GetComponent<VehicleComponent>(vehicle);

            Assert.That(component.MovementKind, Is.EqualTo(VehicleMovementKind.Standard));
            Assert.That(vehicles.TrySetOperator((vehicle, component), driver), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(component.Operator, Is.EqualTo(driver));
                Assert.That(SEntMan.GetComponent<VehicleOperatorComponent>(driver).Vehicle, Is.EqualTo(vehicle));
                Assert.That(SEntMan.GetComponent<RelayInputMoverComponent>(driver).RelayEntity, Is.EqualTo(vehicle));
                Assert.That(SEntMan.GetComponent<MovementRelayTargetComponent>(vehicle).Source, Is.EqualTo(driver));
                Assert.That(SEntMan.HasComponent<GridVehicleOperatorComponent>(driver), Is.False);
                Assert.That(vehicleProbe.OperatorSetEvents, Is.EqualTo(1));
                Assert.That(driverProbe.EnteredEvents, Is.EqualTo(1));
                Assert.That(driverProbe.ExitedEvents, Is.Zero);
            });

            Assert.That(vehicles.TryRemoveOperator((vehicle, component)), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(component.Operator, Is.Null);
                Assert.That(vehicleProbe.OperatorSetEvents, Is.EqualTo(2));
                Assert.That(driverProbe.EnteredEvents, Is.EqualTo(1));
                Assert.That(driverProbe.ExitedEvents, Is.EqualTo(1));
            });
        });

        await Pair.RunTicksSync(1);

        await Server.WaitAssertion(() =>
        {
            var vehicleProbe = SEntMan.GetComponent<VehicleEventProbeComponent>(vehicle);
            var driverProbe = SEntMan.GetComponent<VehicleEventProbeComponent>(driver);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<VehicleOperatorComponent>(driver), Is.False);
                Assert.That(SEntMan.HasComponent<RelayInputMoverComponent>(driver), Is.False);
                Assert.That(SEntMan.HasComponent<MovementRelayTargetComponent>(vehicle), Is.False);
                Assert.That(vehicleProbe.OperatorSetEvents, Is.EqualTo(2), "deferred cleanup must not raise a second clear event");
                Assert.That(driverProbe.EnteredEvents, Is.EqualTo(1));
                Assert.That(driverProbe.ExitedEvents, Is.EqualTo(1), "operator shutdown must not raise a duplicate exit event");
            });
        });
    }

    [Test]
    public async Task GridOperatorUsesGridMarkersAndPreservesMover()
    {
        var map = await Pair.CreateTestMap();
        EntityUid vehicle = default;
        EntityUid driver = default;

        await Server.WaitAssertion(() =>
        {
            _ = Server.System<VehicleEventProbeSystem>();
            var vehicles = Server.System<VehicleSystem>();
            vehicle = SEntMan.SpawnEntity("VehicleHumvee", map.GridCoords);
            driver = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var vehicleProbe = SEntMan.EnsureComponent<VehicleEventProbeComponent>(vehicle);
            var driverProbe = SEntMan.EnsureComponent<VehicleEventProbeComponent>(driver);
            var component = SEntMan.GetComponent<VehicleComponent>(vehicle);

            Assert.That(component.MovementKind, Is.EqualTo(VehicleMovementKind.Grid));
            Assert.That(vehicles.TrySetOperator((vehicle, component), driver), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<GridVehicleMoverComponent>(vehicle), Is.True);
                Assert.That(SEntMan.HasComponent<GridVehicleOperatorComponent>(driver), Is.True);
                Assert.That(SEntMan.HasComponent<RelayInputMoverComponent>(driver), Is.False);
                Assert.That(SEntMan.HasComponent<MovementRelayTargetComponent>(vehicle), Is.False);
                Assert.That(vehicleProbe.OperatorSetEvents, Is.EqualTo(1));
                Assert.That(driverProbe.EnteredEvents, Is.EqualTo(1));
            });

            Assert.That(vehicles.TryRemoveOperator((vehicle, component)), Is.True);
        });

        await Pair.RunTicksSync(1);

        await Server.WaitAssertion(() =>
        {
            var vehicleProbe = SEntMan.GetComponent<VehicleEventProbeComponent>(vehicle);
            var driverProbe = SEntMan.GetComponent<VehicleEventProbeComponent>(driver);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<GridVehicleMoverComponent>(vehicle), Is.True,
                    "leaving a grid vehicle must preserve its movement controller");
                Assert.That(SEntMan.HasComponent<GridVehicleOperatorComponent>(driver), Is.False);
                Assert.That(SEntMan.HasComponent<VehicleOperatorComponent>(driver), Is.False);
                Assert.That(vehicleProbe.OperatorSetEvents, Is.EqualTo(2));
                Assert.That(driverProbe.EnteredEvents, Is.EqualTo(1));
                Assert.That(driverProbe.ExitedEvents, Is.EqualTo(1));
            });
        });
    }

    [Test]
    public async Task OperatorComponentShutdownClearsVehicleWithoutDuplicateEvents()
    {
        var map = await Pair.CreateTestMap();
        EntityUid vehicle = default;
        EntityUid driver = default;

        await Server.WaitAssertion(() =>
        {
            _ = Server.System<VehicleEventProbeSystem>();
            var vehicles = Server.System<VehicleSystem>();
            vehicle = SEntMan.SpawnEntity("ChairOfficeLight", map.GridCoords);
            driver = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            SEntMan.EnsureComponent<VehicleEventProbeComponent>(vehicle);
            SEntMan.EnsureComponent<VehicleEventProbeComponent>(driver);
            var component = SEntMan.GetComponent<VehicleComponent>(vehicle);

            Assert.That(vehicles.TrySetOperator((vehicle, component), driver), Is.True);
            Assert.That(SEntMan.RemoveComponent<VehicleOperatorComponent>(driver), Is.True);
            Assert.That(component.Operator, Is.Null);
        });

        await Pair.RunTicksSync(1);

        await Server.WaitAssertion(() =>
        {
            var vehicleProbe = SEntMan.GetComponent<VehicleEventProbeComponent>(vehicle);
            var driverProbe = SEntMan.GetComponent<VehicleEventProbeComponent>(driver);
            Assert.Multiple(() =>
            {
                Assert.That(vehicleProbe.OperatorSetEvents, Is.EqualTo(2));
                Assert.That(driverProbe.EnteredEvents, Is.EqualTo(1));
                Assert.That(driverProbe.ExitedEvents, Is.EqualTo(1));
                Assert.That(SEntMan.HasComponent<RelayInputMoverComponent>(driver), Is.False);
                Assert.That(SEntMan.HasComponent<MovementRelayTargetComponent>(vehicle), Is.False);
            });
        });
    }
}

[RegisterComponent]
public sealed partial class VehicleEventProbeComponent : Component
{
    public int EnteredEvents;
    public int ExitedEvents;
    public int OperatorSetEvents;
}

public sealed class VehicleEventProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VehicleEventProbeComponent, OnVehicleEnteredEvent>(OnEntered);
        SubscribeLocalEvent<VehicleEventProbeComponent, OnVehicleExitedEvent>(OnExited);
        SubscribeLocalEvent<VehicleEventProbeComponent, VehicleOperatorSetEvent>(OnOperatorSet);
    }

    private static void OnEntered(Entity<VehicleEventProbeComponent> ent, ref OnVehicleEnteredEvent args)
    {
        ent.Comp.EnteredEvents++;
    }

    private static void OnExited(Entity<VehicleEventProbeComponent> ent, ref OnVehicleExitedEvent args)
    {
        ent.Comp.ExitedEvents++;
    }

    private static void OnOperatorSet(Entity<VehicleEventProbeComponent> ent, ref VehicleOperatorSetEvent args)
    {
        ent.Comp.OperatorSetEvents++;
    }
}
