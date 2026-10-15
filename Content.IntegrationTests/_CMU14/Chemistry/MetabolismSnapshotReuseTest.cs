#pragma warning disable RA0002 // Tests set authoritative deadlines and body state to exercise one metabolism pass.

using System.Linq;
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Medical.Stasis;
using Content.Shared.Body;
using Content.Shared.Body.Events;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.EntityEffects;
using Content.Shared.EntityEffects.Effects.Solution;
using Content.Shared.FixedPoint;
using Content.Shared.Metabolism;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Random.Helpers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Chemistry;

[TestFixture]
[TestOf(typeof(MetabolizerSystem))]
public sealed class MetabolismSnapshotReuseTest : GameTest
{
    private const string ReagentA = "CMUMetabolismSnapshotA";
    private const string ReagentB = "CMUMetabolismSnapshotB";
    private const string ReagentC = "CMUMetabolismSnapshotC";
    private const string Marker = "CMUMetabolismSnapshotMarker";

    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [TestPrototypes]
    private const string Prototypes = """
        - type: reagent
          id: CMUMetabolismSnapshotMarker
          name: reagent-name-water
          desc: reagent-desc-water
          physicalDesc: reagent-physical-desc-translucent

        - type: reagent
          parent: CMUMetabolismSnapshotMarker
          id: CMUMetabolismSnapshotBase
          abstract: true
          metabolisms:
            Bloodstream:
              metabolismRate: 1
              effects:
              - !type:AdjustReagent
                reagent: CMUMetabolismSnapshotMarker
                amount: 1
              - !type:AdjustReagent
                reagent: CMUMetabolismSnapshotMarker
                amount: 2
                probability: 0.5

        - type: reagent
          parent: CMUMetabolismSnapshotBase
          id: CMUMetabolismSnapshotA

        - type: reagent
          parent: CMUMetabolismSnapshotBase
          id: CMUMetabolismSnapshotB
          worksOnTheDead: true

        - type: reagent
          parent: CMUMetabolismSnapshotBase
          id: CMUMetabolismSnapshotC

        - type: entity
          id: CMUMetabolismSnapshotSolution
          components:
          - type: Solution
            id: snapshot
            solution:
              maxVol: 100
              canReact: false

        - type: entity
          id: CMUMetabolismSnapshotEntity
          components:
          - type: SolutionManager
            solutions: [CMUMetabolismSnapshotSolution]
          - type: Metabolizer
            updateInterval: 3600
            maxReagents: 10
            stages: [Bloodstream]
            solutions:
              Bloodstream:
                solutionName: snapshot
                solutionOnBody: false
        """;

    [TestCase(false)]
    [TestCase(true)]
    public async Task MutatingCallbacksAndNestedMetabolismPreserveSnapshotAndRandomOrder(bool reenterDuringEffect)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var outer = CreateMetabolizer(map.GridCoords);
            var inner = CreateMetabolizer(map.GridCoords);
            var metabolism = Server.System<MetabolizerSystem>();
            outer.Solution.Comp.Solution.AddReagent(ReagentA, FixedPoint2.New(0.5));
            outer.Solution.Comp.Solution.AddReagent(ReagentB, 2);
            inner.Solution.Comp.Solution.AddReagent(ReagentC, 2);
            var expectedOuter = ExpectedEffects(outer.Owner, outer.Solution.Comp.Solution.Contents);
            var expectedInner = ExpectedEffects(inner.Owner, inner.Solution.Comp.Solution.Contents);

            void Reenter()
            {
                inner.Metabolizer.NextUpdate = SGameTiming.CurTime;
                metabolism.Update(0f);
            }

            outer.OwnerProbe.OnceOnSnapshot = () =>
            {
                // The current pass must still process the original quantities and shuffle order.
                outer.Solution.Comp.Solution.RemoveAllSolution();
                outer.Solution.Comp.Solution.AddReagent(ReagentC, 3);
                if (!reenterDuringEffect)
                    Reenter();
            };
            if (reenterDuringEffect)
                outer.SolutionProbe.OnceOnEffect = Reenter;

            outer.Metabolizer.NextUpdate = SGameTiming.CurTime;
            metabolism.Update(0f);

