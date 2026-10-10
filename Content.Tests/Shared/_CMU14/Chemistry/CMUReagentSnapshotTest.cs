#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Content.Shared.CMU14.Chemistry;
using Content.Shared.Chemistry.Reagent;
using NUnit.Framework;
using Robust.Shared.Random;

namespace Content.Tests.Shared.CMU14.Chemistry;

[TestFixture]
public sealed class CMUReagentSnapshotTest
{
    [TestCase(0), TestCase(1), TestCase(2), TestCase(16), TestCase(17), TestCase(37)]
    public void CapturesIndependentEntriesAndPreservesPredictedShuffle(int count)
    {
        var contents = MakeReagents(count);
        var original = contents.ToList();
        using var snapshot = new CMUReagentSnapshot(contents);
        contents.Clear();
        contents.Add(new ReagentQuantity("Changed", 20));
        using var nested = new CMUReagentSnapshot(contents);

        var beforeRandom = new RobustRandom();
        var afterRandom = new RobustRandom();
        beforeRandom.SetSeed(421);
        afterRandom.SetSeed(421);
        ((IRobustRandom) beforeRandom).Shuffle(original);
        ((IRobustRandom) afterRandom).Shuffle(snapshot.Span);

        Assert.That(snapshot.Span.ToArray(), Is.EqualTo(original));
        Assert.That(nested.Span.ToArray(), Is.EqualTo(contents));
        Assert.That(afterRandom.Next(), Is.EqualTo(beforeRandom.Next()),
            "The shuffle must leave effect-probability RNG in the same state.");
    }

    [TestCase(1), TestCase(4), TestCase(16), Explicit]
    public void MeasureSnapshotAllocations(int reagentCount)
    {
        var contents = MakeReagents(reagentCount);
        Func<int> before = () => contents.ToList().Count;
        Func<int> after = () =>
        {
            using var snapshot = new CMUReagentSnapshot(contents);
            return snapshot.Span.Length;
        };
        for (var i = 0; i < 10000; i++)
        {
            before();
            after();
        }
        const int count = 100000;
        var oldTimes = new double[7];
        var newTimes = new double[7];
        long oldBytes = 0, newBytes = 0;
        for (var block = 0; block < 7; block++)
        {
            if (block % 2 == 0)
            {
                (oldTimes[block], oldBytes) = Measure(before, count, reagentCount);
                (newTimes[block], newBytes) = Measure(after, count, reagentCount);
            }
            else
            {
                (newTimes[block], newBytes) = Measure(after, count, reagentCount);
                (oldTimes[block], oldBytes) = Measure(before, count, reagentCount);
            }
            Assert.That(newBytes, Is.Zero);
        }
        Array.Sort(oldTimes);
        Array.Sort(newTimes);
        TestContext.Out.WriteLine($"CMU_REAGENT_SNAPSHOT reagents={reagentCount} count={count} " +
            $"beforeMs={oldTimes[3]:F3} afterMs={newTimes[3]:F3} " +
            $"beforeBytesPerCall={oldBytes / count} afterBytesPerCall={newBytes / count}");
    }

    private static List<ReagentQuantity> MakeReagents(int count) => Enumerable.Range(0, count)
        .Select(i => new ReagentQuantity($"Reagent{i}", i + 1)).ToList();

    private static (double Milliseconds, long Bytes) Measure(Func<int> action, int count, int expected)
    {
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var start = Stopwatch.GetTimestamp();
        var sum = 0;
        for (var i = 0; i < count; i++)
            sum += action();
        var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        var bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Assert.That(sum, Is.EqualTo(count * expected));
        return (elapsed, bytes);
    }
}
