using System;
using System.Collections.Generic;
using Content.Shared.Timing;
using NUnit.Framework;

namespace Content.Tests.Shared.Timing;

[TestFixture]
public sealed class TimeSliceBudgetTest
{
    [Test]
    public void ItemLimitPreservesQueueAcrossSlices()
    {
        var pending = new Queue<int>(new[] { 0, 1, 2, 3, 4 });
        var completed = new List<int>();

        for (var slice = 0; slice < 3; slice++)
        {
            var budget = new TimeSliceBudget(TimeSpan.MaxValue, 2);
            var before = completed.Count;
            while (pending.Count > 0 && budget.TryConsume())
                completed.Add(pending.Dequeue());

            Assert.That(completed.Count - before, Is.InRange(1, 2));
        }

        Assert.That(pending, Is.Empty);
        Assert.That(completed, Is.EqualTo(new[] { 0, 1, 2, 3, 4 }));
    }

    [Test]
    public void ExpiredSliceMakesProgressWithoutDrainingQueue()
    {
        var budget = new TimeSliceBudget(TimeSpan.Zero, 256);
        Assert.That(budget.TryConsume(), Is.True);
        Assert.That(budget.TryConsume(), Is.False);
        Assert.That(budget.TryConsume(), Is.False);
    }

    [Test]
    public void ExhaustedGlobalBudgetDoesNotRestartForNextQueue()
    {
        var budget = new TimeSliceBudget(TimeSpan.MaxValue, 1);
        var squads = new[] { new Queue<int>(new[] { 1, 2 }), new Queue<int>(new[] { 3, 4 }) };
        foreach (var queue in squads)
        {
            while (queue.Count > 0 && budget.TryConsume())
                queue.Dequeue();
        }

        Assert.That(squads[0], Is.EqualTo(new[] { 2 }));
        Assert.That(squads[1], Is.EqualTo(new[] { 3, 4 }));
    }
}
