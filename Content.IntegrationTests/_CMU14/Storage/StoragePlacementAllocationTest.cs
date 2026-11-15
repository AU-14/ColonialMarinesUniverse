using Content.IntegrationTests.Fixtures;
using Content.Shared.DoAfter;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Storage.Components;
using Content.Shared.Item;
using Content.Shared.SmartFridge;
using Robust.Shared.Containers;

namespace Content.IntegrationTests.CMU14.Storage;

[TestFixture]
public sealed class StoragePlacementAllocationTest : GameTest
{
    [Test]
    public async Task DumpingToFilteredTargetCompactsTheItemsItRejects()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var host = SEntMan.SpawnEntity("CMUStoragePlacementPerfHost", map.GridCoords);
            SEntMan.AddComponent<DumpableComponent>(host);
            var user = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            var target = SEntMan.SpawnEntity("CMUStoragePlacementFilteredTarget", map.GridCoords);
            var fridge = SEntMan.GetComponent<SmartFridgeComponent>(target);
            var containers = Server.System<SharedContainerSystem>();
            var destination = containers.EnsureContainer<Container>(target, fridge.Container);
            var storage = SEntMan.GetComponent<StorageComponent>(host);
            var items = new List<EntityUid>();
            for (var i = 0; i < 4; i++)
            {
                var item = SEntMan.SpawnEntity("CMUStoragePlacementPerfItem", map.GridCoords);
                if (i % 2 == 0) SEntMan.AddComponent<DumpableComponent>(item);
                items.Add(item);
                Assert.That(containers.Insert(item, storage.Container), Is.True);
            }

            var args = new DoAfterArgs(SEntMan, user, TimeSpan.Zero, new DumpableDoAfterEvent(), host, target: target);
            Assert.That(Server.System<SharedDoAfterSystem>().TryStartDoAfter(args), Is.True);
            Assert.That(destination.ContainedEntities, Is.EquivalentTo(new[] { items[0], items[2] }));
            Assert.That(storage.Container.ContainedEntities, Is.EquivalentTo(new[] { items[1], items[3] }));
            Assert.That(storage.StoredItems[items[1]].Position, Is.EqualTo(new Vector2i(0, 0)));
            Assert.That(storage.StoredItems[items[3]].Position, Is.EqualTo(new Vector2i(1, 0)));
            Assert.That(containers.Insert(items[0], storage.Container), Is.True, "Compaction must release the old occupied cells.");
            SEntMan.DeleteEntity(target);
            SEntMan.DeleteEntity(host);
            SEntMan.DeleteEntity(user);
        });
    }

    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: CMUStoragePlacementFilteredTarget
          components:
          - type: SmartFridge
            whitelist:
              components:
              - Dumpable

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
