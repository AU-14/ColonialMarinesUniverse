using Content.Server.CMU14.Diagnostics.Performance;
using NUnit.Framework;

namespace Content.Tests.Server.CMU14.Diagnostics.Performance;

[TestFixture]
public sealed class CMUPerformanceIncidentDetectorTest
{
    [Test]
    public void SingleFrameStallStartsAndThenRecoversWithHysteresis()
    {
        var detector = new CMUPerformanceIncidentDetector();
        CMUPerformanceIncidentThresholds thresholds = Thresholds(recoverySeconds: 2);

        CMUPerformanceIncidentEvaluation opened = detector.Evaluate(
            Sample(frameMilliseconds: 300),
            thresholds);

        Assert.That(opened.Transition, Is.EqualTo(CMUPerformanceIncidentTransition.Started));
        Assert.That(opened.Reasons, Is.EqualTo(CMUPerformanceIncidentReason.FrameStall));
        Assert.That(detector.Active, Is.True);

        CMUPerformanceIncidentEvaluation firstHealthy = detector.Evaluate(Sample(), thresholds);
        Assert.That(firstHealthy.Transition, Is.EqualTo(CMUPerformanceIncidentTransition.None));
        Assert.That(detector.Active, Is.True);

        CMUPerformanceIncidentEvaluation recovered = detector.Evaluate(Sample(), thresholds);
        Assert.That(recovered.Transition, Is.EqualTo(CMUPerformanceIncidentTransition.Recovered));
        Assert.That(recovered.PreviousReasons, Is.EqualTo(CMUPerformanceIncidentReason.FrameStall));
        Assert.That(detector.Active, Is.False);
    }

    [Test]
    public void LowTpsMustRemainBreachedAndReachRecoveryRatio()
    {
        var detector = new CMUPerformanceIncidentDetector();
        CMUPerformanceIncidentThresholds thresholds = Thresholds(
            lowTpsRatio: 0.8,
            breachSeconds: 3,
            recoveryRatio: 0.95,
            recoverySeconds: 2);
        CMUPerformanceIncidentSample slow = Sample(achievedTps: 20, tpsValid: true);

        Assert.That(detector.Evaluate(slow, thresholds).Transition, Is.EqualTo(CMUPerformanceIncidentTransition.None));
        Assert.That(detector.Evaluate(slow, thresholds).Transition, Is.EqualTo(CMUPerformanceIncidentTransition.None));

        CMUPerformanceIncidentEvaluation opened = detector.Evaluate(slow, thresholds);
        Assert.That(opened.Transition, Is.EqualTo(CMUPerformanceIncidentTransition.Started));
        Assert.That(opened.Reasons, Is.EqualTo(CMUPerformanceIncidentReason.LowTps));

        CMUPerformanceIncidentEvaluation belowRecovery = detector.Evaluate(
            Sample(achievedTps: 28, tpsValid: true),
            thresholds);
        Assert.That(belowRecovery.Reasons, Is.EqualTo(CMUPerformanceIncidentReason.LowTps));

        Assert.That(detector.Evaluate(Sample(achievedTps: 30, tpsValid: true), thresholds).Transition,
            Is.EqualTo(CMUPerformanceIncidentTransition.None));
        Assert.That(detector.Evaluate(Sample(achievedTps: 30, tpsValid: true), thresholds).Transition,
            Is.EqualTo(CMUPerformanceIncidentTransition.Recovered));
    }

    [Test]
    public void LowFpsUsesTargetTpsAndBreachDuration()
    {
        var detector = new CMUPerformanceIncidentDetector();
        CMUPerformanceIncidentThresholds thresholds = Thresholds(
            lowFpsRatio: 0.8,
            breachSeconds: 2);

        Assert.That(detector.Evaluate(Sample(averageFps: 20), thresholds).Transition,
            Is.EqualTo(CMUPerformanceIncidentTransition.None));
        CMUPerformanceIncidentEvaluation opened = detector.Evaluate(Sample(averageFps: 20), thresholds);

        Assert.That(opened.Transition, Is.EqualTo(CMUPerformanceIncidentTransition.Started));
        Assert.That(opened.Reasons, Is.EqualTo(CMUPerformanceIncidentReason.LowFps));
    }

