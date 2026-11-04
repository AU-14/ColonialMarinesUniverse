#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Content.Shared.CMU14.Nutrition;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Nutrition.Prototypes;
using NUnit.Framework;
using Robust.Shared.Random;

namespace Content.Tests.Shared.CMU14.Nutrition;

[TestFixture]
public sealed class CMUSatiationThresholdLookupTest
{
    [Test]
    public void MatchesStableSortedLookupAtBoundsAliasesAndInvalidKeys()
    {
        var prototype = new SatiationPrototype { Thresholds = { ["Middle"] = 50, ["MiddleAlias"] = 50 } };
        SatiationValue[] keys = [0, 50, 100, "Middle", "MiddleAlias", "Missing", -10];
        float[] values = [float.NegativeInfinity, -11, -10, -0.001f, 0, 0.001f,
            49, 50, 50.1f, 99.9f, 100, 101, float.PositiveInfinity, float.NaN];
        IRobustRandom random = new RobustRandom();
        random.SetSeed(713);
        for (var mask = 0; mask < 128; mask++)
        for (var order = 0; order < 4; order++)
        {
            var indices = Enumerable.Range(0, keys.Length).ToList();
            random.Shuffle(indices);
            var thresholds = new Dictionary<SatiationValue, string?>();
            foreach (var i in indices)
                if ((mask & (1 << i)) != 0)
                    thresholds.Add(keys[i], i == 4 ? null : $"Value{i}");

            foreach (var value in values)
            {
                var expected = PriorLookup(prototype, value, thresholds);
                var found = CMUSatiationThresholdLookup.TryGetValue(prototype, value, thresholds,
                    out var result, out var higher, out var lower);
                Assert.That((found, result, higher, lower), Is.EqualTo(expected),
                    $"mask={mask}, order={order}, value={value}");
            }
        }
    }

    [TestCase(250), TestCase(500), TestCase(600), Explicit]
    public void MeasureThresholdLookup(int population)
    {
        var prototype = new SatiationPrototype();
        var thresholds = new Dictionary<SatiationValue, float>
            { [0] = 0, [50] = 0.5f, [110] = 0.75f, [380] = 1, [480] = 0.8f };
        Func<float> before = () => PriorLookup(prototype, 200, thresholds).Result;
        Func<float> after = () =>
        {
            CMUSatiationThresholdLookup.TryGetValue(prototype, 200, thresholds, out var result, out _, out _);
            return result;
        };
        for (var i = 0; i < 10000; i++)
        {
            before();
            after();
        }
        var count = population * 200;
        var oldTimes = new double[7];
        var newTimes = new double[7];
        long oldBytes = 0, newBytes = 0;
        for (var block = 0; block < 7; block++)
        {
            if (block % 2 == 0)
            {
                (oldTimes[block], oldBytes) = Measure(before, count);
                (newTimes[block], newBytes) = Measure(after, count);
            }
            else
            {
                (newTimes[block], newBytes) = Measure(after, count);
                (oldTimes[block], oldBytes) = Measure(before, count);
            }
            Assert.That(newBytes, Is.Zero);
        }
        Array.Sort(oldTimes);
        Array.Sort(newTimes);
        TestContext.Out.WriteLine($"CMU_SATIATION_LOOKUP population={population} count={count} " +
            $"beforeMs={oldTimes[3]:F3} afterMs={newTimes[3]:F3} " +
            $"beforeBytesPerCall={oldBytes / count} afterBytesPerCall={newBytes / count}");
    }

    // The previous algorithm is retained only as a behavioral reference and measurement baseline.
    private static (bool Found, T? Result, int? Higher, int? Lower) PriorLookup<T>(
        SatiationPrototype prototype, float value, Dictionary<SatiationValue, T> thresholds)
    {
        using var sorted = thresholds
            .Select(it => prototype.GetValueOrNull(it.Key) is { } bound ? ((int, T)?)(bound, it.Value) : null)
            .OfType<(int, T)>().OrderByDescending(it => it.Item1).GetEnumerator();
        if (!sorted.MoveNext())
            return (false, default, null, null);
        if (value > sorted.Current.Item1)
            return (false, default, null, sorted.Current.Item1);
        var higher = sorted.Current;
        while (sorted.MoveNext())
        {
            var lower = sorted.Current;
            if (value > lower.Item1)
                return (true, higher.Item2, higher.Item1, lower.Item1);
            higher = lower;
        }
        return (true, higher.Item2, higher.Item1, null);
    }

    private static (double Milliseconds, long Bytes) Measure(Func<float> action, int count)
    {
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var start = Stopwatch.GetTimestamp();
        var sum = 0f;
        for (var i = 0; i < count; i++)
            sum += action();
        var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        var bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Assert.That(sum, Is.EqualTo(count));
        return (elapsed, bytes);
    }
}
