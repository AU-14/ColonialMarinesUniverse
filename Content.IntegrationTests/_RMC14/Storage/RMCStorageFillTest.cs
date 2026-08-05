using System;
using System.Collections.Generic;
using System.Linq;
using Content.Shared._RMC14.Item;
using Content.Shared._RMC14.Storage;
using Content.Shared.Prototypes;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._RMC14.Storage;

[TestFixture]
[TestOf(typeof(RMCStorageSystem))]
public sealed class RMCStorageFillTest
{
    private const string HonorGuardKitPrototype = "RMCKitHonorGuard";
    private const string SppMrePrototype = "RMCMRESPP";
    private const string TseMrePrototype = "RMCMRETSE";

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

    [TestCase(HonorGuardKitPrototype)]
    [TestCase(SppMrePrototype)]
    [TestCase(TseMrePrototype)]
    public async Task FixedSizeStorageHasRoomForEveryPossibleFillItem(string prototypeId)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypeManager = server.ResolveDependency<IPrototypeManager>();
            var componentFactory = server.ResolveDependency<IComponentFactory>();
            var prototype = prototypeManager.Index<EntityPrototype>(prototypeId);

            Assert.That(prototype.TryComp<StorageComponent>(out var storage, componentFactory), Is.True);
            Assert.That(prototype.TryComp<StorageFillComponent>(out var fill, componentFactory), Is.True);
            Assert.That(prototype.TryComp<FixedItemSizeStorageComponent>(out var fixedSize, componentFactory), Is.True);

            var requiredSlots = 0;
            var groups = new Dictionary<string, int>();
            foreach (var entry in fill!.Contents)
            {
                var maximumAmount = Math.Max(entry.Amount, entry.MaxAmount);
                if (string.IsNullOrEmpty(entry.GroupId))
                {
                    requiredSlots += maximumAmount;
                    continue;
                }

                groups[entry.GroupId] = Math.Max(maximumAmount, groups.GetValueOrDefault(entry.GroupId));
            }

            requiredSlots += groups.Values.Sum();
            var requiredArea = requiredSlots * fixedSize!.Size.X * fixedSize.Size.Y;

            Assert.That(storage!.Grid.GetArea(), Is.GreaterThanOrEqualTo(requiredArea),
                $"{prototypeId} needs room for {requiredSlots} possible fill items.");
        });

        await pair.CleanReturnAsync();
    }
}
