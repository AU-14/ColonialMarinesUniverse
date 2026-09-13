using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.Chemistry.Components;
using Content.Server.Fluids.EntitySystems;
using Content.Shared._RMC14.Chemistry;
using Content.Shared._RMC14.Throwing;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Fluids;
using Content.Shared.Fluids.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Throwing;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Fluids;

[TestFixture]
[TestOf(typeof(SpraySystem))]
[TestOf(typeof(ThrownItemSystem))]
public sealed class SprayThrowingMergeRegressionTest : GameTest
{
    private static readonly ProtoId<ReagentPrototype> Reagent = "SprayThrowingMergeReagent";

    [TestPrototypes]
    private const string Prototypes = """
- type: reagent
  id: SprayThrowingMergeReagent
  name: reagent-name-nothing
  desc: reagent-desc-nothing
  physicalDesc: reagent-physical-desc-nothing
""";

    [Test]
    public async Task CancelledSprayPreservesSolutionAndHitUserControlsThrowerCollision()
    {
        var map = await Pair.CreateTestMap();
        EntityUid target = default;
        EntityUid ordinaryVapor = default;
        EntityUid hitUserVapor = default;

        await Server.WaitAssertion(() =>
        {
            _ = Server.System<SprayThrowingMergeProbeSystem>();
            var entities = Server.EntMan;
            var sprays = Server.System<SpraySystem>();
            var solutions = Server.System<SharedSolutionContainerSystem>();

            target = entities.SpawnEntity("XenoAcidNormal", map.GridCoords);
            entities.EnsureComponent<SprayThrowingMergeProbeComponent>(target);

            var cancelledSpray = SpawnSpray(solutions, map.GridCoords);
            var cancelledProbe = entities.EnsureComponent<SprayAttemptMergeProbeComponent>(cancelledSpray.Spray.Owner);
            cancelledProbe.Cancel = true;
            var targetCoordinates = TargetCoordinates(cancelledSpray.Spray.Owner);

            sprays.Spray(cancelledSpray.Spray, target, targetCoordinates, hitUser: true);
            Assert.Multiple(() =>
            {
                Assert.That(cancelledProbe.Attempts, Is.EqualTo(1));
                Assert.That(cancelledSpray.Solution.Comp.Solution.Volume, Is.EqualTo(FixedPoint2.New(10)),
                    "SprayAttempt cancellation must preserve the source solution.");
                Assert.That(entities.EntityQuery<VaporComponent>(), Is.Empty,
                    "SprayAttempt cancellation must happen before vapor spawning.");
            });

            var ordinarySpray = SpawnSpray(solutions, map.GridCoords);
            sprays.Spray(ordinarySpray.Spray, targetCoordinates, target);
            ordinaryVapor = SingleVaporExcept();
            Assert.That(entities.HasComponent<ThrownHitUserComponent>(ordinaryVapor), Is.False);

            var ordinaryPrevent = MakePreventEvent(ordinaryVapor, target);
            entities.EventBus.RaiseLocalEvent(ordinaryVapor, ref ordinaryPrevent);
            Assert.That(ordinaryPrevent.Cancelled, Is.True,
                "An ordinary thrown vapor must retain upstream thrower-collision suppression.");
        });

        await Server.WaitRunTicks(3);
        await Server.WaitAssertion(() =>
        {
            var probe = Server.EntMan.GetComponent<SprayThrowingMergeProbeComponent>(target);
            Assert.Multiple(() =>
            {
                Assert.That(probe.TouchReactions, Is.Zero);
                Assert.That(probe.VaporHits, Is.Zero);
            });
            Server.EntMan.DeleteEntity(ordinaryVapor);
        });

        await Server.WaitRunTicks(2);
        await Server.WaitAssertion(() =>
        {
            var entities = Server.EntMan;
            var sprays = Server.System<SpraySystem>();
            var solutions = Server.System<SharedSolutionContainerSystem>();
            var hitUserSpray = SpawnSpray(solutions, map.GridCoords);
            var targetCoordinates = TargetCoordinates(hitUserSpray.Spray.Owner);

            sprays.Spray(hitUserSpray.Spray, target, targetCoordinates, hitUser: true);
            hitUserVapor = SingleVaporExcept();
            Assert.That(entities.HasComponent<ThrownHitUserComponent>(hitUserVapor), Is.True,
                "The RMC hit-user spray route must mark its vapor explicitly.");

            var hitUserPrevent = MakePreventEvent(hitUserVapor, target);
            entities.EventBus.RaiseLocalEvent(hitUserVapor, ref hitUserPrevent);
            Assert.That(hitUserPrevent.Cancelled, Is.False,
                "ThrownHitUser must bypass only the thrower-collision cancellation.");
        });

        await PoolManager.WaitUntil(Server,
            () => Server.EntMan.GetComponent<SprayThrowingMergeProbeComponent>(target).VaporHits > 0,
            maxTicks: 10);
        await Server.WaitRunTicks(2);

        await Server.WaitAssertion(() =>
        {
            var probe = Server.EntMan.GetComponent<SprayThrowingMergeProbeComponent>(target);
            Assert.Multiple(() =>
            {
                Assert.That(probe.TouchReactions, Is.EqualTo(1),
                    "Allowed self-collision must reach VaporSystem's Touch reaction.");
                Assert.That(probe.VaporHits, Is.EqualTo(1),
                    "Allowed self-collision must reach VaporSystem's VaporHit event.");
                Assert.That(probe.LastVapor, Is.EqualTo(hitUserVapor));
            });
        });
    }

