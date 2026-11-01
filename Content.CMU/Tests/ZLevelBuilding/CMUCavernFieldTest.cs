using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Shared.CMU14.ZLevelBuilding;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.CMU.ZLevelBuilding;

[TestFixture]
public sealed class CMUCavernFieldTest
{
    [Test]
    public void CavernsSpanLevelsHaveWideChambersAndTerminateAboveBedrock()
    {
        var field = new CMUCavernField(12345, new CMUCavernSettings
        {
            Variant = CMUCavernVariant.PillarCathedral,
            Layout = CMUCavernLayout.Vault,
        });
        var widest = 0;
        var tallest = 0;
        for (var x = -96; x < 96; x++)
        {
            var width = 0;
            for (var y = -96; y < 96; y++)
            {
                var tile = new Vector2i(x, y);
                width = field.IsOpen(tile, -5) ? width + 1 : 0;
                widest = Math.Max(widest, width);
                var height = 0;
                for (var depth = -3; depth >= -8; depth--)
                {
                    height = field.IsOpen(tile, depth) ? height + 1 : 0;
                    tallest = Math.Max(tallest, height);
                }
                Assert.That(field.IsOpen(tile, -2), Is.False, "Caverns must not breach the shallow cover.");
                Assert.That(field.IsOpen(tile, -9), Is.False, "Every vertical column must end at a solid separator.");
            }
        }
        Assert.That(widest, Is.GreaterThan(50), "A chunk-sized chamber does not satisfy the cavern design.");
        Assert.That(tallest, Is.GreaterThanOrEqualTo(4), "The generator must carve a volume, not independent rooms.");
    }

    [Test]
    public void PassageShapesConnectTheirEndpointsThroughRealVoxels([Values] CMUCavernPassageKind kind) =>
        AssertPassageConnects(kind, 7);

    [Test]
    public void ShortBandsDoNotBuryShaftConnectionsInTheSeparator() =>
        AssertPassageConnects(CMUCavernPassageKind.Shaft, 4);

    private static void AssertPassageConnects(CMUCavernPassageKind kind, int bandHeight)
    {
        var field = new CMUCavernField(12345, new CMUCavernSettings { BandHeight = bandHeight });
        var passage = (from x in Enumerable.Range(-4, 8)
                       from y in Enumerable.Range(-4, 8)
                       from p in field.GetRegionPlan(new Vector2i(x, y), -5).Passages
                       where p.Destination.HasValue && p.Kind == kind
                       select p).First();
        var start = Voxel(passage.Points[0]);
        var targetRegion = field.GetRegionPlan(passage.Destination!.Value, -5);
        var target = Voxel(targetRegion.Chambers[0].Center);
        Assert.That(field.IsOpen(new Vector2i(start.X, start.Y), start.Z), Is.True);
        Assert.That(field.IsOpen(new Vector2i(target.X, target.Y), target.Z), Is.True);
        var margin = passage.Widths.Max() + 2;
        var minX = passage.Points.Min(p => p.X) - margin;
        var maxX = passage.Points.Max(p => p.X) + margin;
        var minY = passage.Points.Min(p => p.Y) - margin;
        var maxY = passage.Points.Max(p => p.Y) + margin;
        var seen = new HashSet<(int X, int Y, int Z)> { start };
        var queue = new Queue<(int X, int Y, int Z)>();
        queue.Enqueue(start);
        var directions = new[] { (1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1) };
        while (queue.TryDequeue(out var tile) && !seen.Contains(target))
        {
            foreach (var (dx, dy, dz) in directions)
            {
                var next = (X: tile.X + dx, Y: tile.Y + dy, Z: tile.Z + dz);
                if (next.X < minX || next.X > maxX || next.Y < minY || next.Y > maxY || next.Z > -3 || next.Z < -bandHeight - 1 ||
                    seen.Contains(next) || !field.IsOpen(new Vector2i(next.X, next.Y), next.Z))
                    continue;
                seen.Add(next);
                queue.Enqueue(next);
            }
        }
        Assert.That(seen.Contains(target), Is.True, "The actual carved passage must reach the neighbouring chamber.");
    }

