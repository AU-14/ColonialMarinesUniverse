using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared._CMU14.Traits;
using Content.Shared._RMC14.Body;
using Content.Shared._RMC14.Medical.Stasis;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids.Components;
using Content.Shared.Forensics.Components;
using Content.Shared.Forensics.Systems;
using Content.Shared.HealthExaminable;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.Body;

[TestFixture]
[TestOf(typeof(BloodstreamSystem))]
public sealed class BloodstreamMergeRegressionTest : GameTest
{
    private static readonly ProtoId<DamageTypePrototype> Blunt = "Blunt";
    private static readonly ProtoId<ReagentPrototype> Blood = "Blood";
    private static readonly ProtoId<ReagentPrototype> Water = "Water";

    [SidedDependency(Side.Server)] private BloodstreamSystem _bloodstream = default!;
    [SidedDependency(Side.Server)] private DamageableSystem _damageable = default!;
    [SidedDependency(Side.Server)] private SharedSolutionContainerSystem _solutions = default!;

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  parent: MobBloodstream
  id: BloodstreamMergeTarget
  components:
  - type: MobState
  - type: Bloodstream
    bloodReferenceSolution:
      reagents:
      - ReagentId: Blood
        Quantity: 100
    bloodRefreshAmount: 0
    bleedPuddleThreshold: 100

- type: entity
  parent: BloodstreamMergeTarget
  id: BloodstreamMergeSpillDefault
  components:
  - type: Bloodstream
    bleedPuddleThreshold: 1

- type: entity
  parent: BloodstreamMergeSpillDefault
  id: BloodstreamMergeSpillChemicals
  components:
  - type: Bloodstream
    spillChemicals: true

- type: entity
  parent: BloodstreamMergeTarget
  id: BloodstreamMergeRmcExamine
  components:
  - type: RMCMedicalExamine

- type: entity
  parent: Solution
  id: BloodstreamMergePrefilledSolution
  categories: [ HideSpawnMenu ]
  components:
  - type: Solution
    id: bloodstream
    solution:
      maxVol: 200
      reagents:
      - ReagentId: Water
        Quantity: 10

- type: entity
  parent: BloodstreamMergeTarget
  id: BloodstreamMergePrefilledTarget
  components:
  - type: Dna
    dna: initial-sentinel
  - type: SolutionManager
    solutions:
    - BloodstreamMergePrefilledSolution
";

    [Test]
    public async Task DamageChangedUsesOrdinaryBleedButCmuAndSynthOwnersHandleIt()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var ordinary = SSpawnAtPosition("MobHuman", map.GridCoords);
            var cmu = SSpawnAtPosition("CMMobHuman", map.GridCoords);
            var synth = SSpawnAtPosition("AU14MobWorkingJoeColony", map.GridCoords);
            var damage = new DamageSpecifier(SProtoMan.Index(Blunt), FixedPoint2.New(1));

            Assert.That(_damageable.TryChangeDamage(ordinary, damage, true, false), Is.Not.Null);
            Assert.That(_damageable.TryChangeDamage(cmu, damage, true, false), Is.Not.Null);
            Assert.That(_damageable.TryChangeDamage(synth, damage, true, false), Is.Not.Null);

