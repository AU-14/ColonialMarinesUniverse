using Content.IntegrationTests.Fixtures;
using Content.Shared.Maps;

namespace Content.IntegrationTests.Tests.Performance;

[TestFixture]
public sealed class AnchoredTileCacheTest : GameTest
{

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
            Assert.That(cache.Get(map.Grid, Vector2i.Zero), Does.Contain(uid));
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

}