            Assert.That(outer.SolutionProbe.Effects, Is.EqualTo(expectedOuter),
                "callbacks must not replace the active snapshot or alter effect RNG consumption");
            Assert.That(inner.SolutionProbe.Effects, Is.EqualTo(expectedInner));
            Assert.That(outer.Solution.Comp.Solution.GetTotalPrototypeQuantity(ReagentC), Is.EqualTo((FixedPoint2) 3),
                "a reagent added after the snapshot must wait for a later metabolism pass");
            Assert.That(outer.Solution.Comp.Solution.GetTotalPrototypeQuantity(Marker),
                Is.EqualTo(expectedOuter.Aggregate(FixedPoint2.Zero, (sum, effect) => sum + effect.Amount * effect.Scale)));
            Assert.That(inner.Solution.Comp.Solution.GetTotalPrototypeQuantity(ReagentC), Is.EqualTo((FixedPoint2) 1));
            Assert.That(outer.OwnerProbe.Snapshots, Is.EqualTo(1));
            Assert.That(inner.OwnerProbe.Snapshots, Is.EqualTo(1));
            Assert.That(outer.Metabolizer.NextUpdate, Is.EqualTo(SGameTiming.CurTime + TimeSpan.FromHours(1)));
            Assert.That(inner.Metabolizer.NextUpdate, Is.EqualTo(SGameTiming.CurTime + TimeSpan.FromHours(1)));
            var returned = GetSnapshotPool(metabolism);
            Assert.That(returned.Count, Is.GreaterThanOrEqualTo(2), "nested calls must own separate reusable lists");
            Assert.That(returned.All(list => list.Count == 0), Is.True, "returned lists must release reagent references");
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task InterruptedPassDoesNotLeakOldReagentsIntoTheNextSnapshot(bool throwFromCallback)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var test = CreateMetabolizer(map.GridCoords);
            var metabolism = Server.System<MetabolizerSystem>();
            var solution = test.Solution.Comp.Solution;
            solution.AddReagent(ReagentA, 2);
            solution.AddReagent(ReagentB, 2);
            test.Metabolizer.MaxReagentsProcessable = 1;
            test.Metabolizer.NextUpdate = SGameTiming.CurTime;

            if (throwFromCallback)
            {
                test.OwnerProbe.OnceOnSnapshot = () => throw new InvalidOperationException("snapshot regression probe");
                Assert.Throws<InvalidOperationException>(() => metabolism.Update(0f));
            }
            else
            {
                metabolism.Update(0f);
                Assert.That(solution.GetTotalPrototypeQuantity(ReagentA) + solution.GetTotalPrototypeQuantity(ReagentB),
                    Is.EqualTo((FixedPoint2) 3), "the one-reagent cap must still end this pass early");
            }

            var returned = GetSnapshotPool(metabolism);
            Assert.That(returned, Is.Not.Empty, "early returns and callback exceptions must return the rented list");
            var previousSnapshot = returned.Peek();
            Assert.That(previousSnapshot, Is.Empty);
            solution.RemoveAllSolution();
            solution.AddReagent(ReagentC, 2);
            test.SolutionProbe.Effects.Clear();
            var expected = ExpectedEffects(test.Owner, solution.Contents);
            test.Metabolizer.NextUpdate = SGameTiming.CurTime;
            metabolism.Update(0f);

            Assert.That(test.SolutionProbe.Effects, Is.EqualTo(expected));
            Assert.That(solution.GetTotalPrototypeQuantity(ReagentC), Is.EqualTo((FixedPoint2) 1));
            Assert.That(test.OwnerProbe.Snapshots, Is.EqualTo(2));
            Assert.That(returned.Peek(), Is.SameAs(previousSnapshot), "the next pass must reuse the returned buffer");
            Assert.That(returned.Peek(), Is.Empty);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task DeadAndStasisChecksKeepTheirExistingProcessingRules(bool inStasis)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var test = CreateMetabolizer(map.GridCoords);
            var solution = test.Solution.Comp.Solution;
            solution.AddReagent(ReagentA, 2);
            solution.AddReagent(ReagentB, 2);
            if (inStasis)
            {
                var body = SEntMan.SpawnEntity(null, map.GridCoords);
                SEntMan.EnsureComponent<CMInStasisComponent>(body);
                SEntMan.EnsureComponent<OrganComponent>(test.Owner).Body = body;
            }
            else
            {
                SEntMan.EnsureComponent<MobStateComponent>(test.Owner).CurrentState = MobState.Dead;
            }

            test.Metabolizer.NextUpdate = SGameTiming.CurTime;
            Server.System<MetabolizerSystem>().Update(0f);

