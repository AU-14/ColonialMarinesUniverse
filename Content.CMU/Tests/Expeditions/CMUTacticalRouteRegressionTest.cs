using System;
using System.Collections.Generic;
using Content.Shared.CMU14.Expeditions;
using NUnit.Framework;

namespace Content.Tests.CMU14.Expeditions;

[TestFixture]
public sealed class CMUTacticalRouteRegressionTest
{
    [Test]
    public void WeightedRoutesMatchIndependentShortestPathSearch()
    {
        const int size = 7;
        const int count = size * size;
        var workspace = new CMUTacticalRoute.Workspace();
        for (var seed = 0; seed < 60; seed++)
        {
            var random = new Random(seed);
            var blocked = new bool[count];
            var danger = new float[count];
            for (var cell = 0; cell < count; cell++)
            {
                blocked[cell] = random.Next(5) == 0;
                danger[cell] = random.Next(4);
            }
            blocked[0] = blocked[^1] = false;
            var expected = ShortestCost(size, blocked, danger);
            var route = CMUTacticalRoute.Find(size, 0, count - 1, cell => !blocked[cell],
                cell => danger[cell], (_, _) => true, out _, 4096, workspace);
            if (float.IsPositiveInfinity(expected))
            {
                Assert.That(route, Is.Null, $"seed {seed}");
                continue;
            }
            Assert.That(route, Is.Not.Null, $"seed {seed}");
            var cost = 0f;
            for (var index = 1; index < route!.Count; index++)
            {
                var previous = route[index - 1];
                var current = route[index];
                Assert.That(Math.Abs(previous % size - current % size) + Math.Abs(previous / size - current / size),
                    Is.EqualTo(1), "Row wrapping and diagonal steps are not passages.");
                Assert.That(blocked[current], Is.False);
                cost += 1 + danger[current];
            }
            Assert.That(route[0], Is.EqualTo(0));
            Assert.That(route[^1], Is.EqualTo(count - 1));
            Assert.That(cost, Is.EqualTo(expected), $"seed {seed}");
        }
    }

    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    [TestCase(-1f)]
    public void InvalidExposureCannotBecomeARoute(float penalty)
    {
        // The middle column cuts every route; invalid costs cannot be shortcuts.
        Assert.That(CMUTacticalRoute.Find(3, 3, 5, _ => true,
            cell => cell % 3 == 1 ? penalty : 0, (_, _) => true, out _, 100), Is.Null);
    }

    [Test]
    public void RowEdgesAndRejectedPassagesRemainBlocked()
    {
        Assert.That(CMUTacticalRoute.Find(3, 2, 3, cell => cell is 2 or 3,
            _ => 0, (_, _) => true, out _), Is.Null);
        Assert.That(CMUTacticalRoute.Find(3, 0, 8, _ => true,
            _ => 0, (from, to) => from / 3 == to / 3, out _), Is.Null);
    }

    private static float ShortestCost(int size, bool[] blocked, float[] danger)
    {
        var distances = new float[blocked.Length];
        Array.Fill(distances, float.PositiveInfinity);
        distances[0] = 0;
        var settled = new HashSet<int>();
        while (true)
        {
            var closest = -1;
            for (var cell = 0; cell < distances.Length; cell++)
                if (!settled.Contains(cell) && float.IsFinite(distances[cell]) &&
                    (closest == -1 || distances[cell] < distances[closest]))
                    closest = cell;
            if (closest == -1 || closest == distances.Length - 1)
                return distances[^1];
            settled.Add(closest);
            for (var cell = 0; cell < distances.Length; cell++)
                if (!blocked[cell] && Math.Abs(cell % size - closest % size) + Math.Abs(cell / size - closest / size) == 1)
                    distances[cell] = Math.Min(distances[cell], distances[closest] + 1 + danger[cell]);
        }
    }
}
