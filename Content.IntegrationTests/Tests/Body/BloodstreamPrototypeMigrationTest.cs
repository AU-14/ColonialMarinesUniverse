using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Body;

[TestFixture]
[TestOf(typeof(BloodstreamComponent))]
public sealed class BloodstreamPrototypeMigrationTest : GameTest
{
    [SidedDependency(Side.Server)] private BloodstreamSystem _bloodstream = default!;

    [TestCase("CMUMobApe", "Blood", 1000)]
    [TestCase("AU14MobWorkingJoeColony", "RMCSynthBlood", 560)]
    [TestCase("CMUMobCarpInvasive", "Blood", 150)]
    [TestCase("CMUMobYautja", "CMUYautjaBlood", 650)]
    [TestCase("RMCMobCat", "Blood", 150)]
    [TestCase("CMMobMouse", "Blood", 50)]
    [TestCase("CMMobHuman", "Blood", 560)]
    public async Task ForkBloodstreamsUseReferenceSolutions(string prototypeId, string reagentId, int quantity)
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var target = SSpawnAtPosition(prototypeId, map.GridCoords);
            var reference = _bloodstream.GetBloodReferenceSolution(SEntity<BloodstreamComponent>(target));

            Assert.That(reference, Is.Not.Null, $"{prototypeId} is missing {nameof(BloodstreamComponent)}");
            Assert.Multiple(() =>
            {
                Assert.That(reference!.Volume, Is.EqualTo(FixedPoint2.New(quantity)),
                    $"{prototypeId} has the wrong normal blood volume");
                Assert.That(reference.GetTotalPrototypeQuantity(reagentId), Is.EqualTo(FixedPoint2.New(quantity)),
                    $"{prototypeId} has the wrong reference blood reagent");
                Assert.That(reference.Contents, Has.Count.EqualTo(1),
                    $"{prototypeId} retained an unexpected inherited reference reagent");
            });
        });
    }

    [Test]
    public async Task AbominationRemainsBloodlessWithoutBloodlossStatusThreshold()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var target = SSpawnAtPosition("AU14AbominationGrunt", map.GridCoords);
            var bloodstream = SEntity<BloodstreamComponent>(target);
            var reference = _bloodstream.GetBloodReferenceSolution(bloodstream);

            Assert.That(reference, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(reference!.Volume, Is.EqualTo(FixedPoint2.Zero));
                Assert.That(reference.Contents, Is.Empty);
                Assert.That(bloodstream.Comp.BloodlossThreshold, Is.Zero);
                Assert.That(bloodstream.Comp.MaxBleedAmount, Is.Zero);
                Assert.That(bloodstream.Comp.BloodRefreshAmount, Is.EqualTo(FixedPoint2.Zero));
            });
        });
    }

    [Test]
    public async Task RmcSpeciesInjectIntoUnifiedBloodstream()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var target = SSpawnAtPosition("CMMobHuman", map.GridCoords);

            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<SolutionManagerComponent>(target), Is.True);
                Assert.That(SEntMan.HasComponent<SolutionContainerManagerComponent>(target), Is.False);
                Assert.That(SComp<InjectableSolutionComponent>(target).Solution,
                    Is.EqualTo(BloodstreamComponent.DefaultBloodSolutionName));
            });
        });
    }
}
