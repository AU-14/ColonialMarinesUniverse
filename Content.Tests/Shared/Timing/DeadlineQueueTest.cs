using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using Content.Shared.Timing;
using NUnit.Framework;

namespace Content.Tests.Shared.Timing;

[TestFixture]
public sealed class DeadlineQueueTest
{
    private sealed record Work(int Id, string Payload)
    {
        public bool Equals(Work other) => other?.Id == Id;
        public override int GetHashCode() => Id;
    }

    [Test]
    public void EqualDeadlineReplacesTheValueWithoutChangingTieOrder()
    {
        var queue = new DeadlineQueue<Work>();
        queue.Schedule(new(1, "old"), TimeSpan.Zero);
        queue.Schedule(new(2, "second"), TimeSpan.Zero);
        queue.Schedule(new(1, "replacement"), TimeSpan.Zero);
        Assert.That(queue.Count, Is.EqualTo(2));
        Assert.That(queue.TryTakeDue(TimeSpan.Zero, out var first), Is.True);
        Assert.That(first.Payload, Is.EqualTo("replacement"));
        Assert.That(queue.TryTakeDue(TimeSpan.Zero, out var second), Is.True);
        Assert.That(second.Id, Is.EqualTo(2));
    }

    [Test]
    public void IdleDeadlineWorkIsIndependentOfPopulation()
    {
        var queue = new DeadlineQueue<int>();
        var deadlines = new TimeSpan[10000];
        for (var i = 0; i < deadlines.Length; i++)
        {
            deadlines[i] = TimeSpan.FromHours(1);
            queue.Schedule(i, deadlines[i]);
        }
        var now = TimeSpan.Zero;
        var visits = 0;
        var due = 0;
        // Warm both paths before the reported sample; no timing threshold in this test.
        queue.TryTakeDue(now, out _);
        var started = Stopwatch.GetTimestamp();
        for (var tick = 0; tick < 1000; tick++)
        foreach (var deadline in deadlines)
        {
            visits++;
            if (deadline <= now) due++;
        }
        var scanMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        started = Stopwatch.GetTimestamp();
        for (var tick = 0; tick < 1000; tick++)
            if (queue.TryTakeDue(now, out _)) due++;
        var scheduledMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        Assert.That(due, Is.Zero);
        Assert.That(queue.Count, Is.EqualTo(deadlines.Length));
        Assert.That(visits, Is.EqualTo(10000000));
        TestContext.Progress.WriteLine($"PERF idle_deadlines population=10000 updates=1000 scanChecks={visits} queueChecks=1000 scanMs={scanMs:F3} queueMs={scheduledMs:F3}");
    }

    [Test]
    public void ReschedulingAndCancellationMatchReferenceSchedule()
    {
        var random = new Random(42);
        var queue = new DeadlineQueue<int>();
        var expected = new Dictionary<int, (TimeSpan Due, int Order)>();
        var order = 0;
        for (var i = 0; i < 10000; i++)
        {
            var item = random.Next(200);
            if (random.Next(4) == 0)
            {
                Assert.That(queue.Remove(item), Is.EqualTo(expected.Remove(item)));
            }
            else
            {
                var due = TimeSpan.FromTicks(random.Next(500));
                expected[item] = (due, expected.TryGetValue(item, out var old) ? old.Order : order++);
                queue.Schedule(item, due);
            }
            Assert.That(queue.Count, Is.EqualTo(expected.Count));
        }

        foreach (var entry in expected.OrderBy(e => e.Value.Due).ThenBy(e => e.Value.Order))
        {
            Assert.That(queue.TryTakeDue(entry.Value.Due - TimeSpan.FromTicks(1), out _), Is.False);
            Assert.That(queue.TryTakeDue(entry.Value.Due, out var actual), Is.True);
            Assert.That(actual, Is.EqualTo(entry.Key));
        }
        Assert.That(queue.Count, Is.Zero);
        Assert.That(queue.TryTakeDue(TimeSpan.MaxValue, out _), Is.False);
    }

    [Test]
    public void ClearReleasesCancelledAndFutureWork()
    {
        var queue = new DeadlineQueue<int>();
        queue.Schedule(1, TimeSpan.MaxValue);
        queue.Clear();
        queue.Schedule(1, TimeSpan.Zero);
        Assert.That(queue.TryTakeDue(TimeSpan.Zero, out var item), Is.True);
        Assert.That(item, Is.EqualTo(1));
        Assert.That(queue.Count, Is.Zero);
    }
}
