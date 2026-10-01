using System.Diagnostics;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Maps;

namespace Content.IntegrationTests.Tests.Performance;

[TestFixture]
public sealed class AnchoredTileCacheTest : GameTest
{
    [Test]
    public async Task UnanchoredChurnAndOtherTilesPreserveCachedMembership()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var cache = Server.System<AnchoredTileCacheSystem>();
            var maps = Server.System<SharedMapSystem>();
            maps.SetTile(map.Grid, new Vector2i(1, 0), map.Tile.Tile);
            var wall = SEntMan.SpawnEntity("CMWallRock", map.GridCoords);
            Assert.That(cache.Get(map.Grid, Vector2i.Zero).Contains(wall), Is.True);
            cache.Get(map.Grid, Vector2i.Zero); // Second occupied query admits a snapshot.
            var misses = cache.CacheMisses;
            for (var i = 0; i < 100; i++)
            {
                var transient = SEntMan.SpawnEntity(null, map.GridCoords);
                SEntMan.DeleteEntity(transient);
                Assert.That(cache.Get(map.Grid, Vector2i.Zero).Contains(wall), Is.True);
            }
            Assert.That(cache.CacheMisses, Is.EqualTo(misses), "Unanchored spawns/deletes must not evict terrain contacts.");
            maps.SetTile(map.Grid, new Vector2i(1, 0), Tile.Empty);
            Assert.That(cache.Get(map.Grid, Vector2i.Zero).Contains(wall), Is.True);
            Assert.That(cache.CacheMisses, Is.EqualTo(misses), "An unrelated tile change must preserve this tile.");
            Server.System<SharedTransformSystem>().Unanchor(wall);
            Assert.That(cache.Get(map.Grid, Vector2i.Zero).Contains(wall), Is.False);
            Assert.That(cache.CacheMisses, Is.EqualTo(misses + 1));
            TestContext.Progress.WriteLine($"PERF cache_churn unanchoredSpawnsAndDeletes=100 retainedHits=101 refreshes=1");
        });
    }

    [TestCase(1)]
    [TestCase(32)]
    public async Task CompareWarmMembershipAndContactSnapshotCosts(int population)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var cache = Server.System<AnchoredTileCacheSystem>();
            var maps = Server.System<SharedMapSystem>();
            for (var i = 0; i < population; i++)
                SEntMan.SpawnEntity("CMWallRock", map.GridCoords);
            var scratch = new List<EntityUid>();
            long Read(int path)
            {
                long sum = 0;
                for (var i = 0; i < 10000; i++)
                {
                    if (path == 0)
                    {
                        foreach (var entity in cache.Get(map.Grid, Vector2i.Zero)) sum += entity.Id;
                        continue;
                    }
                    var query = maps.GetAnchoredEntitiesEnumerator(map.Grid, map.Grid.Comp, Vector2i.Zero);
                    if (path == 1)
                    {
                        while (query.MoveNext(out var entity)) sum += entity!.Value.Id;
                    }
                    else
                    {
                        scratch.Clear();
                        while (query.MoveNext(out var entity)) scratch.Add(entity!.Value);
                        foreach (var entity in scratch) sum += entity.Id;
                    }
                }
                return sum;
            }
            var expected = Read(0);
            Assert.That(Read(1), Is.EqualTo(expected));
            Assert.That(Read(2), Is.EqualTo(expected));
            var samples = new double[3][];
            for (var path = 0; path < 3; path++) samples[path] = new double[5];
            for (var sample = 0; sample < 5; sample++)
            for (var offset = 0; offset < 3; offset++)
            {
                var path = (offset + sample) % 3;
                var started = Stopwatch.GetTimestamp();
                var actual = Read(path);
                samples[path][sample] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                Assert.That(actual, Is.EqualTo(expected));
            }
            foreach (var times in samples) Array.Sort(times);
            TestContext.Progress.WriteLine($"PERF warm_anchor population={population} lookups=10000 medianCachedMs={samples[0][2]:F3} medianDirectMs={samples[1][2]:F3} medianSnapshotMs={samples[2][2]:F3}");
        });
    }

    [Test]
    public async Task CachedMembershipTracksAnchorMoveDeletionAndKeepsOldReadersIntact()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var cache = Server.System<AnchoredTileCacheSystem>();
            var transform = Server.System<SharedTransformSystem>();
            var maps = Server.System<SharedMapSystem>();
            maps.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(1, 0), map.Tile.Tile);
            var uid = SEntMan.SpawnEntity("CMWallRock", map.GridCoords);
            // Retain the returned snapshot itself: copying it would hide mutations.
            var original = cache.Get(map.Grid, Vector2i.Zero);
            Assert.That(original, Does.Contain(uid));
            cache.Get(map.Grid, Vector2i.Zero);
            var misses = cache.CacheMisses;
            var start = Stopwatch.GetTimestamp();
            var matches = 0;
            for (var i = 0; i < 10000; i++)
                if (cache.Get(map.Grid, Vector2i.Zero).Contains(uid)) matches++;
            var cachedMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            Assert.That(matches, Is.EqualTo(10000));
            matches = 0;
            start = Stopwatch.GetTimestamp();
            for (var i = 0; i < 10000; i++)
            {
                var entities = maps.GetAnchoredEntitiesEnumerator(map.Grid, map.Grid.Comp, Vector2i.Zero);
                while (entities.MoveNext(out var entity))
                    if (entity == uid) matches++;
            }
            var directMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            Assert.That(matches, Is.EqualTo(10000));
            TestContext.Progress.WriteLine($"PERF anchor_cache lookups=10000 cachedMs={cachedMs:F3} directMs={directMs:F3}");
            Assert.That(cache.CacheMisses, Is.EqualTo(misses));

            transform.Unanchor(uid);
            Assert.That(cache.Get(map.Grid, Vector2i.Zero).Contains(uid), Is.False);
            Assert.That(original, Does.Contain(uid), "Invalidation must not mutate an active snapshot.");
            transform.SetCoordinates(uid, new EntityCoordinates(map.Grid.Owner, new Vector2(1.5f, 0.5f)));
            transform.AnchorEntity(uid);
            Assert.That(cache.Get(map.Grid, new Vector2i(1, 0)).Contains(uid), Is.True);
            Assert.That(cache.Get(map.Grid, Vector2i.Zero).Contains(uid), Is.False);
            SEntMan.DeleteEntity(uid);
            Assert.That(cache.Get(map.Grid, new Vector2i(1, 0)).Contains(uid), Is.False);
            Assert.That(original, Does.Contain(uid), "Later queries and deletion must preserve the retained snapshot.");
        });
    }
    [Test]
    public async Task ClientAnchoredStateMoveDoesNotRetainIntermediateMembership()
    {
        var map = await Pair.CreateTestMap();
        await Client.WaitAssertion(() =>
        {
            var maps = Client.System<SharedMapSystem>();
            var transforms = Client.System<SharedTransformSystem>();
            var cache = Client.System<AnchoredTileCacheSystem>();
            var grid = new Entity<Robust.Shared.Map.Components.MapGridComponent>(map.CGridUid,
                CEntMan.GetComponent<Robust.Shared.Map.Components.MapGridComponent>(map.CGridUid));
            maps.SetTile(grid, new Vector2i(1, 0), map.Tile.Tile);
            var wall = CEntMan.SpawnEntity("CMWallRock", map.CGridCoords);
            var xform = CEntMan.GetComponent<TransformComponent>(wall);
            cache.Update(0);
            Assert.That(cache.Get(grid, Vector2i.Zero).Contains(wall), Is.True);
            Assert.That(cache.Get(grid, new Vector2i(1, 0)).Contains(wall), Is.False);
            var callbacks = 0;
            void DuringMove(ref MoveEvent ev)
            {
                if (ev.Sender != wall) return;
                callbacks++;
                // The engine has removed the old membership but not added the destination yet.
                Assert.That(cache.Get(grid, new Vector2i(1, 0)).Contains(wall), Is.False);
            }
            transforms.OnGlobalMoveEvent += DuringMove;
            try
            {
                var type = typeof(TransformComponent).Assembly.GetType("Robust.Shared.GameObjects.TransformComponentState")!;
                var state = (IComponentState) Activator.CreateInstance(type,
                    new object[] { new Vector2(1.5f, 0.5f), xform.LocalRotation,
                        CEntMan.GetNetEntity(grid.Owner), xform.NoLocalRotation, true })!;
                var ev = new ComponentHandleState(state, null);
                CEntMan.EventBus.RaiseComponentEvent(wall, xform, ref ev);
                Assert.That(callbacks, Is.EqualTo(1));
                Assert.That(cache.Get(grid, Vector2i.Zero).Contains(wall), Is.False);
                Assert.That(cache.Get(grid, new Vector2i(1, 0)).Contains(wall), Is.True,
                    "A query from inside MoveEvent must not poison the completed state.");
                cache.Update(0);
                Assert.That(cache.Get(grid, new Vector2i(1, 0)).Contains(wall), Is.True);
                cache.Get(grid, new Vector2i(1, 0));
                var misses = cache.CacheMisses;
                Assert.That(cache.Get(grid, new Vector2i(1, 0)).Contains(wall), Is.True);
                Assert.That(cache.CacheMisses, Is.EqualTo(misses), "The following update can cache stable membership.");
            }
            finally
            {
                transforms.OnGlobalMoveEvent -= DuringMove;
                CEntMan.DeleteEntity(wall);
            }
        });
    }

    [Test]
    public async Task SparseQueriesAndUnrelatedAnchoringPreserveHotSnapshots()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var cache = Server.System<AnchoredTileCacheSystem>();
            var maps = Server.System<SharedMapSystem>();
            var wall = SEntMan.SpawnEntity("CMWallRock", map.GridCoords);
            cache.Get(map.Grid, Vector2i.Zero);
            var reader = cache.Get(map.Grid, Vector2i.Zero);
            var count = cache.CachedTileCount;
            var hits = cache.CacheHits;
            for (var i = 0; i < 5000; i++)
                Assert.That(cache.Get(map.Grid, new Vector2i(i + 10, 0)), Is.Empty);
            Assert.That(cache.CachedTileCount, Is.EqualTo(count));
            maps.SetTile(map.Grid, new Vector2i(1, 0), map.Tile.Tile);
            var other = SEntMan.SpawnEntity("CMWallRock", new EntityCoordinates(map.Grid, new Vector2(1.5f, 0.5f)));
            Assert.That(cache.Get(map.Grid, Vector2i.Zero), Is.EqualTo(reader));
            Assert.That(cache.CacheHits, Is.EqualTo(hits + 1), "An anchor on another tile must preserve this resident.");
            Assert.That(reader, Does.Contain(wall));
            Assert.That(reader, Does.Not.Contain(other));
        });
    }

    [Test]
    public async Task CapacityEvictsOneResidentAndPreservesActiveReaders()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var cache = Server.System<AnchoredTileCacheSystem>();
            var maps = Server.System<SharedMapSystem>();
            var transforms = Server.System<SharedTransformSystem>();
            // Keep the fixture connected while constructing it; isolated floor additions
            // can be split onto a different grid by the map systems.
            for (var y = 0; y < 43; y++)
            for (var x = 0; x < 100; x++)
                maps.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);
            var hot = SEntMan.SpawnEntity("CMWallRock", map.GridCoords);
            cache.Get(map.Grid, Vector2i.Zero);
            var reader = cache.Get(map.Grid, Vector2i.Zero);
            var evictions = cache.CacheEvictions;
            for (var i = 1; i <= 4100; i++)
            {
                var tile = new Vector2i(i % 100, i / 100 + 1);
                var uid = SEntMan.SpawnEntity(null, new EntityCoordinates(map.Grid, tile + new Vector2(0.5f)));
                Assert.That(transforms.AnchorEntity(uid), Is.True);
                var direct = maps.GetAnchoredEntitiesEnumerator(map.Grid, map.Grid.Comp, tile);
                var present = false;
                while (direct.MoveNext(out var member)) present |= member == uid;
                Assert.That(present, Is.True, $"Fixture membership at index {i}, tile {tile}, actual {SEntMan.GetComponent<TransformComponent>(uid).Coordinates}");
                cache.Get(map.Grid, tile);
                Assert.That(cache.Get(map.Grid, tile), Does.Contain(uid));
                cache.Get(map.Grid, Vector2i.Zero); // Keep the active tile recent.
            }
            Assert.That(cache.CachedTileCount, Is.EqualTo(4096));
            Assert.That(cache.CacheEvictions - evictions, Is.GreaterThanOrEqualTo(5));
            Assert.That(cache.AdmissionCount, Is.LessThanOrEqualTo(4096));
            var misses = cache.CacheMisses;
            Assert.That(cache.Get(map.Grid, Vector2i.Zero), Is.EqualTo(reader));
            Assert.That(cache.CacheMisses, Is.EqualTo(misses));
            Assert.That(reader, Does.Contain(hot));
        });
    }
}
