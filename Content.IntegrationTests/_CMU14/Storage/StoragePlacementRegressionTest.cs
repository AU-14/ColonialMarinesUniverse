using Content.IntegrationTests.Fixtures;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Storage;

[TestFixture]
[TestOf(typeof(SharedStorageSystem))]
public sealed class StoragePlacementRegressionTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: CMUStoragePlacementHost
          components:
          - type: Storage
            maxItemSize: Huge
            grid:
            - 0,0,1,0

        - type: entity
          id: CMUStoragePlacementItem
          components:
          - type: Item
            size: Tiny
            shape:
            - 0,0,0,1
        """;

    [Test]
    public async Task ContainerInsertionUsesTheRotationAcceptedByPreflight()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var host = SEntMan.SpawnEntity("CMUStoragePlacementHost", map.GridCoords);
            var item = SEntMan.SpawnEntity("CMUStoragePlacementItem", map.GridCoords);
            var storage = SEntMan.GetComponent<StorageComponent>(host);
            var system = SEntMan.System<SharedStorageSystem>();
            var containers = SEntMan.System<SharedContainerSystem>();

            Assert.That(system.TryGetAvailableGridSpace(host, item, out var expected), Is.True);
            Assert.That(expected!.Value.Rotation, Is.Not.EqualTo(Angle.Zero),
                "The item can only fit when rotated into this one-row storage.");
            Assert.That(containers.CanInsert(item, storage.Container), Is.True);
            Assert.That(containers.Insert(item, storage.Container), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(storage.Container.ContainedEntities, Does.Contain(item));
                Assert.That(storage.StoredItems[item], Is.EqualTo(expected.Value));
                Assert.That(system.ItemFitsInGridLocation(item, host, storage.StoredItems[item]), Is.True);
            });

            var extra = SEntMan.SpawnEntity("CMUStoragePlacementItem", map.GridCoords);
            Assert.That(containers.Insert(extra, storage.Container), Is.False,
                "Normal insertion must still reject an actually full storage before mutation.");
            Assert.That(storage.StoredItems.Keys, Is.EquivalentTo(new[] { item }));
            SEntMan.DeleteEntity(extra);
            SEntMan.DeleteEntity(host);
        });
    }

    [Test]
    public async Task EggCartonMapInitializationRetainsAllTwelveEggs()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var carton = SEntMan.SpawnEntity("FoodContainerEgg", map.GridCoords);
            var storage = SEntMan.GetComponent<StorageComponent>(carton);
            Assert.Multiple(() =>
            {
                Assert.That(storage.Container.ContainedEntities, Has.Count.EqualTo(12));
                Assert.That(storage.StoredItems.Keys, Is.EquivalentTo(storage.Container.ContainedEntities));
                Assert.That(storage.StoredItems.Values.Any(location => location.Rotation != Angle.Zero), Is.True);
            });
            foreach (var egg in storage.Container.ContainedEntities)
            {
                Assert.That(SEntMan.GetComponent<MetaDataComponent>(egg).EntityPrototype!.ID, Is.EqualTo("FoodEgg"));
                Assert.That(SEntMan.System<SharedStorageSystem>()
                    .ItemFitsInGridLocation(egg, carton, storage.StoredItems[egg]), Is.True);
            }

            SEntMan.DeleteEntity(carton);
        });
    }

    [Test]
    public async Task GroceriesFridgeInitializesItsNestedEggCarton()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var fridge = SEntMan.SpawnEntity("RMCLockerFridgeGroceries", map.GridCoords);
            var manager = SEntMan.GetComponent<ContainerManagerComponent>(fridge);
            var carton = manager.Containers.Values.SelectMany(container => container.ContainedEntities)
                .Single(uid => SEntMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID == "FoodContainerEgg");
            var storage = SEntMan.GetComponent<StorageComponent>(carton);
            Assert.That(storage.Container.ContainedEntities, Has.Count.EqualTo(12));
            Assert.That(storage.StoredItems.Keys, Is.EquivalentTo(storage.Container.ContainedEntities));
            SEntMan.DeleteEntity(fridge);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ForcedInsertionReconcilesAfterTransaction(bool freeSpace)
    {
        var map = await Pair.CreateTestMap();
        EntityUid host = default;
        EntityUid extra = default;
        StorageComponent storage = null!;
        var containers = SEntMan.System<SharedContainerSystem>();
        await Server.WaitAssertion(() =>
        {
            host = SEntMan.SpawnEntity("CMUStoragePlacementHost", map.GridCoords);
            var first = SEntMan.SpawnEntity("CMUStoragePlacementItem", map.GridCoords);
            extra = SEntMan.SpawnEntity("CMUStoragePlacementItem", map.GridCoords);
            storage = SEntMan.GetComponent<StorageComponent>(host);

            Assert.That(containers.Insert(first, storage.Container), Is.True);
            Assert.That(containers.Insert(extra, storage.Container, force: true), Is.True);
            Assert.That(storage.Container.Contains(extra), Is.True,
                "An insertion callback must not remove the item before its transaction returns.");
            Assert.That(SEntMan.GetComponent<MetaDataComponent>(extra).Flags.HasFlag(MetaDataFlags.InContainer), Is.True);
            Assert.That(storage.StoredItems.ContainsKey(extra), Is.False);
            if (freeSpace)
                SEntMan.DeleteEntity(first);
        });

        await Server.WaitRunTicks(1);
        await Server.WaitAssertion(() =>
        {
            Assert.That(storage.Container.Contains(extra), Is.EqualTo(freeSpace));
            Assert.That(storage.StoredItems.ContainsKey(extra), Is.EqualTo(freeSpace),
                "Deferred reconciliation must reuse space freed before the next tick.");
            Assert.That(SEntMan.GetComponent<MetaDataComponent>(extra).Flags.HasFlag(MetaDataFlags.InContainer),
                Is.EqualTo(freeSpace));
            Assert.That(storage.StoredItems.Keys, Is.EquivalentTo(storage.Container.ContainedEntities));
            SEntMan.DeleteEntity(extra);
            SEntMan.DeleteEntity(host);
        });
    }

    [Test]
    public async Task DeferredReconciliationDoesNotRemoveItemFromItsNewContainer()
    {
        var map = await Pair.CreateTestMap();
        EntityUid original = default;
        EntityUid destination = default;
        EntityUid item = default;
        var containers = SEntMan.System<SharedContainerSystem>();
        await Server.WaitAssertion(() =>
        {
            original = SEntMan.SpawnEntity("CMUStoragePlacementHost", map.GridCoords);
            destination = SEntMan.SpawnEntity("CMUStoragePlacementHost", map.GridCoords);
            var blocker = SEntMan.SpawnEntity("CMUStoragePlacementItem", map.GridCoords);
            item = SEntMan.SpawnEntity("CMUStoragePlacementItem", map.GridCoords);
            var originalStorage = SEntMan.GetComponent<StorageComponent>(original);
            var destinationStorage = SEntMan.GetComponent<StorageComponent>(destination);
            Assert.That(containers.Insert(blocker, originalStorage.Container), Is.True);
            Assert.That(containers.Insert(item, originalStorage.Container, force: true), Is.True);
            Assert.That(containers.Insert(item, destinationStorage.Container), Is.True);
        });

        await Server.WaitRunTicks(1);
        await Server.WaitAssertion(() =>
        {
            var destinationStorage = SEntMan.GetComponent<StorageComponent>(destination);
            Assert.That(destinationStorage.Container.Contains(item), Is.True);
            Assert.That(destinationStorage.StoredItems.ContainsKey(item), Is.True);
            SEntMan.DeleteEntity(original);
            SEntMan.DeleteEntity(destination);
        });
    }
}
