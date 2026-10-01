using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Vehicle;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.CMU14;

[TestFixture]
public sealed class CMUVehiclePushDirectionTest : GameTest
{
    [TestCase(0, false)]
    [TestCase(1, false)]
    [TestCase(0, true)]
    public async Task FrontContactFollowsMovementAndDoesNotShuntAroundWalls(int vertical, bool wall)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var vehicle = SEntMan.SpawnEntity("VehicleHumvee", map.GridCoords);
            var mob = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1.9f, 0)));
            if (wall)
                SEntMan.SpawnEntity("CMWallMetal", map.GridCoords.Offset(new Vector2(2, 0)));
            var transform = SEntMan.System<SharedTransformSystem>();
            var center = transform.GetWorldPosition(vehicle);
            var mobCenter = transform.GetWorldPosition(mob);
            var vehicleBounds = Box2.CenteredAround(center, new Vector2(4, 4));
            var mobBounds = Box2.CenteredAround(mobCenter, new Vector2(0.6f, 0.6f));
            var movement = new Vector2(0.1f, vertical * 0.1f);
            var method = typeof(GridVehicleMoverSystem).GetMethod("TryGetMobPush", BindingFlags.Instance | BindingFlags.NonPublic)!;
            object[] args = [vehicle, mob, vehicleBounds, mobBounds, movement, EntityCoordinates.Invalid];
            var pushed = (bool) method.Invoke(SEntMan.System<GridVehicleMoverSystem>(), args)!;
            Assert.That(pushed, Is.EqualTo(!wall));
            if (!pushed)
                return;
            var displacement = transform.ToMapCoordinates((EntityCoordinates) args[5]).Position - mobCenter;
            Assert.That(displacement.X, Is.GreaterThan(0));
            Assert.That(displacement.Y, Is.EqualTo(displacement.X * vertical).Within(0.01f));
            Assert.That(displacement.Length(), Is.LessThan(1), "A shallow front contact must not teleport across the hull.");
        });
    }
}
