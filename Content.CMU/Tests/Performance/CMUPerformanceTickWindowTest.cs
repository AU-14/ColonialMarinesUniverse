using System;
using Content.Server.CMU14.Diagnostics.Performance;
using NUnit.Framework;
using Robust.Shared.ContentPack;

namespace Content.Tests.CMU14.Performance;

[TestFixture]
public sealed class CMUPerformanceTickWindowTest
{
    [Test]
    public void CatchupTicksStaySeparateAndFrameIdleDoesNotBecomeSimulationCost()
    {
        var phases = new CMUPerformancePhaseTracker();
        var window = new CMUPerformanceTickWindow();
        phases.PhaseCompleted += (phase, _) => window.Record(phase, 1000d / 30);
        phases.Mark(ModUpdateLevel.PreEngine, TimeSpan.Zero, 10);
        phases.Mark(ModUpdateLevel.PostEngine, TimeSpan.FromMilliseconds(20), 10);
        phases.Mark(ModUpdateLevel.PreEngine, TimeSpan.FromMilliseconds(45), 11);
        phases.Mark(ModUpdateLevel.PostEngine, TimeSpan.FromMilliseconds(50), 11);
        phases.Mark(ModUpdateLevel.FramePreEngine, TimeSpan.FromMilliseconds(60), 12);
        phases.Mark(ModUpdateLevel.FramePostEngine, TimeSpan.FromMilliseconds(62), 12);
        phases.EndFrameCallbacks(TimeSpan.FromMilliseconds(65), 12);
        phases.Mark(ModUpdateLevel.InputPostEngine, TimeSpan.FromMilliseconds(2065), 12);

        Assert.Multiple(() =>
        {
            Assert.That(window.Simulation.TotalMs, Is.EqualTo(25));
            Assert.That(window.StateSend.TotalMs, Is.EqualTo(35));
            Assert.That(window.Combined.Count, Is.EqualTo(2));
            Assert.That(window.Combined.TotalMs, Is.EqualTo(60));
            Assert.That(window.Combined.MaxMs, Is.EqualTo(45));
            Assert.That(window.Combined.WorstTick, Is.EqualTo(10));
            Assert.That(window.Combined.OverBudget, Is.EqualTo(1));
            Assert.That(window.Combined.PercentileUpperBound(.95), Is.EqualTo(50));
        });
    }

    [Test]
    public void ResetAndMismatchedTicksCannotCreateAPhantomCombinedTick()
    {
        var window = new CMUPerformanceTickWindow();
        window.Record(new("timers-tasks-and-simulation", 1, 900, 900, 20), 33);
        window.Clear();
        window.Record(new("post-tick-and-state-send", 1, 12, 12, 20), 33);
        window.Record(new("timers-tasks-and-simulation", 1, 8, 8, 21), 33);
        window.Record(new("post-tick-and-state-send", 1, 8, 8, 22), 33);
        Assert.That(window.Combined.PercentileUpperBound(.99), Is.Null,
            "Missing or mismatched phases are unobserved, not a zero or a fabricated tick.");

        window.Record(new("timers-tasks-and-simulation", 1, 800, 800, 23), 33);
        window.Record(new("post-tick-and-state-send", 1, 500, 500, 23), 33);
        Assert.That(window.Combined.Count, Is.EqualTo(1));
        Assert.That(window.Combined.MaxMs, Is.EqualTo(1300));
        Assert.That(window.Combined.PercentileUpperBound(.99), Is.EqualTo(double.PositiveInfinity),
            "A stall beyond the final finite bucket must not be reported as <=1000 ms.");
    }
}
