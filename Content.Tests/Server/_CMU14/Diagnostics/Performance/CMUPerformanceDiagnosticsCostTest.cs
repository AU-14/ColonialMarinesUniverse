using System;
using Content.Server.CMU14.Diagnostics.Performance;
using NUnit.Framework;

namespace Content.Tests.Server.CMU14.Diagnostics.Performance;

[TestFixture]
public sealed class CMUPerformanceDiagnosticsCostTest
{
    [Test]
    public void CoalescingRetainsWorstEvidenceAndReopensAtDeadline()
    {
        var gate = new CMUPerformanceDetailGate();
        Assert.That(gate.TryCapture(TimeSpan.Zero, 100, 10), Is.True);
        Assert.That(gate.TryCapture(TimeSpan.Zero, 500, 200), Is.False);
        Assert.That(gate.TryCapture(TimeSpan.FromMilliseconds(999), 300, 500), Is.False);
        Assert.That(gate.Coalesced, Is.EqualTo(2));
        Assert.That(gate.WorstFrameMilliseconds, Is.EqualTo(500));
        Assert.That(gate.WorstAllocatedBytes, Is.EqualTo(500));
        Assert.That(gate.TryCapture(TimeSpan.FromSeconds(1), 100, 10), Is.True);
        Assert.That(gate.Coalesced, Is.EqualTo(2));
    }

    [Test]
    public void CostScopeAccountsForWorkAndDoesNotAllocatePerMeasurement()
    {
        var cost = new CMUPerformanceDiagnosticsCost();
        using (cost.Measure()) { GC.KeepAlive(new byte[4096]); }
        Assert.That(cost.Calls, Is.EqualTo(1));
        Assert.That(cost.AllocatedBytes, Is.GreaterThanOrEqualTo(4096));
        Assert.That(cost.MaximumMilliseconds, Is.GreaterThan(0));
        for (var i = 0; i < 100; i++) { using (cost.Measure()) { } }
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10000; i++) { using (cost.Measure()) { } }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.That(allocated, Is.Zero);
        Assert.That(cost.Calls, Is.EqualTo(10101));
    }
}
