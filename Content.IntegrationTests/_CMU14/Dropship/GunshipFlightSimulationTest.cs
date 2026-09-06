using Content.Shared._CMU14.Dropship.TacticalLand;

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class GunshipFlightSimulationTest
{
    [Test]
    public void FixedStepCountDoesNotDependOnFrameChunking()
    {
        var singleAccumulator = 0f;
        var splitAccumulator = 0f;

        var singleSteps = GunshipFlightSimulation.ConsumeSteps(
            ref singleAccumulator,
            GunshipFlightSimulation.StepSeconds);
        var splitSteps = GunshipFlightSimulation.ConsumeSteps(
            ref splitAccumulator,
            GunshipFlightSimulation.StepSeconds * 0.5f);
        splitSteps += GunshipFlightSimulation.ConsumeSteps(
            ref splitAccumulator,
            GunshipFlightSimulation.StepSeconds * 0.5f);

        Assert.Multiple(() =>
        {
            Assert.That(singleSteps, Is.EqualTo(1));
            Assert.That(splitSteps, Is.EqualTo(singleSteps));
            Assert.That(splitAccumulator, Is.EqualTo(singleAccumulator).Within(0.00001f));
        });
    }

    [Test]
    public void LagSpikeCannotCreateUnboundedCatchUpWork()
    {
        var accumulator = 0f;

        var steps = GunshipFlightSimulation.ConsumeSteps(ref accumulator, 5f);

        Assert.Multiple(() =>
        {
            Assert.That(steps, Is.EqualTo(GunshipFlightSimulation.MaxCatchUpSteps));
            Assert.That(accumulator, Is.LessThan(GunshipFlightSimulation.StepSeconds));
        });
    }

    [TestCase(0f, 0.5f, 1)]
    [TestCase(0.5f, 0.5f, 1)]
    [TestCase(0.51f, 0.5f, 2)]
    [TestCase(1.01f, 0.5f, 3)]
    public void LinearSweepBoundsSampleSpacing(float distance, float spacing, int expected)
    {
        Assert.That(GunshipFlightSimulation.GetLinearSweepSteps(distance, spacing), Is.EqualTo(expected));
    }

    [Test]
    public void AngularSweepBoundsHullCornerTravel()
    {
        var quarterTurn = MathF.PI * 0.5f;

        var steps = GunshipFlightSimulation.GetAngularSweepSteps(quarterTurn, 2f, 0.5f);

        Assert.That(steps, Is.EqualTo(7));
    }

    [Test]
    public void CombinedSweepConservativelyCombinesMotion()
    {
        var steps = GunshipFlightSimulation.GetCombinedSweepSteps(
            linearDistance: 1f,
            angleRadians: MathF.PI * 0.5f,
            hullRadius: 2f,
            maximumSpacing: 0.5f);

        Assert.That(steps, Is.EqualTo(9));
    }

    [Test]
    public void CombinedSweepStillSamplesTranslationWhenRotationIsStationary()
    {
        var steps = GunshipFlightSimulation.GetCombinedSweepSteps(
            linearDistance: 1.01f,
            angleRadians: 0f,
            hullRadius: 8f,
            maximumSpacing: 0.5f);

        Assert.That(steps, Is.EqualTo(3));
    }
}
