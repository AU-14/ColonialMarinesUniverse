#pragma warning disable RA0002 // Supply held driver input at the movement-system boundary.
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.Ship;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Vehicle.Components;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.CMU14.Vehicle;

[TestFixture]
public sealed class CMUVehicleSpaceTractionTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    [Test]
    public async Task HumveeNeedsFloorToAccelerate()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid map = default;
        EntityUid vehicle = default;
        await Server.WaitAssertion(() =>
        {
            map = Server.System<SharedMapSystem>().CreateMap(runMapInit: true);
            SEntMan.EnsureComponent<MapGridComponent>(map);
            SEntMan.EnsureComponent<CMUZeroGSpaceComponent>(map);
            var driver = SEntMan.SpawnEntity(null, testMap.GridCoords);
            SEntMan.EnsureComponent<HandsComponent>(driver);
            SEntMan.EnsureComponent<EyeComponent>(driver);
            var input = SEntMan.EnsureComponent<InputMoverComponent>(driver);
            vehicle = SEntMan.SpawnEntity("VehicleHumvee", new EntityCoordinates(map, new Vector2(0.5f, 0.5f)));
            var slots = Server.System<ItemSlotsSystem>();
            if (slots.GetItemOrNull(vehicle, "wheel-1") == null)
            {
                var wheels = SEntMan.SpawnEntity("VehicleHumveeWheel", testMap.GridCoords);
                Assert.That(slots.TryInsert(vehicle, "wheel-1", wheels, null), Is.True);
            }
            var vehicleComp = SEntMan.GetComponent<VehicleComponent>(vehicle);
            Assert.That(Server.System<VehicleSystem>().TrySetOperator((vehicle, vehicleComp), driver), Is.True);
            input.HeldMoveButtons = MoveButtons.Up | MoveButtons.Left;
            var canRun = new VehicleCanRunEvent((vehicle, vehicleComp));
            SEntMan.EventBus.RaiseLocalEvent(vehicle, ref canRun);
            Assert.That(canRun.CanRun, Is.True, "The vehicle must be operational before testing traction.");
            Assert.That(SEntMan.GetComponent<TransformComponent>(vehicle).GridUid, Is.EqualTo(map));
        });
        await RunTicksSync(2);
        await Server.WaitAssertion(() =>
        {
            var mover = SEntMan.GetComponent<GridVehicleMoverComponent>(vehicle);
            Assert.That(mover.Position, Is.EqualTo(new Vector2(0.5f, 0.5f)), "Driver input cannot propel a wheeled vehicle through space.");
            Assert.That(mover.CurrentSpeed, Is.Zero);
            var maps = Server.System<SharedMapSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(map);
            for (var x = -6; x <= 6; x++)
            for (var y = -6; y <= 6; y++)
                maps.SetTile(map, grid, new Vector2i(x, y), testMap.Tile.Tile);
        });
        await RunTicksSync(1);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<GridVehicleMoverComponent>(vehicle).CurrentSpeed, Is.GreaterThan(0),
                $"The same vehicle and input must accelerate on floor: {SEntMan.GetComponent<GridVehicleMoverComponent>(vehicle).Position}.");
            SEntMan.DeleteEntity(map);
        });
    }
}