    private (Entity<SprayComponent> Spray, Entity<SolutionComponent> Solution) SpawnSpray(
        SharedSolutionContainerSystem solutions,
        EntityCoordinates coordinates)
    {
        var uid = Server.EntMan.SpawnEntity("SprayBottleSpaceCleaner", coordinates);
        var spray = Server.EntMan.GetComponent<SprayComponent>(uid);
        Assert.That(solutions.TryGetSolution(uid, spray.Solution, out var solutionEnt, out _), Is.True);
        solutions.RemoveAllSolution(solutionEnt!.Value);
        solutions.AddSolution(solutionEnt.Value, new Solution(Reagent, FixedPoint2.New(10)));
        return ((uid, spray), solutionEnt.Value);
    }

    private MapCoordinates TargetCoordinates(EntityUid spray)
    {
        var transform = Server.System<SharedTransformSystem>();
        return transform.GetMapCoordinates(Server.EntMan.GetComponent<TransformComponent>(spray))
            .Offset(new Vector2(0.5f, 0));
    }

    private EntityUid SingleVaporExcept()
    {
        var vapors = Server.EntMan.EntityQuery<VaporComponent>().Select(v => v.Owner).ToArray();
        Assert.That(vapors, Has.Length.EqualTo(1));
        return vapors[0];
    }

    private PreventCollideEvent MakePreventEvent(EntityUid vapor, EntityUid target)
    {
        var entities = Server.EntMan;
        var vaporBody = entities.GetComponent<PhysicsComponent>(vapor);
        var targetBody = entities.GetComponent<PhysicsComponent>(target);
        var vaporFixture = entities.GetComponent<FixturesComponent>(vapor).Fixtures.Values.First();
        var targetFixture = entities.GetComponent<FixturesComponent>(target).Fixtures.Values.First();
        return new PreventCollideEvent(vapor, target, vaporBody, targetBody, vaporFixture, targetFixture);
    }
}

[RegisterComponent]
public sealed partial class SprayAttemptMergeProbeComponent : Component
{
    public bool Cancel;
    public int Attempts;
}

[RegisterComponent]
public sealed partial class SprayThrowingMergeProbeComponent : Component
{
    public int TouchReactions;
    public int VaporHits;
    public EntityUid LastVapor;
}

public sealed class SprayThrowingMergeProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SprayAttemptMergeProbeComponent, SprayAttemptEvent>(OnSprayAttempt);
        SubscribeLocalEvent<SprayThrowingMergeProbeComponent, ReactionEntityEvent>(OnReaction);
        SubscribeLocalEvent<SprayThrowingMergeProbeComponent, VaporHitEvent>(OnVaporHit);
    }

    private static void OnSprayAttempt(Entity<SprayAttemptMergeProbeComponent> ent, ref SprayAttemptEvent args)
    {
        ent.Comp.Attempts++;
        if (ent.Comp.Cancel)
            args.Cancel();
    }

    private static void OnReaction(Entity<SprayThrowingMergeProbeComponent> ent, ref ReactionEntityEvent args)
    {
        if (args.Method == ReactionMethod.Touch)
            ent.Comp.TouchReactions++;
    }

    private static void OnVaporHit(Entity<SprayThrowingMergeProbeComponent> ent, ref VaporHitEvent args)
    {
        ent.Comp.VaporHits++;
        ent.Comp.LastVapor = args.Solution.Owner;
    }
}
