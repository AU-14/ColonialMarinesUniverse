#pragma warning disable RA0002 // Drive the real movement loop with held server-side input.

using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Components;
using Content.Shared.Vehicle.Systems;
using Robust.Server.GameStates;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Performance;

[TestFixture]
public sealed class CMUVehicleReplicationTest : GameTest
{
    [Test]
    public async Task IdleVehicleDoesNotDirtyButDrivingAndStoppingReachClient()
    {
        var map = await Pair.CreateTestMap();
        EntityUid vehicle = default;
        EntityUid driver = default;
        NetEntity net = default;
        GameTick idleTick = default;
        Vector2 stopped = default;
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            for (var x = -8; x <= 8; x++)
            for (var y = -8; y <= 16; y++)
                maps.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(x, y), map.Tile.Tile);

            vehicle = SEntMan.SpawnEntity("VehicleHumvee", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
            var wheels = SEntMan.SpawnEntity("VehicleHumveeWheel", map.GridCoords);
            Assert.That(Server.System<ItemSlotsSystem>().TryInsert(vehicle, "wheel-1", wheels, null), Is.True);
            driver = SEntMan.SpawnEntity(null, map.GridCoords);
            SEntMan.EnsureComponent<HandsComponent>(driver);
            SEntMan.EnsureComponent<EyeComponent>(driver);
            SEntMan.EnsureComponent<InputMoverComponent>(driver);
            net = SEntMan.GetNetEntity(vehicle);
            Server.System<PvsOverrideSystem>().AddSessionOverride(vehicle, ServerSession!);
        });
        await Pair.RunTicksSync(15);
        await Server.WaitAssertion(() =>
            idleTick = SEntMan.GetComponent<GridVehicleMoverComponent>(vehicle).LastModifiedTick);
        await Pair.RunTicksSync(10);
        await Server.WaitAssertion(() =>
        {
            var mover = SEntMan.GetComponent<GridVehicleMoverComponent>(vehicle);
            Assert.That(mover.LastModifiedTick, Is.EqualTo(idleTick),
                "Stationary fixed-step updates must not generate another full mover state.");
            var vehicleComp = SEntMan.GetComponent<VehicleComponent>(vehicle);
            Assert.That(Server.System<VehicleSystem>().TrySetOperator((vehicle, vehicleComp), driver), Is.True);
            SEntMan.GetComponent<InputMoverComponent>(driver).HeldMoveButtons = MoveButtons.Up;
            var canRun = new VehicleCanRunEvent((vehicle, vehicleComp));
            SEntMan.EventBus.RaiseLocalEvent(vehicle, ref canRun);
            Assert.That(canRun.CanRun, Is.True);
        });
        await Pair.RunTicksSync(10);
        await Server.WaitAssertion(() =>
        {
            var mover = SEntMan.GetComponent<GridVehicleMoverComponent>(vehicle);
            Assert.That(mover.CurrentSpeed, Is.GreaterThan(0));
            Assert.That(mover.LastModifiedTick, Is.GreaterThan(idleTick));
        });
        await Client.WaitAssertion(() =>
        {
            var received = CEntMan.GetComponent<GridVehicleMoverComponent>(CEntMan.GetEntity(net));
            Assert.That(received.IsMoving, Is.True, "Suppressing idle updates must preserve movement replication.");
            Assert.That(received.Position, Is.Not.EqualTo(new Vector2(0.5f, 0.5f)));
        });
        await Server.WaitPost(() =>
            SEntMan.GetComponent<InputMoverComponent>(driver).HeldMoveButtons = MoveButtons.None);
        await Pair.RunTicksSync(120);
        await Server.WaitAssertion(() =>
        {
            var mover = SEntMan.GetComponent<GridVehicleMoverComponent>(vehicle);
            Assert.That(mover.IsMoving, Is.False);
            stopped = mover.Position;
            idleTick = mover.LastModifiedTick;
        });
        await Pair.RunTicksSync(15);
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<GridVehicleMoverComponent>(vehicle).LastModifiedTick, Is.EqualTo(idleTick)));
        await Client.WaitAssertion(() =>
        {
            var received = CEntMan.GetComponent<GridVehicleMoverComponent>(CEntMan.GetEntity(net));
            Assert.That(received.IsMoving, Is.False, "The final stop transition must reach the remote client.");
            Assert.That(received.Position, Is.EqualTo(stopped));
        });
    }
}
