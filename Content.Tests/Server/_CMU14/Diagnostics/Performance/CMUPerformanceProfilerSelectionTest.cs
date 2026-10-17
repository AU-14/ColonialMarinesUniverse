using System.Collections.Generic;
using System.Linq;
using Content.Server.CMU14.Diagnostics.Performance;
using NUnit.Framework;

namespace Content.Tests.Server.CMU14.Diagnostics.Performance;

[TestFixture]
public sealed class CMUPerformanceProfilerSelectionTest
{
    [Test]
    public void SelectionMixIncludesZeroTickProblemsAndTickWork()
    {
        CMUPerformanceProfileCandidate[] candidates =
        [
            new(1, 1.0, 10, 0, 10),
            new(2, 0.1, 1000, 0, 10),
            new(3, 0.02, 2, 0, 10),
            new(4, 0.2, 20, 1, 10),
            new(5, 0.01, 1, 0, 10),
            new(6, 0.03, 3, 1, 10),
        ];

        IReadOnlyList<long> selected = CMUPerformanceProfilerReader.SelectFrameOffsets(
            candidates,
            4,
            1000,
            out bool truncated);

        Assert.That(selected, Has.Count.EqualTo(4));
        Assert.That(selected.Contains(1), Is.True, "slow zero-tick frame must be selected");
        Assert.That(selected.Contains(2), Is.True, "allocation-heavy zero-tick frame must be selected");
        Assert.That(selected.Contains(6), Is.True, "most recent tick-bearing frame must be selected");
        Assert.That(selected.Contains(3), Is.False);
        Assert.That(selected.Contains(5), Is.False);
        Assert.That(truncated, Is.False);
    }

    [Test]
    public void SelectionHonorsEventCapWithoutDroppingTheFirstProblemFrame()
    {
        CMUPerformanceProfileCandidate[] candidates =
        [
            new(1, 1.0, 10, 0, 200),
            new(2, 0.1, 1000, 0, 100),
            new(3, 0.2, 20, 1, 10),
        ];

        IReadOnlyList<long> selected = CMUPerformanceProfilerReader.SelectFrameOffsets(
            candidates,
            3,
            250,
            out bool truncated);

        Assert.That(selected.Contains(1), Is.True);
        Assert.That(selected.Contains(2), Is.False);
        Assert.That(selected.Contains(3), Is.True);
        Assert.That(truncated, Is.True);
    }

    [TestCase(0)]
    [TestCase(1)]
    public void SingleFrameSelectionUsesNewestCompletedFrame(int newestTickCount)
    {
        CMUPerformanceProfileCandidate[] candidates =
        [
            new(1, 1.0, 1000, 1, 20),
            new(2, 0.2, 100, 1, 20),
            new(3, 0.01, 1, newestTickCount, 20),
        ];

        IReadOnlyList<long> selected = CMUPerformanceProfilerReader.SelectFrameOffsets(
            candidates,
            1,
            128,
            out bool truncated);

        Assert.That(selected, Is.EqualTo(new long[] { 3 }),
            "The newest completed frame must remain visible even when older frames have larger timings or allocations.");
        Assert.That(truncated, Is.False);
    }

    [Test]
    public void NewestZeroTickFrameKeepsRecentTickWorkAlongsideIt()
    {
        CMUPerformanceProfileCandidate[] candidates =
        [
            new(1, 1.0, 1000, 1, 20),
            new(2, 0.02, 2, 1, 20),
            new(3, 0.01, 1, 0, 20),
        ];

        IReadOnlyList<long> selected = CMUPerformanceProfilerReader.SelectFrameOffsets(
            candidates,
            2,
            128,
            out bool truncated);

        Assert.That(selected, Is.EqualTo(new long[] { 2, 3 }),
            "A fresh input-only frame and the latest simulation frame take priority over an older extreme.");
        Assert.That(truncated, Is.False);
    }

    [Test]
    public void OversizedHistoricalFrameDoesNotStarveRecentFrames()
    {
        CMUPerformanceProfileCandidate[] candidates =
        [
            new(1, 10.0, 1000, 1, 2000),
            new(2, 0.02, 2, 1, 64),
            new(3, 0.01, 1, 0, 64),
        ];

        IReadOnlyList<long> selected = CMUPerformanceProfilerReader.SelectFrameOffsets(
            candidates,
            3,
            128,
            out bool truncated);

        Assert.That(selected, Is.EqualTo(new long[] { 2, 3 }));
        Assert.That(truncated, Is.True,
            "Omitting historical detail because it exceeds the event budget must remain explicit.");
    }

    [Test]
    public void RejectedLargeCandidatesAreBackfilledWithinFrameAndEventLimits()
    {
        CMUPerformanceProfileCandidate[] candidates =
        [
            new(1, 10.0, 10, 1, 200),
            new(2, 0.1, 1000, 1, 200),
            new(3, 0.01, 1, 1, 30),
            new(4, 0.02, 2, 1, 30),
            new(5, 0.03, 3, 1, 30),
        ];

        IReadOnlyList<long> selected = CMUPerformanceProfilerReader.SelectFrameOffsets(
            candidates,
            3,
            128,
            out bool truncated);

        Assert.That(selected, Is.EqualTo(new long[] { 3, 4, 5 }),
            "Rejected expensive candidates must not occupy frame slots that smaller recent frames can fill.");
        Assert.That(truncated, Is.True);
    }

    [Test]
    public void OversizedNewestFrameIsSelectedAloneForPartialCapture()
    {
        CMUPerformanceProfileCandidate[] candidates =
        [
            new(1, 1.0, 1000, 1, 20),
            new(2, 0.2, 100, 1, 20),
            new(3, 0.01, 1, 0, 400),
        ];

        IReadOnlyList<long> selected = CMUPerformanceProfilerReader.SelectFrameOffsets(
            candidates,
            3,
            128,
            out bool truncated);

        Assert.That(selected, Is.EqualTo(new long[] { 3 }),
            "A bounded tail of the newest frame must not be replaced by complete but stale frames.");
        Assert.That(truncated, Is.True);
    }
}
