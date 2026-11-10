using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared.CMU14.ZLevels;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Robust.Server.GameObjects;

namespace Content.IntegrationTests.CMU14.ZLevels;

[TestFixture]
public sealed class CMUZProbeOwnershipTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: CMUTestProbeNativeViewer
          components:
          - type: Eye
          - type: CMUZLevelViewer
        """;

    private readonly List<EntityUid> _cleanup = new();
    private EntityUid _camera;
    private EntityUid _lower;
    private EntityUid? _originalAttached;
    private bool _originalEnabled;

    // Includes the normal 4 Hz reconciliation interval, not just immediate event-driven refreshes.
    private const int RefreshTicks = 20;

    [Test]
    public async Task SameMapReparentKeepsProbesButParentMapTransferRetargetsThem()
    {
        try
        {
            await CreateScenario();
            await SubscribeCamera();
            EntityUid[] originalEyes = [];
            var parent = EntityUid.Invalid;
            var upper = EntityUid.Invalid;
            await Server.WaitAssertion(() =>
            {
                var eyes = AssertLiveProbes().Eyes;
                originalEyes = eyes.ToArray();
                upper = SComp<TransformComponent>(_camera).MapUid!.Value;
                parent = SEntMan.SpawnEntity(null, new EntityCoordinates(upper, Vector2.Zero));
                Server.System<SharedTransformSystem>().SetParent(_camera, parent);
            });
            await Pair.RunTicksSync(RefreshTicks);
            await Server.WaitAssertion(() =>
            {
                Assert.That(AssertLiveProbes().Eyes, Is.EquivalentTo(originalEyes),
                    "Changing parent on the same map must not discard valid visibility subscriptions.");
                // Transfer the parent, so the viewer receives the recursive map-change event.
                Server.System<SharedTransformSystem>().SetParent(parent, _lower);
            });
            await Pair.RunTicksSync(RefreshTicks);
            await Server.WaitAssertion(() =>
            {
                AssertOldProbesRemoved(originalEyes);
                var eyes = SComp<CMUZLevelViewerComponent>(_camera).Eyes;
                Assert.That(eyes, Is.Not.Empty, "The open upper floor must remain visible from below.");
                foreach (var eye in eyes)
                {
                    Assert.That(SComp<TransformComponent>(eye).MapUid, Is.EqualTo(upper));
                    Assert.That(ServerSession!.ViewSubscriptions, Does.Contain(eye));
                }
            });
        }
        finally
        {
            await CleanupScenario();
        }
    }

    [Test]
    public async Task SubscriberComponentShutdownRemovesOwnedViewerAndProbeSubscriptions()
    {
        try
        {
            await CreateScenario();
            await SubscribeCamera();
            EntityUid[] oldEyes = [];
            await Server.WaitAssertion(() =>
            {
                var eyes = AssertLiveProbes().Eyes;
                oldEyes = eyes.ToArray();
                var subscriber = SEntMan.ComponentFactory.GetRegistration("ViewSubscriber");
                SEntMan.RemoveComponent(_camera, subscriber.Type);
                Assert.That(ServerSession!.ViewSubscriptions, Does.Not.Contain(_camera));
            });
            await Pair.RunTicksSync(RefreshTicks);
            await Server.WaitAssertion(() =>
            {
                Assert.That(SEntMan.HasComponent<CMUZLevelViewerComponent>(_camera), Is.False);
                AssertOldProbesRemoved(oldEyes);
            });
        }
        finally
        {
            await CleanupScenario();
        }
    }

    [Test]
    public async Task ActorDetachPreservesIndependentCameraSubscriptionAndNativeViewer()
    {
        try
        {
            await CreateScenario(nativeViewer: true);
            CMUZLevelViewerComponent native = default!;
            await Server.WaitAssertion(() =>
            {
                native = SComp<CMUZLevelViewerComponent>(_camera);
                Server.PlayerMan.SetAttachedEntity(ServerSession!, _camera);
                // This feed is independently subscribed by the same session. Its reason for retaining
                // the probe remains after the actor detaches, despite engine subscriber deduplication.
                Server.System<ViewSubscriberSystem>().AddViewSubscriber(_camera, ServerSession!);
            });
            await Pair.RunTicksSync(RefreshTicks);
            await Server.WaitAssertion(() =>
            {
                AssertLiveProbes();
                Server.PlayerMan.SetAttachedEntity(ServerSession!, _originalAttached);
            });
            await Pair.RunTicksSync(RefreshTicks);
            EntityUid[] oldEyes = [];
            await Server.WaitAssertion(() =>
            {
                var viewer = AssertLiveProbes();
                Assert.That(viewer, Is.SameAs(native));
                var eyes = viewer.Eyes;
                oldEyes = eyes.ToArray();
                Server.System<ViewSubscriberSystem>().RemoveViewSubscriber(_camera, ServerSession!);
            });
            await Pair.RunTicksSync(RefreshTicks);
            await Server.WaitAssertion(() =>
            {
                Assert.That(SComp<CMUZLevelViewerComponent>(_camera), Is.SameAs(native));
                Assert.That(native.Eyes, Is.Empty);
                AssertOldProbesRemoved(oldEyes);
            });
        }
        finally
        {
            await CleanupScenario();
        }
    }

    private async Task CreateScenario(bool nativeViewer = false)
    {
        await Server.WaitAssertion(() =>
        {
            _originalAttached = ServerSession!.AttachedEntity;
            _originalEnabled = Server.CfgMan.GetCVar(CMUZLevelsCVars.Enabled);
            Server.CfgMan.SetCVar(CMUZLevelsCVars.Enabled, true);
            var maps = Server.System<SharedMapSystem>();
            _lower = maps.CreateMap(runMapInit: true);
            _cleanup.Add(_lower);
            var upper = maps.CreateMap(runMapInit: true);
            _cleanup.Add(upper);
            var zLevels = Server.System<CMUZLevelsSystem>();
            var network = zLevels.CreateZNetwork();
            _cleanup.Add(network.Owner);
            Assert.That(zLevels.TryAddMapsIntoZNetwork(network, new() { [_lower] = 0, [upper] = 1 }), Is.True);
            _camera = SEntMan.SpawnEntity(nativeViewer ? "CMUTestProbeNativeViewer" : null, new EntityCoordinates(upper, Vector2.Zero));
            _cleanup.Add(_camera);
            SEntMan.EnsureComponent<EyeComponent>(_camera);
        });
    }

    private async Task SubscribeCamera()
    {
        await Server.WaitAssertion(() => Server.System<ViewSubscriberSystem>().AddViewSubscriber(_camera, ServerSession!));
        await Pair.RunTicksSync(RefreshTicks);
    }

    private CMUZLevelViewerComponent AssertLiveProbes()
    {
        var viewer = SComp<CMUZLevelViewerComponent>(_camera);
        Assert.That(viewer.Eyes, Is.Not.Empty);
        foreach (var eye in viewer.Eyes)
        {
            Assert.That(SComp<TransformComponent>(eye).MapUid, Is.EqualTo(_lower));
            Assert.That(ServerSession!.ViewSubscriptions, Does.Contain(eye));
        }
        return viewer;
    }

    private void AssertOldProbesRemoved(IEnumerable<EntityUid> eyes)
    {
        foreach (var eye in eyes)
        {
            Assert.That(SEntMan.Deleted(eye), Is.True);
            Assert.That(ServerSession!.ViewSubscriptions, Does.Not.Contain(eye));
        }
    }

    private async Task CleanupScenario()
    {
        await Server.WaitPost(() =>
        {
            if (ServerSession!.AttachedEntity == _camera)
                Server.PlayerMan.SetAttachedEntity(ServerSession, _originalAttached);
            if (SEntMan.EntityExists(_camera))
                Server.System<ViewSubscriberSystem>().RemoveViewSubscriber(_camera, ServerSession);
            Server.CfgMan.SetCVar(CMUZLevelsCVars.Enabled, _originalEnabled);
        });
        for (var i = _cleanup.Count - 1; i >= 0; i--)
            await Pair.DeleteEntityTreeLeafFirst(_cleanup[i]);
    }
}
