using System;
using System.Collections.Generic;
using System.Linq;
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
        var field = new CMUCavernField(12345, new CMUCavernSettings { Variant = CMUCavernVariant.PillarCathedral });
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
    public void NeighbouringRegionsConnectAcrossNegativeChunkCoordinates([Values] CMUCavernVariant variant)
    {
        var field = new CMUCavernField(12345, new CMUCavernSettings { Variant = variant });
        var open = new HashSet<Vector2i>();
        for (var x = -96; x < 96; x++)
        for (var y = -96; y < 96; y++)
        {
            var tile = new Vector2i(x, y);
            if (field.IsOpen(tile, -5))
                open.Add(tile);
        }

        var start = open.First(tile => tile.X < -40 && tile.Y < -40);
        var seen = new HashSet<Vector2i> { start };
        var queue = new Queue<Vector2i>();
        queue.Enqueue(start);
        var directions = new[] { new Vector2i(1, 0), new Vector2i(-1, 0), new Vector2i(0, 1), new Vector2i(0, -1) };
        while (queue.TryDequeue(out var tile))
        {
            foreach (var direction in directions)
            {
                var next = tile + direction;
                if (open.Contains(next) && seen.Add(next))
                    queue.Enqueue(next);
            }
        }
        Assert.That(seen.Any(tile => tile.X > 40 && tile.Y > 40), Is.True,
            "Explorers must be able to follow galleries between regions, including across zero.");
    }
}
