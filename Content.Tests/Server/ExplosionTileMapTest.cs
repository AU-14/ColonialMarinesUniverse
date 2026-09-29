using System;
using System.Linq;
using Content.Server.Explosion.EntitySystems;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Server;

[TestFixture]
public sealed class ExplosionTileMapTest
{
    [Test]
    public void CapturingLargeGeometryDoesNotCopyTilesAndEditsCopyOnlyTheirChunk()
    {
        var map = new ExplosionTileMap<int>();
        for (var x = 0; x < 100; x++)
        for (var y = 0; y < 100; y++) map.Add(new Vector2i(x, y), x + y);
        map.Capture(); // Warm the snapshot allocation path.
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var snapshot = map.Capture();
        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Assert.That(allocated, Is.LessThan(512), "Capture must not copy 10,000 tile entries.");
        var copies = map.CopiedChunks;
        for (var i = 0; i < 16; i++) map[new Vector2i(i, 0)] = 100;
        Assert.That(map.CopiedChunks - copies, Is.EqualTo(1));
        Assert.That(snapshot[Vector2i.Zero], Is.Zero);
        Assert.That(map[Vector2i.Zero], Is.EqualTo(100));
    }

    [Test]
    public void SnapshotsRetainValuesAcrossChunkBoundariesAndRemoval()
    {
        var map = new ExplosionTileMap<int>();
        var tiles = new[] { new Vector2i(-17, -1), new Vector2i(-1, 0), Vector2i.Zero, new Vector2i(16, 16) };
        for (var i = 0; i < tiles.Length; i++) map.Add(tiles[i], i);
        var first = map.Capture();
        var iterator = first.GetEnumerator();
        Assert.That(iterator.MoveNext(), Is.True);
        map[tiles[0]] = 42;
        map.Remove(tiles[1]);
        var second = map.Capture();
        map.Remove(tiles[0]);
        map[tiles[1]] = 99;
        var third = map.Capture();
        Assert.That(first.Count, Is.EqualTo(4));
        for (var i = 0; i < tiles.Length; i++) Assert.That(first[tiles[i]], Is.EqualTo(i));
        Assert.That(second[tiles[0]], Is.EqualTo(42));
        Assert.That(second.ContainsKey(tiles[1]), Is.False);
        Assert.That(third.ContainsKey(tiles[0]), Is.False);
        Assert.That(third[tiles[1]], Is.EqualTo(99));
        Assert.That(third.Keys.Count(), Is.EqualTo(map.Count));
        var seen = 1;
        while (iterator.MoveNext()) seen++;
        Assert.That(seen, Is.EqualTo(4), "A live snapshot enumerator must remain valid during writes.");
        iterator.Dispose();
    }
}
