using System;
using Content.Server.CMU14.Diagnostics.Performance;
using NUnit.Framework;

namespace Content.Tests.Server.CMU14.Diagnostics.Performance;

[TestFixture]
public sealed class CMUPerformanceSpikeCaptureTest
{
    private const double AllocationThreshold = 32 * 1024 * 1024;

    [TestCase(155.07, 98348432)]
    [TestCase(218.75, 102509384)]
    public void LoggedGameplaySpikesAreCapturedIndependentlyOfIncidentAge(double ms, long bytes)
    {
        var gate = new CMUPerformanceSpikeCapture();
        Assert.That(gate.ShouldCapture(TimeSpan.FromSeconds(73), ms, bytes, 50, AllocationThreshold), Is.True);
        gate.Record(TimeSpan.FromSeconds(73), ms, bytes);
        Assert.That(gate.ShouldCapture(TimeSpan.FromSeconds(74), ms, bytes, 50, AllocationThreshold), Is.False);
        Assert.That(gate.ShouldCapture(TimeSpan.FromSeconds(83), ms, bytes, 50, AllocationThreshold), Is.True);
    }

    [Test]
    public void WorseSpikeBypassesCooldownButReportsCannotRecurseImmediately()
    {
        var gate = new CMUPerformanceSpikeCapture();
        gate.Record(TimeSpan.Zero, 50, 1024);
        Assert.That(gate.ShouldCapture(TimeSpan.FromMilliseconds(500), 219, 100000000, 50, AllocationThreshold), Is.False);
        Assert.That(gate.ShouldCapture(TimeSpan.FromSeconds(1), 219, 100000000, 50, AllocationThreshold), Is.True);
        Assert.That(gate.ShouldCapture(TimeSpan.FromSeconds(11), 10, 1024, 50, AllocationThreshold), Is.False);
    }

    [Test]
    public void AllocationSpikeDoesNotRequireMatchingFrameTiming()
    {
        var gate = new CMUPerformanceSpikeCapture();
        Assert.That(gate.ShouldCapture(TimeSpan.Zero, 1, 100000000, 50, AllocationThreshold), Is.True);
        gate.Record(TimeSpan.Zero, 1, 100000000);
        gate.Clear();
        Assert.That(gate.ShouldCapture(TimeSpan.Zero, 155, 100000000, 50, AllocationThreshold), Is.True);
    }
}
