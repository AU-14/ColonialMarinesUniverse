using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.Movement.Components;
using Content.Shared.Vehicle.Components;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._RMC14.Vehicle;

[TestFixture]
public sealed class RMCVehicleMoverPrototypeTest : GameTest
{
    private static readonly EntProtoId SPPCommandTank = "VehicleSPPTankCommand";
    private static readonly EntProtoId Janicart = "VehicleJanicart";

    [Test]
    public async Task SPPCommandTankDoesNotUseMobMoverTest()
    {
        var protoManager = Pair.Server.ResolveDependency<IPrototypeManager>();

        await Pair.Server.WaitAssertion(() =>
        {
            var prototype = protoManager.Index<EntityPrototype>(SPPCommandTank);

            Assert.Multiple(() =>
            {
                Assert.That(prototype.Components.TryGetComponent("Vehicle", out _), Is.True);
                Assert.That(prototype.Components.TryGetComponent("GridVehicleMover", out _), Is.True);
                Assert.That(prototype.Components.TryGetComponent("Physics", out var physics), Is.True);
                Assert.That(((PhysicsComponent) physics!).BodyType, Is.EqualTo(BodyType.Dynamic));
                Assert.That(prototype.Components.TryGetComponent("InputMover", out _), Is.False,
                    "Dynamic grid vehicles must receive their operator's input through GridVehicleMoveSystem. " +
                    "InputMover routes the vehicle through SharedMoverController, which only supports kinematic bodies.");
            });
        });
    }

    [Test]
    public async Task SPPCommandTankPlayerAttachmentDoesNotEnterMobMoverPhysicsTest()
    {
        var map = await Pair.CreateTestMap();
        var playerManager = Server.ResolveDependency<IPlayerManager>();
        var session = playerManager.Sessions.Single();
        EntityUid tank = default;

        await Server.WaitAssertion(() =>
        {
            tank = SEntMan.SpawnEntity(SPPCommandTank, map.GridCoords);
            Assert.That(playerManager.SetAttachedEntity(session, tank), Is.True);
            Assert.That(SEntMan.HasComponent<InputMoverComponent>(tank), Is.False);
        });

        await Pair.RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(session.AttachedEntity, Is.EqualTo(tank));
            Assert.That(SEntMan.GetComponent<PhysicsComponent>(tank).BodyType, Is.EqualTo(BodyType.Dynamic));
        });
    }

    [Test]
    public async Task SPPCommandTankRoutesDriverInputWithoutVehicleMobMoverTest()
    {
        var map = await Pair.CreateTestMap();
        var playerManager = Server.ResolveDependency<IPlayerManager>();
        var session = playerManager.Sessions.Single();
        EntityUid driver = default;
        EntityUid tank = default;

        await Server.WaitAssertion(() =>
        {
            driver = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            tank = SEntMan.SpawnEntity(SPPCommandTank, map.GridCoords);
            Assert.That(playerManager.SetAttachedEntity(session, driver), Is.True);

            var vehicle = SEntMan.GetComponent<VehicleComponent>(tank);
            var vehicleSystem = SEntMan.System<Content.Shared.Vehicle.Systems.VehicleSystem>();
            Assert.That(vehicleSystem.TrySetOperator((tank, vehicle), driver), Is.True);
        });

        await Pair.RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.GetComponent<VehicleComponent>(tank).Operator, Is.EqualTo(driver));
                Assert.That(SEntMan.HasComponent<InputMoverComponent>(driver), Is.True);
                Assert.That(SEntMan.HasComponent<GridVehicleOperatorComponent>(driver), Is.True);
                Assert.That(SEntMan.HasComponent<RelayInputMoverComponent>(driver), Is.False);
                Assert.That(SEntMan.HasComponent<MovementRelayTargetComponent>(tank), Is.False);
                Assert.That(SEntMan.HasComponent<InputMoverComponent>(tank), Is.False);
            });
        });
    }

    [Test]
    public async Task StandardVehicleStillUsesMovementRelayTest()
    {
        var map = await Pair.CreateTestMap();
        EntityUid driver = default;
        EntityUid vehicleUid = default;

        await Server.WaitAssertion(() =>
        {
            driver = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            vehicleUid = SEntMan.SpawnEntity(Janicart, map.GridCoords);

            var vehicle = SEntMan.GetComponent<VehicleComponent>(vehicleUid);
            var vehicleSystem = SEntMan.System<Content.Shared.Vehicle.Systems.VehicleSystem>();
            Assert.That(vehicle.MovementKind, Is.EqualTo(VehicleMovementKind.Standard));
            Assert.That(vehicleSystem.TrySetOperator((vehicleUid, vehicle), driver), Is.True);
        });

        await Pair.RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.GetComponent<VehicleComponent>(vehicleUid).Operator, Is.EqualTo(driver));
                Assert.That(SEntMan.HasComponent<RelayInputMoverComponent>(driver), Is.True);
                Assert.That(SEntMan.HasComponent<MovementRelayTargetComponent>(vehicleUid), Is.True);
                Assert.That(SEntMan.HasComponent<GridVehicleOperatorComponent>(driver), Is.False);
            });
        });
    }
}
