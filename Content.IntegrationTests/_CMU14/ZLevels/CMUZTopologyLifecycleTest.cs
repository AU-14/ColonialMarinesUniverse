using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.CMU14.ZLevels.Ordnance;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.CMU14.ZLevels;

[TestFixture]
public sealed class CMUZTopologyLifecycleTest : GameTest
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task DeletingAndDetachingMapsReconcilesLiveMembership(bool deleteNetwork)
    {
        await Server.WaitAssertion(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            var z = SEntMan.System<CMUZLevelsSystem>();
            var lower = maps.CreateMap();
            var middle = maps.CreateMap();
            var upper = maps.CreateMap();
            var network = z.CreateZNetwork();
            try
            {
                Assert.That(z.TryAddMapsIntoZNetwork(network, new() { [lower] = -1, [middle] = 0, [upper] = 1 }), Is.True);
                SEntMan.DeleteEntity(middle);
                Assert.That(z.TryMapUp(lower, out _), Is.False);
                Assert.That(z.TryMapDown(upper, out _), Is.False);
                Assert.That(z.GetAllNetworkMaps(lower), Is.EqualTo(new[] { lower, upper }));
                Assert.That(SComp<CMUZLevelMapComponent>(lower).MapAbove, Is.Null);
                Assert.That(z.TryRemoveMapFromZNetwork(upper), Is.True);
                Assert.That(z.TryGetZNetwork(upper, out _), Is.False);
                Assert.That(z.TryAddMapsIntoZNetwork(network, new() { [upper] = 0 }), Is.True);
                Assert.That(z.TryMapUp(lower, out var target), Is.True);
                Assert.That(target.Value.Owner, Is.EqualTo(upper));
                if (deleteNetwork)
                    SEntMan.DeleteEntity(network);
                else
                    SEntMan.RemoveComponent<CMUZLevelsNetworkComponent>(network);
                Assert.That(z.TryGetZNetwork(lower, out _), Is.False);
                Assert.That(SEntMan.HasComponent<CMUZLevelMapComponent>(upper), Is.False);
            }
            finally
            {
                foreach (var uid in new[] { lower, middle, upper, network.Owner })
                    if (!SEntMan.Deleted(uid)) SEntMan.DeleteEntity(uid);
            }
        });
    }

    [Test]
    public async Task SparseExtremeDepthsAreBoundedAndNeverWrap()
    {
        await Server.WaitAssertion(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            var z = SEntMan.System<CMUZLevelsSystem>();
            var lower = maps.CreateMap();
            var upper = maps.CreateMap(out var upperId);
            var invalid = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var network = z.CreateZNetwork();
            try
            {
                Assert.That(z.TryAddMapsIntoZNetwork(network, new() { [invalid] = 0 }), Is.False);
                Assert.That(z.TryAddMapsIntoZNetwork(network, new() { [upper] = int.MaxValue }), Is.True);
                Assert.That(z.TryGetDepthBounds(network, out var min, out var max), Is.True);
                Assert.That(min, Is.EqualTo(int.MaxValue));
                Assert.That(max, Is.EqualTo(int.MaxValue));
                Assert.That(z.TryAddMapsIntoZNetwork(network, new() { [lower] = int.MinValue }), Is.True);
                Assert.That(z.GetAllNetworkMaps(upper), Is.EqualTo(new[] { upper, lower }));
                Assert.That(z.TryMapUp(upper, out _), Is.False);
                Assert.That(z.TryMapDown(lower, out _), Is.False);
                Assert.That(SEntMan.System<CMUTopDownOrdnanceSystem>().IsOpenToSky(new MapCoordinates(Vector2.Zero, upperId)), Is.True);
            }
            finally
            {
                foreach (var uid in new[] { lower, upper, invalid, network.Owner })
                    SEntMan.DeleteEntity(uid);
            }
        });
    }

    [Test]
    public async Task ExistingBodyStartsFallingWhenItsMapJoinsNetwork()
    {
        EntityUid lower = default, upper = default, network = default, body = default;
        await Server.WaitAssertion(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            var z = SEntMan.System<CMUZLevelsSystem>();
            lower = maps.CreateMap(runMapInit: true);
            upper = maps.CreateMap(runMapInit: true);
            SEntMan.EnsureComponent<MapGridComponent>(lower);
            SEntMan.EnsureComponent<MapGridComponent>(upper);
            body = SEntMan.SpawnEntity(null, new EntityCoordinates(upper, Vector2.Zero));
            var physics = SEntMan.AddComponent<PhysicsComponent>(body);
            SEntMan.System<SharedPhysicsSystem>().SetBodyType(body, BodyType.Dynamic, body: physics);
            SEntMan.AddComponent<CMUZPhysicsComponent>(body);
            Assert.That(SEntMan.HasComponent<CMUZFallingComponent>(body), Is.False);
            var net = z.CreateZNetwork();
            network = net;
            Assert.That(z.TryAddMapsIntoZNetwork(net, new() { [lower] = 0, [upper] = 1 }), Is.True);
            Assert.That(SEntMan.HasComponent<CMUZFallingComponent>(body), Is.True);
            // Use a short production update: the test server's one-second ticks can
            // carry a body through an entire layer before its velocity is observed.
            z.Update(0.05f);
            Assert.That(SComp<CMUZPhysicsComponent>(body).Velocity, Is.LessThan(0));
        });
        foreach (var uid in new[] { body, lower, upper, network })
            await Pair.DeleteEntityTreeLeafFirst(uid);
    }

    [Test]
    public async Task RemoteProbeMovesToNewDepthAndUnsubscribesOutsideNetwork()
    {
        EntityUid lower = default, middle = default, upper = default, outside = default, network = default, camera = default;
        await Server.WaitAssertion(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            lower = maps.CreateMap(runMapInit: true);
            middle = maps.CreateMap(runMapInit: true);
            upper = maps.CreateMap(runMapInit: true);
            outside = maps.CreateMap(runMapInit: true);
            var z = SEntMan.System<CMUZLevelsSystem>();
            var net = z.CreateZNetwork();
            network = net;
            Assert.That(z.TryAddMapsIntoZNetwork(net, new() { [lower] = 0, [middle] = 1, [upper] = 2 }), Is.True);
            camera = SEntMan.SpawnEntity(null, new EntityCoordinates(middle, Vector2.Zero));
            SEntMan.EnsureComponent<EyeComponent>(camera);
            SEntMan.System<ViewSubscriberSystem>().AddViewSubscriber(camera, ServerSession!);
        });
        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() =>
        {
            var viewer = SComp<CMUZLevelViewerComponent>(camera);
            Assert.That(viewer.Eyes.Select(e => SComp<TransformComponent>(e).MapUid), Does.Contain(lower));
            SEntMan.System<SharedTransformSystem>().SetCoordinates(camera, new EntityCoordinates(upper, Vector2.One));
        });
        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() =>
        {
            var viewer = SComp<CMUZLevelViewerComponent>(camera);
            Assert.That(viewer.Eyes.Select(e => SComp<TransformComponent>(e).MapUid),
                Is.EquivalentTo(new[] { middle, lower }));
            foreach (var eye in viewer.Eyes)
            {
                Assert.That(ServerSession!.ViewSubscriptions, Does.Contain(eye));
            }
            SEntMan.System<SharedTransformSystem>().SetCoordinates(camera, new EntityCoordinates(outside, Vector2.Zero));
            SEntMan.System<ViewSubscriberSystem>().RemoveViewSubscriber(camera, ServerSession!);
        });
        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() => Assert.That(SEntMan.HasComponent<CMUZLevelViewerComponent>(camera), Is.False));
        foreach (var uid in new[] { camera, lower, middle, upper, outside, network })
            await Pair.DeleteEntityTreeLeafFirst(uid);
    }
}