            Assert.Multiple(() =>
            {
                Assert.That(SComp<BloodstreamComponent>(ordinary).BleedAmount, Is.GreaterThan(0),
                    "The upstream ordinary bleed path did not run.");
                Assert.That(SComp<BloodstreamComponent>(cmu).BleedAmount, Is.Zero,
                    "CMU wound-ledger damage also entered the upstream generic bleed path.");
                Assert.That(SComp<BloodstreamComponent>(synth).BleedAmount, Is.Zero,
                    "Synthetic damage entered the generic bleed path.");
            });
        });
    }

    [Test]
    public async Task StasisAndDeathFreezeBloodstreamUntilMetabolismResumes()
    {
        var map = await Pair.CreateTestMap();
        var stasis = EntityUid.Invalid;
        var dead = EntityUid.Invalid;

        await Server.WaitAssertion(() =>
        {
            stasis = SSpawnAtPosition("BloodstreamMergeTarget", map.GridCoords);
            dead = SSpawnAtPosition("BloodstreamMergeTarget", map.GridCoords);
            SEntMan.EnsureComponent<CMInStasisComponent>(stasis);
            SEntMan.System<MobStateSystem>().ChangeMobState(dead, MobState.Dead);
            Assert.That(_bloodstream.TryModifyBleedAmount(stasis, 1), Is.True);
            Assert.That(_bloodstream.TryModifyBleedAmount(dead, 1), Is.True);
        });

        await RunSeconds(3.2f);
        await Server.WaitAssertion(() =>
        {
            AssertFrozen(stasis);
            AssertFrozen(dead);
            SEntMan.RemoveComponent<CMInStasisComponent>(stasis);
        });

        await RunSeconds(3.2f);
        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(GetBloodSolution(stasis).GetTotalPrototypeQuantity(Blood),
                    Is.EqualTo(FixedPoint2.New(99)));
                Assert.That(SComp<BloodstreamComponent>(stasis).BleedAmount, Is.LessThan(1));
            });
            AssertFrozen(dead);
        });
    }

    [Test]
    public async Task AnemicModifierMultipliesTheActualBleedDrainOnce()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var ordinary = SSpawnAtPosition("BloodstreamMergeTarget", map.GridCoords);
            var anemic = SSpawnAtPosition("BloodstreamMergeTarget", map.GridCoords);
            SEntMan.EnsureComponent<AnemicComponent>(anemic);
            Assert.That(_bloodstream.TryModifyBleedAmount(ordinary, 1), Is.True);
            Assert.That(_bloodstream.TryModifyBleedAmount(anemic, 1), Is.True);

            _bloodstream.TickBleed(SEntity<BloodstreamComponent>(ordinary));
            _bloodstream.TickBleed(SEntity<BloodstreamComponent>(anemic));

            Assert.Multiple(() =>
            {
                Assert.That(GetBloodSolution(ordinary).GetTotalPrototypeQuantity(Blood),
                    Is.EqualTo(FixedPoint2.New(99)));
                Assert.That(GetBloodSolution(anemic).GetTotalPrototypeQuantity(Blood),
                    Is.EqualTo(FixedPoint2.New(98.8f)));
            });
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task SpillChemicalsPreservesDefaultRetentionAndExactOptInSample(bool spillChemicals)
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var existingPuddles = GetPuddles();
            var prototype = spillChemicals ? "BloodstreamMergeSpillChemicals" : "BloodstreamMergeSpillDefault";
            var target = SSpawnAtPosition(prototype, map.GridCoords);
            var bloodstream = SEntity<BloodstreamComponent>(target);
            Assert.That(_solutions.TryGetSolution(target,
                bloodstream.Comp.BloodSolutionName,
                out var bloodEntity,
                out var bloodSolution), Is.True);
            _solutions.AddSolution(bloodEntity!.Value, new Solution(Water, FixedPoint2.New(10)));

            Assert.That(_bloodstream.TryBleedOut(bloodstream.AsNullable(), FixedPoint2.New(2)), Is.True);

            var foreignSpill = spillChemicals ? FixedPoint2.New(0.2f) : FixedPoint2.Zero;
            Assert.Multiple(() =>
            {
                Assert.That(bloodSolution!.GetTotalPrototypeQuantity(Blood), Is.EqualTo(FixedPoint2.New(98)));
                Assert.That(bloodSolution.GetTotalPrototypeQuantity(Water),
                    Is.EqualTo(FixedPoint2.New(10) - foreignSpill));
                Assert.That(GetTemporarySolution(target).Volume, Is.EqualTo(FixedPoint2.Zero));
            });

            var newPuddles = GetPuddles();
            newPuddles.ExceptWith(existingPuddles);
            Assert.That(newPuddles, Has.Count.EqualTo(1));
            var puddle = newPuddles.Single();
            Assert.That(_solutions.TryGetSolution(puddle,
                SComp<PuddleComponent>(puddle).SolutionName,
                out _,
                out var puddleSolution), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(puddleSolution!.GetTotalPrototypeQuantity(Blood), Is.EqualTo(FixedPoint2.New(2)));
                Assert.That(puddleSolution.GetTotalPrototypeQuantity(Water), Is.EqualTo(foreignSpill));
            });
            SDeleteNow(puddle);
        });
    }

    [Test]
    public async Task RmcAdapterReportsReferenceBloodAndIsolatedForeignChemicals()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var target = SSpawnAtPosition("BloodstreamMergeTarget", map.GridCoords);
            var bloodstream = SEntity<BloodstreamComponent>(target);
            Assert.That(_solutions.TryGetSolution(target,
                bloodstream.Comp.BloodSolutionName,
                out var bloodEntity,
                out var bloodSolution), Is.True);
            _solutions.AddSolution(bloodEntity!.Value, new Solution(Water, FixedPoint2.New(10)));
            var adapter = SEntMan.System<SharedRMCBloodstreamSystem>();

            Assert.That(adapter.TryGetBloodReadout(target, out var current, out var normal), Is.True);
            Assert.That(adapter.TryGetChemicalSolution(target, out var chemicalEntity, out var chemicals), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(current, Is.EqualTo(FixedPoint2.New(100)));
                Assert.That(normal, Is.EqualTo(FixedPoint2.New(100)));
                Assert.That(chemicalEntity.Owner, Is.EqualTo(bloodEntity.Value.Owner));
                Assert.That(chemicals!.GetTotalPrototypeQuantity(Blood), Is.EqualTo(FixedPoint2.Zero));
                Assert.That(chemicals.GetTotalPrototypeQuantity(Water), Is.EqualTo(FixedPoint2.New(10)));
            });

            chemicals!.RemoveReagent(Water, FixedPoint2.New(10));
            Assert.That(bloodSolution!.GetTotalPrototypeQuantity(Water), Is.EqualTo(FixedPoint2.New(10)),
                "The filtered chemical snapshot aliases the live bloodstream.");

            adapter.RemoveBloodstreamChemical(target, Water, FixedPoint2.New(4));
            Assert.Multiple(() =>
            {
                Assert.That(bloodSolution.GetTotalPrototypeQuantity(Blood), Is.EqualTo(FixedPoint2.New(100)));
                Assert.That(bloodSolution.GetTotalPrototypeQuantity(Water), Is.EqualTo(FixedPoint2.New(6)));
            });
        });
    }

    [Test]
    public async Task PrefilledForeignChemicalDoesNotDisplaceReferenceBloodAtMapInit()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var target = SSpawnAtPosition("BloodstreamMergePrefilledTarget", map.GridCoords);
            var solution = GetBloodSolution(target);

            Assert.Multiple(() =>
            {
                Assert.That(solution.GetTotalPrototypeQuantity(Blood), Is.EqualTo(FixedPoint2.New(100)));
                Assert.That(solution.GetTotalPrototypeQuantity(Water), Is.EqualTo(FixedPoint2.New(10)));
            });
        });
    }

    [Test]
    public async Task GenerateDnaUpdatesReferenceBloodButLeavesForeignChemicalDataUntouched()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var target = SSpawnAtPosition("BloodstreamMergePrefilledTarget", map.GridCoords);
            var bloodstream = SEntity<BloodstreamComponent>(target);
            Assert.That(_solutions.TryGetSolution(target,
                bloodstream.Comp.BloodSolutionName,
                out var bloodEntity,
                out var solution), Is.True);

            _solutions.RemoveReagent(bloodEntity!.Value, Water, FixedPoint2.New(10));
            _solutions.AddSolution(
                bloodEntity.Value,
                new Solution(Water, FixedPoint2.New(10), [new DnaData { DNA = "foreign-sentinel" }]));

            var dna = SEntity<DnaComponent>(target);
            var oldDna = dna.Comp.DNA;
            SEntMan.System<ForensicsSystem>().RandomizeDNA(dna.AsNullable());
            Assert.That(dna.Comp.DNA, Is.Not.EqualTo(oldDna));

            var bloodData = solution!.Contents.Single(x => x.Reagent.Prototype == Blood).Reagent.Data;
            var waterData = solution.Contents.Single(x => x.Reagent.Prototype == Water).Reagent.Data;
            Assert.Multiple(() =>
            {
                Assert.That(bloodData, Is.Not.Null);
                Assert.That(bloodData!.OfType<DnaData>().Single().DNA, Is.EqualTo(dna.Comp.DNA));
                Assert.That(waterData, Is.Not.Null);
                Assert.That(waterData!.OfType<DnaData>().Single().DNA, Is.EqualTo("foreign-sentinel"));
            });
        });
    }

    [Test]
    public async Task EmptyReferenceReplacementPreservesForeignChemicalsAndCannotRegulate()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var target = SSpawnAtPosition("BloodstreamMergeTarget", map.GridCoords);
            var bloodstream = SEntity<BloodstreamComponent>(target);
            Assert.That(_solutions.TryGetSolution(target,
                bloodstream.Comp.BloodSolutionName,
                out var bloodEntity,
                out var solution), Is.True);
            _solutions.AddSolution(bloodEntity!.Value, new Solution(Water, FixedPoint2.New(10)));

            Assert.DoesNotThrow(() => _bloodstream.ChangeBloodReagents(bloodstream.AsNullable(), new Solution()));

            var reference = _bloodstream.GetBloodReferenceSolution(bloodstream.AsNullable());
            Assert.Multiple(() =>
            {
                Assert.That(reference, Is.Not.Null);
                Assert.That(reference!.Volume, Is.EqualTo(FixedPoint2.Zero));
                Assert.That(solution!.GetTotalPrototypeQuantity(Blood), Is.EqualTo(FixedPoint2.Zero));
                Assert.That(solution.GetTotalPrototypeQuantity(Water), Is.EqualTo(FixedPoint2.New(10)));
                Assert.That(_bloodstream.TryRegulateBloodLevel(
                    bloodstream.AsNullable(),
                    FixedPoint2.New(1)), Is.False);
            });
        });
    }

    [Test]
    public async Task OrdinaryHealthExamineKeepsUpstreamTextWhileRmcSuppressesIt()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var ordinary = SSpawnAtPosition("BloodstreamMergeTarget", map.GridCoords);
            var rmc = SSpawnAtPosition("BloodstreamMergeRmcExamine", map.GridCoords);
            Assert.That(_bloodstream.TryModifyBleedAmount(ordinary, 1), Is.True);
            Assert.That(_bloodstream.TryModifyBleedAmount(rmc, 1), Is.True);

            var ordinaryMessage = new FormattedMessage();
            var rmcMessage = new FormattedMessage();
            SEntMan.EventBus.RaiseLocalEvent(ordinary, new HealthBeingExaminedEvent(ordinaryMessage));
            SEntMan.EventBus.RaiseLocalEvent(rmc, new HealthBeingExaminedEvent(rmcMessage));

            Assert.Multiple(() =>
            {
                Assert.That(ordinaryMessage.ToMarkup(), Is.Not.Empty);
                Assert.That(rmcMessage.ToMarkup(), Is.Empty);
            });
        });
    }

    private void AssertFrozen(EntityUid target)
    {
        Assert.Multiple(() =>
        {
            Assert.That(GetBloodSolution(target).GetTotalPrototypeQuantity(Blood),
                Is.EqualTo(FixedPoint2.New(100)));
            Assert.That(SComp<BloodstreamComponent>(target).BleedAmount, Is.EqualTo(1));
        });
    }

    private Solution GetBloodSolution(EntityUid target)
    {
        var bloodstream = SComp<BloodstreamComponent>(target);
        Assert.That(_solutions.TryGetSolution(target, bloodstream.BloodSolutionName, out _, out var solution), Is.True);
        return solution!;
    }

    private Solution GetTemporarySolution(EntityUid target)
    {
        var bloodstream = SComp<BloodstreamComponent>(target);
        Assert.That(_solutions.TryGetSolution(target,
            bloodstream.BloodTemporarySolutionName,
            out _,
            out var solution), Is.True);
        return solution!;
    }

    private HashSet<EntityUid> GetPuddles()
    {
        var puddles = new HashSet<EntityUid>();
        var query = SEntMan.EntityQueryEnumerator<PuddleComponent>();
        while (query.MoveNext(out var puddle, out _))
        {
            puddles.Add(puddle);
        }

        return puddles;
    }
}
