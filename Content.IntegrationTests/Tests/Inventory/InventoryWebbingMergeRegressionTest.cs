#pragma warning disable RA0002 // Integration regression intentionally inspects restricted component state.

using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Webbing;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Inventory;

[TestFixture]
[TestOf(typeof(InventorySystem))]
[TestOf(typeof(SharedWebbingSystem))]
public sealed class InventoryWebbingMergeRegressionTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: inventoryTemplate
  id: InventoryWebbingMergeFull
  slots:
  - name: obsolete
    slotTexture: pocket
    slotFlags: POCKET
    uiWindowPos: 0,0
    strippingWindowPos: 0,0
    displayName: Obsolete
  - name: retained
    slotTexture: pocket
    slotFlags: POCKET
    uiWindowPos: 1,0
    strippingWindowPos: 1,0
    displayName: Retained

- type: inventoryTemplate
  id: InventoryWebbingMergeReduced
  slots:
  - name: retained
    slotTexture: pocket
    slotFlags: POCKET
    uiWindowPos: 0,0
    strippingWindowPos: 0,0
    displayName: Retained

- type: entity
  id: InventoryWebbingMergeInventory
  components:
  - type: Inventory
    templateId: InventoryWebbingMergeFull
  - type: InventoryWebbingMergeProbe

- type: entity
  parent: BaseItem
  id: InventoryWebbingMergeClothing
  components:
  - type: Item
    size: Small
  - type: WebbingClothing

- type: entity
  parent: BaseItem
  id: InventoryWebbingMergeWebbing
  components:
  - type: Item
    size: Large
  - type: Storage
    grid:
    - 0,0,3,1
  - type: Webbing
    components:
    - type: Storage
      grid:
      - 0,0,3,1

- type: entity
  parent: BaseItem
  id: InventoryWebbingMergeStoredItem
  components:
  - type: Item
    size: Tiny

- type: entity
  parent: BaseItem
  id: InventoryWebbingMergeEquippedItem
