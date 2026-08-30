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
}