            Assert.That(solution.GetTotalPrototypeQuantity(ReagentA), Is.EqualTo((FixedPoint2) 2));
            Assert.That(solution.GetTotalPrototypeQuantity(ReagentB), Is.EqualTo((FixedPoint2) (inStasis ? 2 : 1)));
            Assert.That(test.OwnerProbe.Snapshots, Is.EqualTo(inStasis ? 0 : 1));
            if (inStasis)
                Assert.That(test.SolutionProbe.Effects, Is.Empty);
            else
            {
                Assert.That(test.SolutionProbe.Effects, Is.Not.Empty);
                Assert.That(test.SolutionProbe.Effects.Select(effect => effect.Reagent), Is.All.EqualTo(ReagentB),
                    "only the reagent marked WorksOnTheDead may apply effects");
            }
            Assert.That(test.Metabolizer.NextUpdate, Is.EqualTo(SGameTiming.CurTime + TimeSpan.FromHours(1)));
        });
    }

    // Public gameplay events cover correctness; inspecting the private pool verifies the allocation
    // contract and cleanup paths without a timing-sensitive GC allocation threshold.
    private static Stack<List<ReagentQuantity>> GetSnapshotPool(MetabolizerSystem metabolism)
    {
        var field = typeof(MetabolizerSystem).GetField("_reagentSnapshots", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        return (Stack<List<ReagentQuantity>>) field!.GetValue(metabolism)!;
    }

    private (EntityUid Owner, MetabolizerComponent Metabolizer, Entity<SolutionComponent> Solution,
        CMUMetabolismSnapshotProbeComponent OwnerProbe, CMUMetabolismSnapshotProbeComponent SolutionProbe)
        CreateMetabolizer(EntityCoordinates coordinates)
    {
        _ = Server.System<CMUMetabolismSnapshotProbeSystem>();
        var owner = SEntMan.SpawnEntity("CMUMetabolismSnapshotEntity", coordinates);
        var metabolizer = SEntMan.GetComponent<MetabolizerComponent>(owner);
        var solutions = Server.System<SharedSolutionContainerSystem>();
        Assert.That(solutions.TryGetSolution(owner, "snapshot", out var solution), Is.True);
        return (owner, metabolizer, solution!.Value,
            SEntMan.EnsureComponent<CMUMetabolismSnapshotProbeComponent>(owner),
            SEntMan.EnsureComponent<CMUMetabolismSnapshotProbeComponent>(solution.Value.Owner));
    }

    private List<(string Reagent, FixedPoint2 Amount, float Scale)> ExpectedEffects(
        EntityUid owner, List<ReagentQuantity> contents)
    {
        var shuffled = contents.ToList();
        var net = SEntMan.GetNetEntity(owner);
        var random = SharedRandomExtensions.PredictedRandom(SGameTiming, net, net);
        random.Shuffle(shuffled);
        var expected = new List<(string Reagent, FixedPoint2 Amount, float Scale)>();
        foreach (var quantity in shuffled)
        {
            var scale = Math.Min(1f, quantity.Quantity.Float());
            random.NextFloat(); // Even a guaranteed effect consumes one probability draw.
            expected.Add((quantity.Reagent.Prototype.Id, FixedPoint2.New(1), scale));
            if (random.NextFloat() < 0.5f)
                expected.Add((quantity.Reagent.Prototype.Id, FixedPoint2.New(2), scale));
        }
        return expected;
    }
}

[RegisterComponent]
public sealed partial class CMUMetabolismSnapshotProbeComponent : Component
{
    public int Snapshots;
    public Action OnceOnSnapshot;
    public Action OnceOnEffect;
    public readonly List<(string Reagent, FixedPoint2 Amount, float Scale)> Effects = [];
}

public sealed class CMUMetabolismSnapshotProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<CMUMetabolismSnapshotProbeComponent, MetabolismExclusionEvent>(OnSnapshot);
        SubscribeLocalEvent<CMUMetabolismSnapshotProbeComponent, EntityEffectEvent<AdjustReagent>>(OnEffect);
    }

    private static void OnSnapshot(Entity<CMUMetabolismSnapshotProbeComponent> ent, ref MetabolismExclusionEvent args)
    {
        ent.Comp.Snapshots++;
        var callback = ent.Comp.OnceOnSnapshot;
        ent.Comp.OnceOnSnapshot = null;
        callback?.Invoke();
    }

    private static void OnEffect(Entity<CMUMetabolismSnapshotProbeComponent> ent, ref EntityEffectEvent<AdjustReagent> args)
    {
        if (args.ReagentContext is not { Origin: ReagentEffectOrigin.Metabolism } context)
            return;

        ent.Comp.Effects.Add((context.Reagent.ID, args.Effect.Amount, args.Scale));
        var callback = ent.Comp.OnceOnEffect;
        ent.Comp.OnceOnEffect = null;
        callback?.Invoke();
    }
}