    [Test]
    public void RegionalMazeHasBranchesAndConnectsAcrossNegativeMacroBoundaries()
    {
        var field = new CMUCavernField(12345, new CMUCavernSettings());
        var graph = new Dictionary<Vector2i, HashSet<Vector2i>>();
        for (var x = -4; x < 4; x++)
        for (var y = -4; y < 4; y++)
            graph[new Vector2i(x, y)] = new HashSet<Vector2i>();
        foreach (var region in graph.Keys)
        {
            foreach (var passage in field.GetRegionPlan(region, -5).Passages)
            {
                if (passage.Destination is not { } target || !graph.ContainsKey(target))
                    continue;
                graph[region].Add(target);
                graph[target].Add(region);
            }
        }
        var pending = new Queue<Vector2i>();
        var seen = new HashSet<Vector2i> { new(-4, -4) };
        pending.Enqueue(new(-4, -4));
        while (pending.TryDequeue(out var region))
        foreach (var next in graph[region])
        {
            if (seen.Add(next))
                pending.Enqueue(next);
        }
        Assert.That(seen, Is.EquivalentTo(graph.Keys), "Main routes must survive the loss of the old full grid of exits.");
        Assert.That(graph.Any(p => p.Key.X > -4 && p.Key.X < 3 && p.Key.Y > -4 && p.Key.Y < 3 && p.Value.Count == 1),
            Is.True, "Interior branch endings must exist, not only clipped edges of the test area.");
    }

    [Test]
    public void ClosedSideBranchesEndInRock()
    {
        var field = new CMUCavernField(12345, new CMUCavernSettings());
        var closed = 0;
        for (var x = -2; x <= 2; x++)
        for (var y = -2; y <= 2; y++)
        foreach (var passage in field.GetRegionPlan(new Vector2i(x, y), -5).Passages.Where(p => p.Blind))
        {
            var tip = passage.Points[^1];
            var direction = Vector3.Normalize(tip - passage.Points[^2]);
            var ahead = tip + direction * (passage.Widths[^1] + 4);
            var end = Voxel(tip);
            var wall = Voxel(ahead);
            if (field.IsOpen(new Vector2i(end.X, end.Y), end.Z) &&
                !field.IsOpen(new Vector2i(wall.X, wall.Y), wall.Z) &&
                !field.IsOpen(new Vector2i(wall.X, wall.Y), wall.Z + 1) &&
                !field.IsOpen(new Vector2i(wall.X, wall.Y), wall.Z - 1))
                closed++;
        }
        Assert.That(closed, Is.GreaterThan(0), "Finite side passages must produce actual blind ends in the sampled terrain.");
    }

    [Test]
    public void MixedChambersAndTerrainSurviveDifferentDiscoveryOrderAndCacheEviction()
    {
        var a = new CMUCavernField(12345, new CMUCavernSettings());
        var b = new CMUCavernField(12345, new CMUCavernSettings());
        var plan = a.GetRegionPlan(Vector2i.Zero, -5);
        var localGeology = plan.Chambers.Select(c =>
        {
            var p = Voxel(c.Center);
            Assert.That(a.IsOpen(new Vector2i(p.X, p.Y), p.Z), Is.True);
            return a.VariantAt(new Vector2i(p.X, p.Y), p.Z);
        }).Distinct().ToArray();
        Assert.That(localGeology.Length, Is.GreaterThan(1), "One region must contain more than a renamed single biome.");
        var sites = (from x in Enumerable.Range(-12, 25)
                     from y in Enumerable.Range(-12, 25)
                     select new Vector2i(x * 16, y * 16)).ToArray();
        var expected = sites.ToDictionary(p => p, p => (a.IsOpen(p, -5), a.IsOpen(p, -7), a.SceneryAt(p, -7)));
        for (var i = 0; i < 100; i++)
            b.GetRegionPlan(new Vector2i(50 + i, -50 - i), -12);
        foreach (var p in sites.Reverse())
            Assert.That((b.IsOpen(p, -5), b.IsOpen(p, -7), b.SceneryAt(p, -7)), Is.EqualTo(expected[p]));
    }

    private static (int X, int Y, int Z) Voxel(Vector3 p) =>
        ((int) MathF.Floor(p.X), (int) MathF.Floor(p.Y), (int) MathF.Round(p.Z));
}