    [Test]
    public void ImmediateReasonsAreCoalescedIntoOneIncident()
    {
        var detector = new CMUPerformanceIncidentDetector();
        CMUPerformanceIncidentThresholds thresholds = Thresholds(
            sendBytesPerSecond: 100,
            receiveBytesPerSecond: 100,
            allocationBytesPerFrame: 1000);
        CMUPerformanceIncidentSample sample = Sample(
            frameMilliseconds: 500,
            sendBytesPerSecond: 200,
            receiveBytesPerSecond: 300,
            allocatedBytes: 2000);

        CMUPerformanceIncidentEvaluation result = detector.Evaluate(sample, thresholds);

        CMUPerformanceIncidentReason expected =
            CMUPerformanceIncidentReason.FrameStall |
            CMUPerformanceIncidentReason.SendBandwidth |
            CMUPerformanceIncidentReason.ReceiveBandwidth |
            CMUPerformanceIncidentReason.Allocation;
        Assert.That(result.Transition, Is.EqualTo(CMUPerformanceIncidentTransition.Started));
        Assert.That(result.Reasons, Is.EqualTo(expected));
        Assert.That(CMUPerformanceIncidentDetector.FormatReasons(expected),
            Is.EqualTo("frame-stall,send-bandwidth,receive-bandwidth,allocation"));
    }

    [Test]
    public void WarmupSuppressesRatesButNotHardStalls()
    {
        var detector = new CMUPerformanceIncidentDetector();
        CMUPerformanceIncidentThresholds thresholds = Thresholds(
            lowTpsRatio: 0.8,
            breachSeconds: 1,
            entityGrowthPerMinute: 100);

        CMUPerformanceIncidentEvaluation rateOnly = detector.Evaluate(
            Sample(
                achievedTps: 1,
                tpsValid: true,
                entityGrowthPerMinute: 1000,
                churnValid: true,
                suppressRateTriggers: true),
            thresholds);
        Assert.That(rateOnly.Transition, Is.EqualTo(CMUPerformanceIncidentTransition.None));

        CMUPerformanceIncidentEvaluation stall = detector.Evaluate(
            Sample(frameMilliseconds: 300, suppressRateTriggers: true),
            thresholds);
        Assert.That(stall.Transition, Is.EqualTo(CMUPerformanceIncidentTransition.Started));
        Assert.That(stall.Reasons, Is.EqualTo(CMUPerformanceIncidentReason.FrameStall));
    }

    [Test]
    public void EcsGrowthAndChurnThresholdsAreIndependent()
    {
        var detector = new CMUPerformanceIncidentDetector();
        CMUPerformanceIncidentThresholds thresholds = Thresholds(
            entityGrowthPerMinute: 100,
            entityChurnPerMinute: 200,
            componentGrowthPerMinute: 300,
            componentChurnPerMinute: 400);

        CMUPerformanceIncidentEvaluation result = detector.Evaluate(
            Sample(
                entityGrowthPerMinute: 101,
                entityChurnPerMinute: 201,
                componentGrowthPerMinute: 301,
                componentChurnPerMinute: 401,
                churnValid: true),
            thresholds);

        CMUPerformanceIncidentReason expected =
            CMUPerformanceIncidentReason.EntityGrowth |
            CMUPerformanceIncidentReason.EntityChurn |
            CMUPerformanceIncidentReason.ComponentGrowth |
            CMUPerformanceIncidentReason.ComponentChurn;
        Assert.That(result.Reasons, Is.EqualTo(expected));
    }

    private static CMUPerformanceIncidentThresholds Thresholds(
        double stallMilliseconds = 250,
        double lowTpsRatio = 0,
        double lowFpsRatio = 0,
        double breachSeconds = 3,
        double recoveryRatio = 0.95,
        double recoverySeconds = 1,
        double entityGrowthPerMinute = 0,
        double entityChurnPerMinute = 0,
        double componentGrowthPerMinute = 0,
        double componentChurnPerMinute = 0,
        double sendBytesPerSecond = 0,
        double receiveBytesPerSecond = 0,
        double allocationBytesPerFrame = 0)
    {
        return new(
            stallMilliseconds,
            lowTpsRatio,
            lowFpsRatio,
            breachSeconds,
            recoveryRatio,
            recoverySeconds,
            entityGrowthPerMinute,
            entityChurnPerMinute,
            componentGrowthPerMinute,
            componentChurnPerMinute,
            sendBytesPerSecond,
            receiveBytesPerSecond,
            allocationBytesPerFrame);
    }

    private static CMUPerformanceIncidentSample Sample(
        double frameMilliseconds = 0,
        double achievedTps = 30,
        bool tpsValid = false,
        double averageFps = 30,
        double entityGrowthPerMinute = 0,
        double entityChurnPerMinute = 0,
        double componentGrowthPerMinute = 0,
        double componentChurnPerMinute = 0,
        bool churnValid = false,
        double sendBytesPerSecond = 0,
        double receiveBytesPerSecond = 0,
        long allocatedBytes = 0,
        bool suppressRateTriggers = false)
    {
        return new(
            1,
            frameMilliseconds,
            achievedTps,
            tpsValid,
            averageFps,
            30,
            entityGrowthPerMinute,
            entityChurnPerMinute,
            componentGrowthPerMinute,
            componentChurnPerMinute,
            churnValid,
            sendBytesPerSecond,
            receiveBytesPerSecond,
            allocatedBytes,
            suppressRateTriggers);
    }
}
