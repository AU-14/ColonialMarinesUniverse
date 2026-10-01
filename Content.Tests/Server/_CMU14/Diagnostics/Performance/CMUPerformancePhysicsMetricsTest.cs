using System;
using System.Linq;
using Content.Server.CMU14.Diagnostics.Performance;
using NUnit.Framework;
using Prometheus;

namespace Content.Tests.Server.CMU14.Diagnostics.Performance;

[TestFixture]
public sealed class CMUPerformancePhysicsMetricsTest
{
    private static (Histogram.Child Before, Histogram.Child After) Children()
    {
        var factory = Metrics.WithCustomRegistry(Metrics.NewCustomRegistry());
        var histogram = factory.CreateHistogram("test_physics", "", new HistogramConfiguration
        {
            LabelNames = ["phase"],
        });
        return (histogram.WithLabels("before"), histogram.WithLabels("after"));
    }

    [Test]
    public void SeparatesControllerPhasesAndExcludesPriorWork()
    {
        var (before, after) = Children();
        before.Observe(10);
        var reader = new CMUPerformancePhysicsMetrics([("MoverController", before, after)], TimeSpan.FromSeconds(5));
        before.Observe(0.010);
        before.Observe(0.020);
        after.Observe(0.004);
        var window = reader.Drain(TimeSpan.FromSeconds(15));
        Assert.That(window.Seconds, Is.EqualTo(10));
        var pre = window.Stages.Single(row => row.Phase == "before");
        var post = window.Stages.Single(row => row.Phase == "after");
        Assert.Multiple(() =>
        {
            Assert.That(pre.Controller, Is.EqualTo("MoverController"));
            Assert.That(pre.Calls, Is.EqualTo(2));
            Assert.That(pre.TotalMs, Is.EqualTo(30).Within(0.000001));
            Assert.That(pre.AverageMs, Is.EqualTo(15).Within(0.000001));
            Assert.That(post.TotalMs, Is.EqualTo(4).Within(0.000001));
            Assert.That(before.Count, Is.EqualTo(3), "The reader must leave the engine's counters intact.");
        });
        Assert.That(reader.Drain(TimeSpan.FromSeconds(16)).Stages.All(row =>
            row.Calls == 0 && row.TotalMs == null && row.AverageMs == null && row.Status == "no-observations"), Is.True);
    }

    [Test]
    public void ResetAndClockRollbackRebaselineWithoutFalseZeroCosts()
    {
        var (before, after) = Children();
        var reader = new CMUPerformancePhysicsMetrics([("MoverController", before, after)], TimeSpan.Zero);
        before.Observe(1);
        reader.Reset(TimeSpan.FromSeconds(10));
        before.Observe(0.003);
        var first = reader.Drain(TimeSpan.FromSeconds(12));
        Assert.That(first.Stages[0].TotalMs, Is.EqualTo(3).Within(0.000001));
        var rollback = reader.Drain(TimeSpan.Zero);
        Assert.That(rollback.Stages.All(row => row.Status == "counter-reset" && row.TotalMs == null), Is.True);
        before.Observe(0.007);
        Assert.That(reader.Drain(TimeSpan.FromSeconds(1)).Stages[0].TotalMs, Is.EqualTo(7).Within(0.000001));
    }
}
