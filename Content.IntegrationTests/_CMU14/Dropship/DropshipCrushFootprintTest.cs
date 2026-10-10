using System.Numerics;
using System.Reflection;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Shuttles.Systems;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class DropshipCrushFootprintTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [TestCase(0)]
    [TestCase(90)]
    public async Task LandingCrushSparesPeopleWhoseCentersAreOutsideTheHull(int degrees)
    {
        var map = await Pair.CreateTestMap();
        EntityUid ship = default;
        EntityUid outside = default;
        EntityUid inside = default;
        var rotation = Angle.FromDegrees(degrees);
        var origin = new Vector2(30, 20);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<MapLoaderSystem>().TryLoadMap(
                new ResPath("/Maps/CMU14/Shuttles/alamo.yml"), out _, out var loaded,
                DeserializationOptions.Default with { InitializeMaps = true }), Is.True);
            ship = loaded!.Single().Owner;
            var transforms = Server.System<SharedTransformSystem>();
            transforms.SetCoordinates(ship, new EntityCoordinates(map.MapUid, new Vector2(-30, -20)));
            transforms.SetWorldRotation(ship, rotation);
            // A person just outside the starboard engine tile still overlaps its collision fixture.
            outside = SEntMan.SpawnEntity("CMMobHuman", new MapCoordinates(
                origin + rotation.RotateVec(new Vector2(6.1f, -4.5f)), map.MapId));
            inside = SEntMan.SpawnEntity("CMMobHuman", new MapCoordinates(
                origin + rotation.RotateVec(new Vector2(5.5f, -4.5f)), map.MapId));
        });
        await Pair.RunTicksSync(1);
        await Server.WaitAssertion(() =>
        {
            Server.System<SharedTransformSystem>().SetCoordinates(ship, new EntityCoordinates(map.MapUid, origin));
            typeof(ShuttleSystem).GetMethod("Smimsh", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(Server.System<ShuttleSystem>(), new object[] { ship, null, null, null });
            Assert.That(SEntMan.IsQueuedForDeletion(outside), Is.False,
                "Touching the outer edge must not crush someone outside the actual shuttle tile.");
            Assert.That(SEntMan.IsQueuedForDeletion(inside), Is.True,
                "The person standing under the engine must still be crushed.");
        });
    }
}
