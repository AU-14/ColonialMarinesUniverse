using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Nutrition;
using Content.Shared.StatusEffectNew;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Nutrition;

[TestFixture]
public sealed class PillForceFeedRegressionTest : GameTest
{
    private static readonly ProtoId<ReagentPrototype> OsteoCalc = "CMUOsteoCalc";

    [SidedDependency(Side.Server)] private BodySystem _body = default!;
    [SidedDependency(Side.Server)] private SharedHandsSystem _hands = default!;
    [SidedDependency(Side.Server)] private SharedSolutionContainerSystem _solutions = default!;
    [SidedDependency(Side.Server)] private StatusEffectsSystem _status = default!;

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  parent: CMPill
  id: CMDirectAbsorptionTestPill
  components:
  - type: Solution
    solution:
      maxVol: 10
      reagents:
      - ReagentId: CMUOsteoCalc
        Quantity: 10

- type: entity
  parent: Pill
  id: UpstreamDigestionTestPill
  components:
  - type: Edible
    delay: 0
  - type: Solution
    solution:
      maxVol: 10
      reagents:
      - ReagentId: CMUOsteoCalc
        Quantity: 10
";

    [Test]
    public async Task FeedingPillToAnotherMobStartsForceFeedDoAfter()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var feeder = SSpawnAtPosition("CMMobHuman", map.GridCoords);
            var target = SSpawnAtPosition("CMMobHuman", map.GridCoords);
            var pill = SSpawnAtPosition("CMPill", map.GridCoords);

            Assert.That(_hands.TryPickupAnyHand(feeder, pill, checkActionBlocker: false), Is.True);

            var ingest = new AttemptIngestEvent(feeder, pill, true);
            SEntMan.EventBus.RaiseLocalEvent(target, ref ingest);

            Assert.That(ingest.Handled, Is.True);
            Assert.That(SEntMan.TryGetComponent<DoAfterComponent>(feeder, out var doAfters), Is.True);
            Assert.That(doAfters!.DoAfters.Values.Any(data =>
                data.Args.Event is EatingDoAfterEvent &&
                data.Args.Delay == TimeSpan.FromSeconds(1) &&
                !data.Cancelled &&
                !data.Completed), Is.True);

            SEntMan.DeleteEntity(pill);
            SEntMan.DeleteEntity(target);
            SEntMan.DeleteEntity(feeder);
        });
    }

    [Test]
    public async Task CmPillsAbsorbDirectlyWhileUpstreamPillsUseTheStomach()
    {
        var map = await Pair.CreateTestMap();
        var cmTarget = EntityUid.Invalid;
        var upstreamTarget = EntityUid.Invalid;

        await Server.WaitAssertion(() =>
        {
            cmTarget = SSpawnAtPosition("CMMobHuman", map.GridCoords);
            upstreamTarget = SSpawnAtPosition("CMMobHuman", map.GridCoords);

            StartSelfIngestion(cmTarget, SSpawnAtPosition("CMDirectAbsorptionTestPill", map.GridCoords));
            StartSelfIngestion(upstreamTarget, SSpawnAtPosition("UpstreamDigestionTestPill", map.GridCoords));
        });

        await RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(GetBloodstreamQuantity(cmTarget), Is.EqualTo(FixedPoint2.New(10)),
                    "CM pill dose did not enter the bloodstream whole.");
                Assert.That(GetStomachQuantity(cmTarget), Is.EqualTo(FixedPoint2.Zero),
                    "CM pill dose incorrectly entered upstream digestion.");
                Assert.That(GetBloodstreamQuantity(upstreamTarget), Is.EqualTo(FixedPoint2.Zero),
                    "Upstream pill bypassed digestion.");
                Assert.That(GetStomachQuantity(upstreamTarget), Is.EqualTo(FixedPoint2.New(10)),
                    "Upstream pill did not enter the stomach whole.");
            });
        });
    }

    [Test]
    public async Task CmOsteoCalcPillStartsItsBloodstreamEffect()
    {
        var map = await Pair.CreateTestMap();
        var target = EntityUid.Invalid;

        await Server.WaitAssertion(() =>
        {
            target = SSpawnAtPosition("CMMobHuman", map.GridCoords);
            StartSelfIngestion(target, SSpawnAtPosition("CMDirectAbsorptionTestPill", map.GridCoords));
        });

        await RunSeconds(1.2f);

        await Server.WaitAssertion(() =>
        {
            Assert.That(_status.TryGetStatusEffect(target, "StatusEffectCMUBoneRegenBoost", out _), Is.True,
                "Osteocalc reached the bloodstream but did not start bone regeneration.");
        });
    }

    private void StartSelfIngestion(EntityUid target, EntityUid pill)
    {
        Assert.That(_hands.TryPickupAnyHand(target, pill, checkActionBlocker: false), Is.True);

        var ingest = new AttemptIngestEvent(target, pill, true);
        SEntMan.EventBus.RaiseLocalEvent(target, ref ingest);

        Assert.That(ingest.Handled, Is.True);
    }

    private FixedPoint2 GetBloodstreamQuantity(EntityUid target)
    {
        var bloodstream = SComp<BloodstreamComponent>(target);
        Assert.That(_solutions.TryGetSolution(target, bloodstream.BloodSolutionName, out var solution), Is.True);
        return solution!.Value.Comp.Solution.GetTotalPrototypeQuantity(OsteoCalc);
    }

    private FixedPoint2 GetStomachQuantity(EntityUid target)
    {
        Assert.That(_body.TryGetOrgansWithComponent<StomachComponent>(target, out var stomachs), Is.True);
        var stomach = stomachs.Single();
        Assert.That(_solutions.TryGetSolution(stomach.Owner, StomachSystem.DefaultSolutionName, out var solution), Is.True);
        return solution!.Value.Comp.Solution.GetTotalPrototypeQuantity(OsteoCalc);
    }
}
