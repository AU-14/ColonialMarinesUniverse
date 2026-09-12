using Content.IntegrationTests.Fixtures;
using Content.Shared.DoAfter;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Item;
using Robust.Shared.Containers;

namespace Content.IntegrationTests.CMU14.Storage;

[TestFixture]
public sealed class StoragePlacementAllocationTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: CMUStoragePlacementPerfHost
          components:
          - type: Storage
            maxItemSize: Huge
            grid:
            - 0,0,7,7

        - type: entity
          id: CMUStoragePlacementPerfItem
          components:
          - type: Item
            size: Tiny
            shape:
            - 0,0,0,0

        - type: entity
          parent: CMUStoragePlacementPerfHost
          id: CMUStoragePlacementSplitGrid
          components:
          - type: Storage
            grid:
            - -2,0,-1,0
            - 1,0,1,0

        - type: entity
          parent: CMUStoragePlacementSplitGrid
          id: CMUStoragePlacementFixedGrid
          components:
          - type: FixedItemSizeStorage
            size: 1,1

        - type: entity
          parent: CMUStoragePlacementPerfItem
          id: CMUStoragePlacementWideItem
          components:
          - type: Item
            shape:
            - 0,0,1,0
        """;

    [Test]
    public async Task NearlyFullStorageDoesNotRebuildEveryStoredShapeForEveryCandidate()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var host = SEntMan.SpawnEntity("CMUStoragePlacementPerfHost", map.GridCoords);
            var candidate = SEntMan.SpawnEntity("CMUStoragePlacementPerfItem", map.GridCoords);
            var storage = SEntMan.GetComponent<StorageComponent>(host);
            var containers = Server.System<SharedContainerSystem>();
            var system = Server.System<SharedStorageSystem>();
            try
            {
                for (var i = 0; i < 63; i++)
                {
                    var item = SEntMan.SpawnEntity("CMUStoragePlacementPerfItem", map.GridCoords);
                    Assert.That(containers.Insert(item, storage.Container), Is.True);
                }

                Assert.That(system.TryGetAvailableGridSpace(host, candidate, out var expected), Is.True);
                Assert.That(expected!.Value.Position, Is.EqualTo(new Vector2i(7, 7)));
                for (var i = 0; i < 8; i++)
                    system.TryGetAvailableGridSpace(host, candidate, out _);

                var before = GC.GetAllocatedBytesForCurrentThread();
                var found = 0;
                for (var i = 0; i < 32; i++)
                {
                    if (system.TryGetAvailableGridSpace(host, candidate, out var location) && location == expected)
                        found++;
                }
                var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                TestContext.Out.WriteLine($"32 nearly-full placement searches allocated {allocated} bytes");
                Assert.Multiple(() =>
                {
                    Assert.That(found, Is.EqualTo(32));
                    Assert.That(allocated, Is.LessThan(1024 * 1024),
                        "Placement must not allocate shapes again for each candidate cell and rotation.");
                });

                Assert.That(containers.Insert(candidate, storage.Container), Is.True);
                Assert.That(system.TryGetAvailableGridSpace(host, candidate, out var ownLocation), Is.True,
                    "An item already in storage must be allowed to reuse its own occupied cells.");
                Assert.That(ownLocation, Is.EqualTo(expected));
                var extra = SEntMan.SpawnEntity("CMUStoragePlacementPerfItem", map.GridCoords);
                try
                {
                    Assert.That(system.TryGetAvailableGridSpace(host, extra, out _), Is.False);
                }
                finally
                {
                    SEntMan.DeleteEntity(extra);
                }
            }
            finally
            {
                SEntMan.DeleteEntity(candidate);
                SEntMan.DeleteEntity(host);
            }
        });
    }

    [Test]
    public async Task AreaPickupIntoNearlyFullStorageKeepsAllocationsBounded()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var host = SEntMan.SpawnEntity("CMUStoragePlacementPerfHost", map.GridCoords);
            var user = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            var storage = SEntMan.GetComponent<StorageComponent>(host);
            var containers = Server.System<SharedContainerSystem>();
            var doAfters = Server.System<SharedDoAfterSystem>();
            var pickups = new List<EntityUid>();
            try
            {
                for (var i = 0; i < 53; i++)
                {
                    var item = SEntMan.SpawnEntity("CMUStoragePlacementPerfItem", map.GridCoords);
                    Assert.That(containers.Insert(item, storage.Container), Is.True);
                }

                // Warm the actual completion handler before measuring a full ten-item pickup.
                var warmItem = SEntMan.SpawnEntity("CMUStoragePlacementPerfItem", map.GridCoords);
                pickups.Add(warmItem);
                var warmEvent = new AreaPickupDoAfterEvent([SEntMan.GetNetEntity(warmItem)]);
                Assert.That(doAfters.TryStartDoAfter(new DoAfterArgs(SEntMan, user, TimeSpan.Zero, warmEvent, host)), Is.True);
                Assert.That(storage.Container.ContainedEntities.Count, Is.EqualTo(54));

                var entities = new List<NetEntity>();
                for (var i = 0; i < StorageComponent.AreaPickupLimit; i++)
                {
                    var item = SEntMan.SpawnEntity("CMUStoragePlacementPerfItem", map.GridCoords);
                    pickups.Add(item);
                    entities.Add(SEntMan.GetNetEntity(item));
                }

                var ev = new AreaPickupDoAfterEvent(entities);
                var args = new DoAfterArgs(SEntMan, user, TimeSpan.Zero, ev, host);
                var before = GC.GetAllocatedBytesForCurrentThread();
                var started = doAfters.TryStartDoAfter(args);
                var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                TestContext.Out.WriteLine($"Ten-item area pickup allocated {allocated} bytes");
                Assert.Multiple(() =>
                {
                    Assert.That(started, Is.True);
                    Assert.That(ev.Handled, Is.True);
                    Assert.That(storage.Container.ContainedEntities.Count, Is.EqualTo(64));
                    Assert.That(allocated, Is.LessThan(4 * 1024 * 1024),
                        "Completing a pickup must not rebuild stored shapes for each placement candidate.");
                });
            }
            finally
            {
                foreach (var item in pickups)
                    SEntMan.DeleteEntity(item);
                SEntMan.DeleteEntity(user);
                SEntMan.DeleteEntity(host);
            }
        });
    }

    [TestCase("CMUStoragePlacementSplitGrid", false)]
    [TestCase("CMUStoragePlacementFixedGrid", true)]
    public async Task PlacementPreservesHolesNegativeCoordinatesAndFixedSize(string prototype, bool fixedSize)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var host = SEntMan.SpawnEntity(prototype, map.GridCoords);
            var item = SEntMan.SpawnEntity("CMUStoragePlacementWideItem", map.GridCoords);
            var system = Server.System<SharedStorageSystem>();
            try
            {
                Assert.That(system.TryGetAvailableGridSpace(host, item, out var location), Is.True);
                Assert.That(location!.Value.Position, Is.EqualTo(new Vector2i(-2, 0)));
                Assert.Multiple(() =>
                {
                    Assert.That(system.ItemFitsInGridLocation(item, host, new Vector2i(-1, 0), Angle.Zero), Is.EqualTo(fixedSize));
                    Assert.That(system.ItemFitsInGridLocation(item, host, new Vector2i(0, 0), Angle.Zero), Is.False);
                    Assert.That(system.ItemFitsInGridLocation(item, host, new Vector2i(1, 0), Angle.Zero), Is.EqualTo(fixedSize));
                });

                // A new query must observe shape changes without retaining a stale occupancy snapshot.
                Server.System<SharedItemSystem>().SetShape(item, [new Box2i(0, 0, 0, 0)]);
                Assert.That(system.ItemFitsInGridLocation(item, host, new Vector2i(1, 0), Angle.Zero), Is.True);
            }
            finally
            {
                SEntMan.DeleteEntity(item);
                SEntMan.DeleteEntity(host);
            }
        });
    }

    [Test]
    public async Task SavedPlacementFallsBackWhenBlockedAndMovingDoesNotIgnoreOtherItems()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var host = SEntMan.SpawnEntity("CMUStoragePlacementPerfHost", map.GridCoords);
            var first = SEntMan.SpawnEntity("CMUStoragePlacementPerfItem", map.GridCoords);
            var second = SEntMan.SpawnEntity("CMUStoragePlacementPerfItem", map.GridCoords);
            var system = Server.System<SharedStorageSystem>();
            var containers = Server.System<SharedContainerSystem>();
            var storage = SEntMan.GetComponent<StorageComponent>(host);
            try
            {
                Assert.That(containers.Insert(first, storage.Container), Is.True);
                var saved = new ItemStorageLocation(Angle.Zero, new Vector2i(6, 6));
                Assert.That(system.TrySetItemStorageLocation(first, host, saved), Is.True);
                system.SaveItemLocation(host, first);
                Assert.That(containers.Remove(first, storage.Container), Is.True);
                Assert.That(system.TryGetAvailableGridSpace(host, second, out var preferred), Is.True);
                Assert.That(preferred, Is.EqualTo(saved));
                Assert.That(containers.Insert(first, storage.Container), Is.True);
                Assert.That(system.TryGetAvailableGridSpace(host, second, out var fallback), Is.True);
                Assert.That(fallback!.Value.Position, Is.EqualTo(Vector2i.Zero));
                Assert.That(containers.Insert(second, storage.Container), Is.True);
                Assert.That(system.TrySetItemStorageLocation(first, host, fallback.Value), Is.False);

                // Even with overlapping stored locations, ignoring the moving item must retain the other occupant.
                storage.StoredItems[second] = saved;
                Assert.That(system.ItemFitsInGridLocation(first, host, saved), Is.False);
                Assert.That(system.TryGetAvailableGridSpace(host, first, out var free), Is.True);
                Assert.That(free!.Value.Position, Is.EqualTo(Vector2i.Zero));
            }
            finally
            {
                SEntMan.DeleteEntity(first);
                SEntMan.DeleteEntity(second);
                SEntMan.DeleteEntity(host);
            }
        });
    }
}
