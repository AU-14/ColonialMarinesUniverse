using Content.Shared._RMC14.Storage;
using Content.Shared.Storage;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._RMC14.Storage;

[TestFixture]
[TestOf(typeof(RMCStorageSystem))]
public sealed class RMCStorageFillTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          parent: BaseItem
          id: RMCStorageFillTestItem

        - type: entity
          parent: BaseStorageItem
          id: RMCStorageFillTestContainer
          components:
          - type: Storage
            maxItemSize: Huge
            grid:
            - 0,0,1,1
          - type: FixedItemSizeStorage
            size: 2,2
          - type: StorageFill
            contents:
            - id: RMCStorageFillTestItem
              amount: 2
        """;

    [Test]
    public async Task ExpandingGridRefreshesOccupiedCells()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid storage = default;
        await server.WaitPost(() =>
            storage = server.EntMan.SpawnEntity("RMCStorageFillTestContainer", map.GridCoords));

        await server.WaitAssertion(() =>
        {
            var component = server.EntMan.GetComponent<StorageComponent>(storage);

            Assert.Multiple(() =>
            {
                Assert.That(component.Container.ContainedEntities, Has.Count.EqualTo(2));
                Assert.That(component.StoredItems, Has.Count.EqualTo(2));
            });
        });

        await pair.CleanReturnAsync();
    }
}
