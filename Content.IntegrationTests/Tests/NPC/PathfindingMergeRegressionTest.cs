using System.Linq;
using System.Reflection;
using Content.Client.NPC;
using Content.IntegrationTests.Fixtures;
using Content.Shared.NPC;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using ClientPathfindingSystem = Content.Client.NPC.PathfindingSystem;

namespace Content.IntegrationTests.Tests.NPC;

[TestFixture]
[TestOf(typeof(ClientPathfindingSystem))]
public sealed class PathfindingMergeRegressionTest : GameTest
{
    [Test]
    public async Task ModesRouteExpiryAndQuietShutdownCleanupRemainConcordant()
    {
        var probe = Server.System<PathfindingDebugRequestProbeSystem>();
        await Server.WaitPost(probe.Reset);

        MethodInfo quietSetModes = null!;
        await Client.WaitAssertion(() =>
        {
            var pathfinding = Client.System<ClientPathfindingSystem>();
            quietSetModes = typeof(ClientPathfindingSystem).GetMethod(
                "SetModes",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            Assert.That(quietSetModes, Is.Not.Null,
                "Shutdown must share the ordinary mode-cleanup implementation without sending a network event");

            quietSetModes.Invoke(pathfinding, new object[] { PathfindingDebugMode.None, false });
            pathfinding.Modes = PathfindingDebugMode.Breadcrumbs | PathfindingDebugMode.Steering;
        });
        await Pair.RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(probe.Requests, Is.EqualTo(1));
                Assert.That(probe.LastMode,
                    Is.EqualTo(PathfindingDebugMode.Breadcrumbs | PathfindingDebugMode.Steering));
            });
        });

        await Client.WaitAssertion(() =>
        {
            var timing = Client.ResolveDependency<IGameTiming>();
            var overlays = Client.ResolveDependency<IOverlayManager>();
            var pathfinding = Client.System<ClientPathfindingSystem>();
            var steering = Client.System<NPCSteeringSystem>();
            var expiredFirst = new PathRouteMessage([], []);
            var expiredSecond = new PathRouteMessage([], []);
            var live = new PathRouteMessage([], []);

            pathfinding.Breadcrumbs[NetEntity.Invalid] = [];
            pathfinding.Polys[NetEntity.Invalid] = [];
            pathfinding.Routes.Add((timing.RealTime - TimeSpan.FromSeconds(1), expiredFirst));
            pathfinding.Routes.Add((timing.RealTime - TimeSpan.FromSeconds(0.5), expiredSecond));
            pathfinding.Routes.Add((timing.RealTime + TimeSpan.FromSeconds(1), live));
            pathfinding.Update(0f);

            Assert.Multiple(() =>
            {
                Assert.That(pathfinding.Modes,
                    Is.EqualTo(PathfindingDebugMode.Breadcrumbs | PathfindingDebugMode.Steering));
                Assert.That(overlays.HasOverlay<PathfindingOverlay>(), Is.True);
                Assert.That(steering.DebugEnabled, Is.True);
                Assert.That(pathfinding.Routes, Has.Count.EqualTo(1),
                    "adjacent expired routes must both be removed in one update");
                Assert.That(pathfinding.Routes.Single().Message, Is.SameAs(live));
            });

            quietSetModes.Invoke(pathfinding, new object[] { PathfindingDebugMode.None, false });
            Assert.Multiple(() =>
            {
                Assert.That(pathfinding.Modes, Is.EqualTo(PathfindingDebugMode.None));
                Assert.That(pathfinding.Breadcrumbs, Is.Empty);
                Assert.That(pathfinding.Polys, Is.Empty);
                Assert.That(pathfinding.Routes, Is.Empty);
                Assert.That(overlays.HasOverlay<PathfindingOverlay>(), Is.False);
                Assert.That(steering.DebugEnabled, Is.False);
            });
        });
        await Pair.RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            Assert.That(probe.Requests, Is.EqualTo(1),
                "the quiet cleanup used by Shutdown must not send a second debug request");
        });
    }
}

public sealed class PathfindingDebugRequestProbeSystem : EntitySystem
{
    public int Requests;
    public PathfindingDebugMode LastMode;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<RequestPathfindingDebugMessage>(OnRequest);
    }

    public void Reset()
    {
        Requests = 0;
        LastMode = PathfindingDebugMode.None;
    }

    private void OnRequest(RequestPathfindingDebugMessage message, EntitySessionEventArgs args)
    {
        Requests++;
        LastMode = message.Mode;
    }
}