";

    private static readonly MethodInfo DetachMethod = typeof(SharedWebbingSystem).GetMethod(
        "Detach",
        BindingFlags.Instance | BindingFlags.NonPublic)!;

    [Test]
    public async Task WebbingAttachAndDetachTransferStorageContentsAndRestoreClothing()
    {
        var map = await Pair.CreateTestMap();
        EntityUid clothing = default;
        EntityUid webbing = default;
        EntityUid stored = default;
        EntityUid user = default;

        try
        {
            await Server.WaitPost(() =>
            {
                var storage = Server.System<SharedStorageSystem>();
                var webbingSystem = Server.System<Content.Server._RMC14.Webbing.WebbingSystem>();
                clothing = SEntMan.SpawnEntity("InventoryWebbingMergeClothing", map.GridCoords);
                webbing = SEntMan.SpawnEntity("InventoryWebbingMergeWebbing", map.GridCoords);
                stored = SEntMan.SpawnEntity("InventoryWebbingMergeStoredItem", map.GridCoords);
                user = SEntMan.SpawnEntity("MobHuman", map.GridCoords);

                Assert.That(storage.Insert(webbing, stored, out _, playSound: false), Is.True);
                var clothingComp = SEntMan.GetComponent<WebbingClothingComponent>(clothing);
                Assert.That(webbingSystem.Attach((clothing, clothingComp), webbing, user, out var handled), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(handled, Is.True);
                    Assert.That(clothingComp.Webbing, Is.EqualTo(webbing),
                        "container insertion must publish the attached webbing UID immediately");
                    Assert.That(SEntMan.HasComponent<StorageComponent>(clothing), Is.True,
                        "the webbing component registry is applied to clothing before deferred content transfer");
                    Assert.That(SEntMan.GetComponent<ItemComponent>(clothing).Size.ToString(), Is.EqualTo("Large"));
                    Assert.That(SEntMan.GetComponent<WebbingClothingComponent>(clothing).UnequippedSize?.ToString(),
                        Is.EqualTo("Small"));
                });
            });

            await Server.WaitRunTicks(3);
            await Server.WaitAssertion(() =>
            {
                var clothingStorage = SEntMan.GetComponent<StorageComponent>(clothing);
                var webbingStorage = SEntMan.GetComponent<StorageComponent>(webbing);
                Assert.Multiple(() =>
                {
                    Assert.That(clothingStorage.StoredItems.ContainsKey(stored), Is.True,
                        "deferred transfer moves existing contents into the clothing's ordinary Storage");
                    Assert.That(webbingStorage.StoredItems.ContainsKey(stored), Is.False);
                });

                var system = Server.System<Content.Server._RMC14.Webbing.WebbingSystem>();
                var clothingComponent = SEntMan.GetComponent<WebbingClothingComponent>(clothing);
                var clothingEntity = new Entity<WebbingClothingComponent>(clothing, clothingComponent);
                DetachMethod.Invoke(system, new object?[] { clothingEntity, user });
                Assert.That(clothingComponent.Webbing, Is.Null,
                    "container removal must clear the networked webbing UID immediately");
            });

            await Server.WaitRunTicks(3);
            await Server.WaitAssertion(() =>
            {
                var webbingStorage = SEntMan.GetComponent<StorageComponent>(webbing);
                var clothingComponent = SEntMan.GetComponent<WebbingClothingComponent>(clothing);
                Assert.Multiple(() =>
                {
                    Assert.That(webbingStorage.StoredItems.ContainsKey(stored), Is.True,
                        "detach returns clothing contents to the webbing before removing transferred components");
                    Assert.That(SEntMan.HasComponent<StorageComponent>(clothing), Is.False);
                    Assert.That(clothingComponent.Webbing, Is.Null);
                    Assert.That(clothingComponent.UnequippedSize, Is.Null);
                    Assert.That(SEntMan.GetComponent<ItemComponent>(clothing).Size.ToString(), Is.EqualTo("Small"));
                });
            });
        }
        finally
        {
            // GameTest owns entity cleanup. Deleting a container and its child in the same server tick can make the
            // client process both deletions while it is still detaching the live child collection.
        }
    }

    [Test]
    public async Task TemplateSwitchEmptiesObsoleteContainerWithoutDeletingItemAndRaisesOnce()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var inventorySystem = Server.System<InventorySystem>();
            var containerSystem = Server.System<SharedContainerSystem>();
            _ = Server.System<InventoryWebbingMergeProbeSystem>();
            var inventory = SEntMan.SpawnEntity("InventoryWebbingMergeInventory", map.GridCoords);
            var equipped = SEntMan.SpawnEntity("InventoryWebbingMergeEquippedItem", map.GridCoords);

            try
            {
                var inventoryComponent = SEntMan.GetComponent<InventoryComponent>(inventory);
                var probe = SEntMan.GetComponent<InventoryWebbingMergeProbeComponent>(inventory);
                Assert.That(inventorySystem.TryGetSlotContainer(
                    inventory,
                    "obsolete",
                    out var obsolete,
                    out _,
                    inventoryComponent), Is.True);
                Assert.That(containerSystem.Insert(equipped, obsolete), Is.True);
                probe.TemplateUpdates = 0;

                inventorySystem.SetTemplateId((inventory, inventoryComponent), "InventoryWebbingMergeReduced");

                Assert.Multiple(() =>
                {
                    Assert.That(probe.TemplateUpdates, Is.EqualTo(1));
                    Assert.That(SEntMan.EntityExists(equipped), Is.True,
                        "the equipped item must survive removal of its obsolete slot");
                    Assert.That(containerSystem.TryGetContainingContainer(equipped, out _), Is.False,
                        "the obsolete container is emptied before shutdown");
                    Assert.That(containerSystem.TryGetContainer(inventory, "obsolete", out _), Is.False);
                    Assert.That(inventorySystem.TryGetSlotContainer(
                        inventory,
                        "retained",
                        out _,
                        out _,
                        inventoryComponent), Is.True);
                });
            }
            finally
            {
                SEntMan.DeleteEntity(equipped);
                SEntMan.DeleteEntity(inventory);
            }
        });
    }
}

[RegisterComponent]
public sealed partial class InventoryWebbingMergeProbeComponent : Component
{
    public int TemplateUpdates;
}

public sealed class InventoryWebbingMergeProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<InventoryWebbingMergeProbeComponent, InventoryTemplateUpdated>(OnTemplateUpdated);
    }

    private static void OnTemplateUpdated(
        Entity<InventoryWebbingMergeProbeComponent> entity,
        ref InventoryTemplateUpdated args)
    {
        entity.Comp.TemplateUpdates++;
    }
}

#pragma warning restore RA0002
