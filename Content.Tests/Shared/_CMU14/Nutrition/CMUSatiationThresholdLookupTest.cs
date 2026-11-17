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

}
