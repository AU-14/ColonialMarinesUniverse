using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.Gravity;
using Content.Shared.Alert;
using Content.Shared.Friction;
using Content.Shared.Gravity;
using Content.Shared.Movement.Components;
using Robust.Client.Timing;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.Tests.Gravity;

[TestFixture]
[TestOf(typeof(GravitySystem))]
[TestOf(typeof(GravityAffectedComponent))]
[TestOf(typeof(TileFrictionController))]
public sealed class GravityMergeRegressionTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: GravityMergeSubject
  components:
  - type: Alerts
  - type: Physics
    bodyType: Dynamic
  - type: GravityAffected
  - type: GravityMergeProbe

- type: entity
  id: GravityMergeInherent
  components:
  - type: Gravity
    enabled: true
    inherent: true

- type: entity
  id: GravityMergeAirborne
  components:
  - type: Physics
    bodyType: Dynamic
  - type: Fixtures
    fixtures:
      body:
        shape: !type:PhysShapeCircle
          radius: 0.25
        hard: false
  - type: MovementIgnoreGravity
    weightless: false

- type: entity
  id: GravityMergeAirMover
  parent: GravityMergeAirborne
  components:
  - type: CanMoveInAir
";

    [Test]
    public async Task CachedGridAndOverrideStateTracksParentBodyAndAlerts()
    {
        var map = await Pair.CreateTestMap();

        EntityUid subject = default;
        GravityAffectedComponent affected = default!;
        GravityMergeProbeComponent probe = default!;

        await Server.WaitAssertion(() =>
        {
            _ = Server.System<GravityMergeProbeSystem>();
            var gravity = Server.System<GravitySystem>();

            gravity.EnableGravity(map.Grid.Owner);
            subject = SEntMan.SpawnEntity("GravityMergeSubject", map.GridCoords);
            affected = SEntMan.GetComponent<GravityAffectedComponent>(subject);
            probe = SEntMan.GetComponent<GravityMergeProbeComponent>(subject);

            var inherentUid = SEntMan.SpawnEntity("GravityMergeInherent", MapCoordinates.Nullspace);
            var inherent = SEntMan.GetComponent<GravityComponent>(inherentUid);
            Assert.Multiple(() =>
            {
                Assert.That(inherent.Enabled, Is.True);
                Assert.That(inherent.Inherent, Is.True);
            });
        });

        await Server.WaitRunTicks(1);

        await Server.WaitAssertion(() =>
        {
            var gravity = Server.System<GravitySystem>();
            var alerts = Server.System<AlertsSystem>();
            var transform = Server.System<SharedTransformSystem>();
            var physics = Server.System<SharedPhysicsSystem>();
            var body = SEntMan.GetComponent<PhysicsComponent>(subject);

            AssertState(gravity, alerts, subject, affected, weightless: false, fromGrid: true);
            probe.Reset();

            // Reparenting off the gravity-enabled grid must recompute the cached grid-derived state.
            transform.SetMapCoordinates(subject, new MapCoordinates(new Vector2(1000, 1000), map.MapId));
            AssertState(gravity, alerts, subject, affected, weightless: true, fromGrid: true);
            Assert.That(probe.Changes, Is.EqualTo(new[] { true }));

            transform.SetCoordinates(subject, map.GridCoords);
            AssertState(gravity, alerts, subject, affected, weightless: false, fromGrid: true);
            Assert.That(probe.Changes, Is.EqualTo(new[] { true, false }));

            // An entity-local override is authoritative and marks the cached value as non-grid-derived.
            var localOverride = SEntMan.EnsureComponent<GravityMergeOverrideComponent>(subject);
            localOverride.Weightless = true;
            gravity.RefreshWeightless((subject, affected));
            AssertState(gravity, alerts, subject, affected, weightless: true, fromGrid: false);

            localOverride.Weightless = false;
            gravity.RefreshWeightless((subject, affected));
            AssertState(gravity, alerts, subject, affected, weightless: false, fromGrid: false);

            // Removing the override changes only the source flag. That still networks and raises the event.
            var beforeSourceOnlyChange = probe.Changes.Count;
            SEntMan.RemoveComponent<GravityMergeOverrideComponent>(subject);
            gravity.RefreshWeightless((subject, affected));
            AssertState(gravity, alerts, subject, affected, weightless: false, fromGrid: true);
            Assert.That(probe.Changes.Count, Is.EqualTo(beforeSourceOnlyChange + 1));
            Assert.That(probe.Changes[^1], Is.False);

            // With no generator, RefreshGravity disables the grid and updates every affected child.
            gravity.RefreshGravity(map.Grid.Owner);
            AssertState(gravity, alerts, subject, affected, weightless: true, fromGrid: true);

            // Static bodies cannot be weightless; restoring Dynamic recomputes the grid state.
            physics.SetBodyType(subject, BodyType.Static, body: body);
            AssertState(gravity, alerts, subject, affected, weightless: false, fromGrid: false);

            physics.SetBodyType(subject, BodyType.Dynamic, body: body);
            AssertState(gravity, alerts, subject, affected, weightless: true, fromGrid: true);
        });
    }

    [Test]
    public async Task AirFrictionRunsOnlyOnFirstPredictionUnlessAirMovementUsesTheTile()
    {
        var map = await Pair.CreateTestMap();
        EntityUid airborne = default;
        EntityUid airMover = default;
        NetEntity airborneNet = default;
        NetEntity airMoverNet = default;

        await Server.WaitAssertion(() =>
        {
            airborne = SEntMan.SpawnEntity("GravityMergeAirborne", map.GridCoords);
            airMover = SEntMan.SpawnEntity("GravityMergeAirMover", map.GridCoords);
            airborneNet = SEntMan.GetNetEntity(airborne);
            airMoverNet = SEntMan.GetNetEntity(airMover);
        });
        await Pair.RunUntilSynced();

        await Client.WaitAssertion(() =>
        {
            var controller = Client.System<TileFrictionController>();
            var physics = Client.System<SharedPhysicsSystem>();
            Assert.That(CEntMan.TryGetEntity(airborneNet, out var clientAirborne), Is.True);
            Assert.That(CEntMan.TryGetEntity(airMoverNet, out var clientAirMover), Is.True);
            var clientAirborneUid = clientAirborne!.Value;
            var clientAirMoverUid = clientAirMover!.Value;
            var airborneBody = CEntMan.GetComponent<PhysicsComponent>(clientAirborneUid);
            var airMoverBody = CEntMan.GetComponent<PhysicsComponent>(clientAirMoverUid);

            PrepareAirborne(physics, clientAirborneUid, airborneBody);
            PrepareAirborne(physics, clientAirMoverUid, airMoverBody);
            controller.UpdateBeforeSolve(prediction: false, frameTime: 0.1f);

            var firstAirDamping = airborneBody.LinearDamping;
            var firstMoverDamping = airMoverBody.LinearDamping;

            PrepareAirborne(physics, clientAirborneUid, airborneBody);
            PrepareAirborne(physics, clientAirMoverUid, airMoverBody);
            CGameTiming.StartPastPrediction();
            try
            {
                controller.UpdateBeforeSolve(prediction: false, frameTime: 0.1f);
            }
            finally
            {
                CGameTiming.EndPastPrediction();
            }

            Assert.Multiple(() =>
            {
                Assert.That(firstAirDamping, Is.GreaterThan(airborneBody.LinearDamping),
                    "air damping must be omitted while replaying a past prediction");
                Assert.That(firstMoverDamping, Is.EqualTo(airMoverBody.LinearDamping),
                    "CanMoveInAir uses tile friction, which is not restricted to first prediction");
                Assert.That(firstMoverDamping, Is.GreaterThan(firstAirDamping),
                    "the air mover must take the tile-friction path rather than air damping");
            });
        });
    }

    private static void AssertState(
        SharedGravitySystem gravity,
        AlertsSystem alerts,
        EntityUid subject,
        GravityAffectedComponent affected,
        bool weightless,
        bool fromGrid)
    {
        Assert.Multiple(() =>
        {
            Assert.That(affected.Weightless, Is.EqualTo(weightless));
            Assert.That(affected.GridWeightlessStatus, Is.EqualTo(fromGrid));
            Assert.That(gravity.IsWeightless((subject, affected)), Is.EqualTo(weightless));
            Assert.That(gravity.IsWeightlessStatusFromGrid((subject, affected)), Is.EqualTo(fromGrid));
            Assert.That(alerts.IsShowingAlert(subject, SharedGravitySystem.WeightlessAlert), Is.EqualTo(weightless));
        });
    }

    private static void PrepareAirborne(
        SharedPhysicsSystem physics,
        EntityUid uid,
        PhysicsComponent body)
    {
        physics.SetBodyStatus(uid, body, BodyStatus.InAir);
        physics.WakeBody(uid, body: body);
        physics.SetLinearVelocity(uid, Vector2.One, wakeBody: false, body: body);
    }
}

[RegisterComponent]
public sealed partial class GravityMergeProbeComponent : Component
{
    public readonly List<bool> Changes = new();

    public void Reset()
    {
        Changes.Clear();
    }
}

[RegisterComponent]
public sealed partial class GravityMergeOverrideComponent : Component
{
    public bool Weightless;
}

public sealed class GravityMergeProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GravityMergeProbeComponent, WeightlessnessChangedEvent>(OnChanged);
        SubscribeLocalEvent<GravityMergeOverrideComponent, IsWeightlessEvent>(OnOverride);
    }

    private static void OnChanged(
        Entity<GravityMergeProbeComponent> ent,
        ref WeightlessnessChangedEvent args)
    {
        ent.Comp.Changes.Add(args.Weightless);
    }

    private static void OnOverride(
        Entity<GravityMergeOverrideComponent> ent,
        ref IsWeightlessEvent args)
    {
        args.IsWeightless = ent.Comp.Weightless;
        args.Handled = true;
    }
}
