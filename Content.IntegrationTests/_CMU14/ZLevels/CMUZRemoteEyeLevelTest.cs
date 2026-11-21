using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared.CMU14.ZLevels;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.CMU14.ZLevels.Ghost;

namespace Content.IntegrationTests.CMU14.ZLevels;

/// <summary>
/// COUPLING-01: the "ghost" Z mover is the generic Z up/down mover, and works on a plain remote camera eye.
/// </summary>
[TestFixture]
[TestOf(typeof(CMUZLevelGhostMoverComponent))]
public sealed class CMUZRemoteEyeLevelTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: CMUTestRemoteEyeLevelMover
          components:
          - type: Eye
          - type: CMUZLevelGhostMover
        """;

    // Includes the normal 4 Hz reconciliation interval, not just immediate event-driven refreshes.
    private const int RefreshTicks = 20;

    private readonly List<EntityUid> _cleanup = new();
    private EntityUid _lower;
    private EntityUid _upper;
    private EntityUid _body;
    private EntityUid _eye;
    private EntityUid? _originalAttached;
    private bool _originalEnabled;

    [Test]
    public async Task TargetedEyeMovesUpAndOwnsItsOwnProbes()
    {
        var eyeStart = new Vector2(2.5f, 3.5f);
        EntityUid[] bodyProbes = [];
        EntityUid[] eyeProbesBefore = [];
        EntityUid[] eyeProbesAfter = [];

        await Server.WaitAssertion(() =>
        {
            _originalAttached = ServerSession!.AttachedEntity;
            _originalEnabled = Server.CfgMan.GetCVar(CMUZLevelsCVars.Enabled);
            Server.CfgMan.SetCVar(CMUZLevelsCVars.Enabled, true);

            var maps = Server.System<SharedMapSystem>();
            _lower = maps.CreateMap(runMapInit: true);
            _cleanup.Add(_lower);
            _upper = maps.CreateMap(runMapInit: true);
            _cleanup.Add(_upper);
            var zLevels = Server.System<CMUZLevelsSystem>();
            var network = zLevels.CreateZNetwork();
            _cleanup.Add(network.Owner);
            Assert.That(zLevels.TryAddMapsIntoZNetwork(network, new() { [_lower] = 0, [_upper] = 1 }), Is.True);

            // The body watches from the upper level, so it owns its own probe of the lower one.
            _body = SEntMan.SpawnEntity(null, new EntityCoordinates(_upper, new Vector2(-6.5f, -6.5f)));
            _cleanup.Add(_body);
            SEntMan.EnsureComponent<EyeComponent>(_body);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, _body);

            _eye = SEntMan.SpawnEntity("CMUTestRemoteEyeLevelMover", new EntityCoordinates(_lower, eyeStart));
            _cleanup.Add(_eye);
            Assert.That(SEntMan.HasComponent<CMUZPhysicsComponent>(_eye), Is.False, "The remote eye is not a z-physics body.");
        });
        await Pair.RunTicksSync(RefreshTicks);

        await Server.WaitAssertion(() =>
        {
            bodyProbes = AssertLiveProbes(_body, _lower).ToArray();
            Server.System<SharedEyeSystem>().SetTarget(_body, _eye);
            Assert.That(ServerSession!.ViewSubscriptions, Does.Contain(_eye));
        });
        await Pair.RunTicksSync(RefreshTicks);

        await Server.WaitAssertion(() =>
        {
            var eyes = SComp<CMUZLevelViewerComponent>(_eye).Eyes;
            eyeProbesBefore = eyes.ToArray();

            // The action the mover grants, not only the movement API behind it.
            var up = new CMUZLevelActionUp();
            SEntMan.EventBus.RaiseLocalEvent(_eye, up);
            Assert.That(up.Handled, Is.True, "The Z up action must move a non-ghost eye.");

            var xform = SComp<TransformComponent>(_eye);
            Assert.That(xform.MapUid, Is.EqualTo(_upper));
            Assert.That(Server.System<SharedTransformSystem>().GetWorldPosition(xform), Is.EqualTo(eyeStart),
                "Moving between levels keeps the same X/Y.");
        });
        await Pair.RunTicksSync(RefreshTicks);

        await Server.WaitAssertion(() =>
        {
            AssertProbesRemoved(eyeProbesBefore);
            eyeProbesAfter = AssertLiveProbes(_eye, _lower).ToArray();
            Assert.That(eyeProbesAfter.Intersect(bodyProbes), Is.Empty, "The eye spends its own probes.");
            Assert.That(AssertLiveProbes(_body, _lower), Is.EquivalentTo(bodyProbes),
                "Moving the targeted eye must not touch the body's probes.");

            Server.System<SharedEyeSystem>().SetTarget(_body, null);
            Assert.That(ServerSession!.ViewSubscriptions, Does.Not.Contain(_eye));
        });
        await Pair.RunTicksSync(RefreshTicks);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<CMUZLevelViewerComponent>(_eye), Is.False,
                "Clearing the target releases the eye's viewer.");
            AssertProbesRemoved(eyeProbesAfter);
            Assert.That(AssertLiveProbes(_body, _lower), Is.EquivalentTo(bodyProbes));
        });
    }

    private List<EntityUid> AssertLiveProbes(EntityUid viewer, EntityUid probeMap)
    {
        var eyes = SComp<CMUZLevelViewerComponent>(viewer).Eyes;
        Assert.That(eyes, Is.Not.Empty);
        foreach (var eye in eyes)
        {
            Assert.That(SComp<TransformComponent>(eye).MapUid, Is.EqualTo(probeMap));
            Assert.That(ServerSession!.ViewSubscriptions, Does.Contain(eye));
        }

        return eyes;
    }

    private void AssertProbesRemoved(IEnumerable<EntityUid> eyes)
    {
        foreach (var eye in eyes)
        {
            Assert.That(SEntMan.Deleted(eye), Is.True);
            Assert.That(ServerSession!.ViewSubscriptions, Does.Not.Contain(eye));
        }
    }

    [TearDown]
    public async Task CleanupScenario()
    {
        await Server.WaitPost(() =>
        {
            if (SEntMan.TryGetComponent(_body, out EyeComponent? bodyEye) && bodyEye.Target != null)
                Server.System<SharedEyeSystem>().SetTarget(_body, null, bodyEye);
            if (ServerSession!.AttachedEntity == _body)
                Server.PlayerMan.SetAttachedEntity(ServerSession!, _originalAttached);
            Server.CfgMan.SetCVar(CMUZLevelsCVars.Enabled, _originalEnabled);
        });
        for (var i = _cleanup.Count - 1; i >= 0; i--)
            await Pair.DeleteEntityTreeLeafFirst(_cleanup[i]);
        _cleanup.Clear();
    }
}
