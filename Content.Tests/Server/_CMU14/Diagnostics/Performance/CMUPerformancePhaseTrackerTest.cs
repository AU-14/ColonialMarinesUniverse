using System;
using System.Linq;
using Content.Server.CMU14.Diagnostics.Performance;
using NUnit.Framework;
using Robust.Shared.ContentPack;

namespace Content.Tests.Server.CMU14.Diagnostics.Performance;

[TestFixture]
public sealed class CMUPerformancePhaseTrackerTest
{
    [Test]
    public void SeparatesStateSendStallFromSimulationAndIdleTime()
    {
        var phases = new CMUPerformancePhaseTracker();
        phases.Mark(ModUpdateLevel.PreEngine, TimeSpan.Zero, 100);
        phases.Mark(ModUpdateLevel.PostEngine, TimeSpan.FromMilliseconds(10), 100);
        phases.Mark(ModUpdateLevel.FramePreEngine, TimeSpan.FromMilliseconds(12010), 100);
        phases.Mark(ModUpdateLevel.FramePostEngine, TimeSpan.FromMilliseconds(12011), 100);
        phases.EndFrameCallbacks(TimeSpan.FromMilliseconds(12021), 100);
        phases.Mark(ModUpdateLevel.InputPostEngine, TimeSpan.FromMilliseconds(13011), 101);
        var rows = phases.Drain();
        Assert.Multiple(() =>
        {
            Assert.That(rows.Single(row => row.Name == "timers-tasks-and-simulation").MaxMs, Is.EqualTo(10));
            Assert.That(rows.Single(row => row.Name == "post-tick-and-state-send").MaxMs, Is.EqualTo(12000));
            Assert.That(rows.Single(row => row.Name == "post-tick-and-state-send").WorstTick, Is.EqualTo(100));
            Assert.That(rows.Single(row => row.Name == "content-frame-callbacks").MaxMs, Is.EqualTo(10));
            Assert.That(rows.Single(row => row.Name == "frame-tail-wait-and-input").MaxMs, Is.EqualTo(990));
            Assert.That(phases.LastFrameWorst.Name, Is.EqualTo("post-tick-and-state-send"));
            Assert.That(phases.LastFrameWorst.MaxMs, Is.EqualTo(12000));
            Assert.That(phases.Drain(), Is.Empty);
        });
        phases.Clear();
        phases.Mark(ModUpdateLevel.PreEngine, TimeSpan.FromSeconds(20), 200);
        Assert.That(phases.Drain(), Is.Empty, "Reset must not attribute downtime to a running tick.");
    }

    [Test]
    public void LatestFrameDoesNotInheritAnEarlierLoadingStall()
    {
        var phases = new CMUPerformancePhaseTracker();
        phases.Mark(ModUpdateLevel.PreEngine, TimeSpan.Zero, 100);
        phases.Mark(ModUpdateLevel.PostEngine, TimeSpan.FromSeconds(10), 100);
        phases.Mark(ModUpdateLevel.InputPostEngine, TimeSpan.FromSeconds(10.01), 101);
        phases.Mark(ModUpdateLevel.PreEngine, TimeSpan.FromSeconds(10.02), 101);
        phases.Mark(ModUpdateLevel.PostEngine, TimeSpan.FromSeconds(10.03), 101);
        phases.Mark(ModUpdateLevel.FramePreEngine, TimeSpan.FromSeconds(10.53), 101);
        phases.Mark(ModUpdateLevel.InputPostEngine, TimeSpan.FromSeconds(10.54), 102);

        Assert.Multiple(() =>
        {
            Assert.That(phases.LastFrameWorst.Name, Is.EqualTo("post-tick-and-state-send"));
            Assert.That(phases.LastFrameWorst.MaxMs, Is.EqualTo(500).Within(0.001));
            Assert.That(phases.LastFrameWorst.WorstTick, Is.EqualTo(101));
            Assert.That(phases.Drain().Single(row => row.Name == "timers-tasks-and-simulation").MaxMs, Is.EqualTo(10000));
        });
    }
}
