using System;
using System.Linq;
using System.Threading.Tasks;
using Content.Server.CMU14.Diagnostics.Performance;
using NUnit.Framework;
using Serilog.Events;
using Serilog.Parsing;

namespace Content.Tests.Server.CMU14.Diagnostics.Performance;

[TestFixture]
public sealed class CMUPerformanceErrorTrackerTest
{
    [Test]
    public void ConcurrentStormIsCountedWithoutRetainingEveryException()
    {
        var tracker = new CMUPerformanceErrorTracker();
        var message = Message(LogEventLevel.Error);
        Parallel.For(0, 10000, _ => tracker.Log("system.pvs", message));
        var snapshot = tracker.Drain();
        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Total, Is.EqualTo(10000));
            Assert.That(snapshot.Sources, Has.Length.EqualTo(1));
            Assert.That(snapshot.Sources[0].Count, Is.EqualTo(10000));
            Assert.That(snapshot.Sources[0].Last, Is.SameAs(message));
            Assert.That(tracker.Drain().Total, Is.Zero);
        });
    }

    [Test]
    public void BoundsSourcesAndIgnoresDiagnosticFeedback()
    {
        var tracker = new CMUPerformanceErrorTracker();
        for (var i = 0; i < 100; i++)
            tracker.Log($"source{i}", Message(LogEventLevel.Error));
        tracker.Log("system.pvs", Message(LogEventLevel.Warning));
        tracker.Log("cmu.server-performance", Message(LogEventLevel.Error));
        tracker.Log("cmu.client_state", Message(LogEventLevel.Error));
        var snapshot = tracker.Drain();
        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Total, Is.EqualTo(100));
            Assert.That(snapshot.Sources, Has.Length.EqualTo(CMUPerformanceErrorTracker.SourceCapacity));
            Assert.That(snapshot.Overflow + snapshot.Sources.Sum(source => source.Count), Is.EqualTo(100));
        });
        tracker.Log("system.pvs", Message(LogEventLevel.Error));
        Assert.That(tracker.Drain().Sources.Single().Sawmill, Is.EqualTo("system.pvs"));
    }

    private static LogEvent Message(LogEventLevel level) => new(DateTimeOffset.UtcNow, level,
        new InvalidOperationException("network state failed"), new MessageTemplateParser().Parse("state failure"),
        Array.Empty<LogEventProperty>());
}
