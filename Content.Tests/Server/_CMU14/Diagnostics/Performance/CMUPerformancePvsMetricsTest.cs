using System;
using System.Linq;
using Content.Server.CMU14.Diagnostics.Performance;
using NUnit.Framework;
using Prometheus;

namespace Content.Tests.Server.CMU14.Diagnostics.Performance;

[TestFixture]
public sealed class CMUPerformancePvsMetricsTest
{
    private static (IMetricFactory Factory, Histogram Engine) CreateEngineMetrics()
    {
        var factory = Metrics.WithCustomRegistry(Metrics.NewCustomRegistry());
        var engine = factory.CreateHistogram("robust_game_state_update_usage",
            "Amount of time spent processing different parts of the game state update",
            new HistogramConfiguration
            {
                LabelNames = ["area"],
                Buckets = Histogram.ExponentialBuckets(0.000_001, 1.5, 25),
            });
        return (factory, engine);
    }

    [Test]
    public void ReadsExistingEngineCollectorAndDoesNotRepeatDrainedWork()
    {
        var (factory, engine) = CreateEngineMetrics();
        var serialize = engine.WithLabels("Serialize States");
        serialize.Observe(10); // Process lifetime work must not enter the new reporting window.
        var reader = new CMUPerformancePvsMetrics(factory, TimeSpan.FromSeconds(5));
        serialize.Observe(0.025);
        serialize.Observe(0.010);
        engine.WithLabels("Send States").Observe(0.007);

        var window = reader.Drain(TimeSpan.FromSeconds(15));
        var row = window.Stages.Single(stage => stage.Name == "Serialize States");
        var send = window.Stages.Single(stage => stage.Name == "Send States");
        Assert.Multiple(() =>
        {
            Assert.That(window.Seconds, Is.EqualTo(10));
            Assert.That(row.Status, Is.EqualTo("observed"));
            Assert.That(row.Calls, Is.EqualTo(2));
            Assert.That(row.TotalMs, Is.EqualTo(35).Within(0.000001));
            Assert.That(row.AverageMs, Is.EqualTo(17.5).Within(0.000001));
            Assert.That(send.Calls, Is.EqualTo(1));
            Assert.That(send.TotalMs, Is.EqualTo(7).Within(0.000001));
            Assert.That(serialize.Count, Is.EqualTo(3), "The reader must not reset the engine's collector.");
        });

        var next = reader.Drain(TimeSpan.FromSeconds(16));
        Assert.That(next.Stages.All(stage => stage.Calls == 0 && stage.TotalMs == null && stage.AverageMs == null &&
                                            stage.Status == "no-observations"), Is.True,
            "Absent observations, including untimed async ACK/leave work, must not be reported as zero cost.");
    }

    [Test]
    public void ResetExcludesDisabledOrPreviousRoundWork()
    {
        var (factory, engine) = CreateEngineMetrics();
        var reader = new CMUPerformancePvsMetrics(factory, TimeSpan.Zero);
        var child = engine.WithLabels("Get Chunks");
        child.Observe(5);
        reader.Reset(TimeSpan.FromSeconds(10));
        child.Observe(0.003);

        var window = reader.Drain(TimeSpan.FromSeconds(12));
        var row = window.Stages.Single(stage => stage.Name == "Get Chunks");
        Assert.Multiple(() =>
        {
            Assert.That(window.Seconds, Is.EqualTo(2));
            Assert.That(row.Calls, Is.EqualTo(1));
            Assert.That(row.TotalMs, Is.EqualTo(3).Within(0.000001));
            Assert.That(child.Count, Is.EqualTo(2));
        });
    }

    [Test]
    public void ClockResetRebaselinesWithoutReportingOldWork()
    {
        var (factory, engine) = CreateEngineMetrics();
        var reader = new CMUPerformancePvsMetrics(factory, TimeSpan.FromSeconds(10));
        var child = engine.WithLabels("Send States");
        child.Observe(1);
        var reset = reader.Drain(TimeSpan.Zero);
        Assert.That(reset.Seconds, Is.Zero);
        Assert.That(reset.Stages.All(stage => stage.Status == "counter-reset" && stage.TotalMs == null), Is.True);

        child.Observe(0.004);
        var row = reader.Drain(TimeSpan.FromSeconds(1)).Stages.Single(stage => stage.Name == "Send States");
        Assert.That(row.Calls, Is.EqualTo(1));
        Assert.That(row.TotalMs, Is.EqualTo(4).Within(0.000001));
    }
}
