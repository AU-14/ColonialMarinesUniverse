using Content.IntegrationTests.Tests.Movement;
using Content.Shared.Buckle.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Vehicle.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._RMC14.Vehicle;

[TestFixture]
public sealed class RMCVehicleDriverMovementTest : MovementTest
{
    private static readonly EntProtoId SPPCommandTank = "VehicleSPPTankCommand";

    [Test]
    public async Task GridVehicleMovementDoesNotUnbuckleDriverTest()
    {
        await SpawnTarget("Chair");
        var vehicleMap = await Pair.CreateTestMap();
        EntityUid tank = default;

        await Server.WaitAssertion(() =>
        {
            tank = SEntMan.SpawnEntity(SPPCommandTank, vehicleMap.GridCoords);
        });

        await Interact();

        await Server.WaitAssertion(() =>
        {
            var vehicles = SEntMan.System<Content.Shared.Vehicle.VehicleSystem>();
            var vehicle = SEntMan.GetComponent<VehicleComponent>(tank);

            Assert.That(SEntMan.GetComponent<BuckleComponent>(SPlayer).Buckled, Is.True);
            Assert.That(vehicles.TrySetOperator((tank, vehicle), SPlayer));
        });

        await RunTicks(5);

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.GetComponent<BuckleComponent>(SPlayer).Buckled, Is.True);
                Assert.That(
                    SEntMan.GetComponent<VehicleComponent>(tank).MovementKind,
                    Is.EqualTo(VehicleMovementKind.Grid));
            });
        });

        await Move(DirectionFlag.East, 0.25f);

        await Server.WaitAssertion(() =>
        {
            var buckle = SEntMan.GetComponent<BuckleComponent>(SPlayer);
            var vehicle = SEntMan.GetComponent<VehicleComponent>(tank);

            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<RelayInputMoverComponent>(SPlayer), Is.False);
                Assert.That(SEntMan.GetComponent<InputMoverComponent>(SPlayer).CanMove, Is.False);
                Assert.That(buckle.Buckled, Is.True);
                Assert.That(buckle.BuckledTo, Is.EqualTo(STarget!.Value));
                Assert.That(vehicle.Operator, Is.EqualTo(SPlayer));
            });
        });
    }
}
