using Content.IntegrationTests.Fixtures;
using Content.Server.Station.Systems;
using Content.Shared._RMC14.Storage;
using Content.Shared.Containers;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Roles;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Containers;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.Roles;

[TestFixture]
[TestOf(typeof(StationSpawningSystem))]
public sealed class StationSpawningMergeRegressionTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: StationSpawningMergeBackpack
          parent: ClothingBackpack
          components:
          - type: Storage
            maxItemSize: Huge
            grid:
            - 0,0,0,0
          - type: StationSpawningMergeProbe

        - type: entity
          id: StationSpawningMergeLargeItem
          components:
          - type: Item
            size: Large

        - type: startingGear
          id: StationSpawningMergeGear
          equipment:
            back: StationSpawningMergeBackpack
            shoes: ClothingShoesColorBlack
          storage:
            back:
            - StationSpawningMergeLargeItem
        """;

    [Test]
    public async Task EquipStartingGearExpandsBackStorageBeforeInsertAndHonorsRaiseEvent()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var spawning = Server.System<StationSpawningSystem>();
            var inventory = Server.System<InventorySystem>();
            var gear = SProtoMan.Index<StartingGearPrototype>("StationSpawningMergeGear");

            var wearer = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            var wearerProbe = SEntMan.AddComponent<StationSpawningMergeProbeComponent>(wearer);
            spawning.EquipStartingGear(wearer, gear);

            Assert.That(inventory.TryGetSlotEntity(wearer, "back", out var back), Is.True);
            Assert.That(back, Is.Not.Null);
            Assert.That(inventory.TryGetSlotEntity(wearer, "shoes", out var shoes), Is.True,
                "ordinary starting equipment must still be equipped");
            Assert.That(shoes, Is.Not.Null);
            Assert.That(PrototypeId(shoes!.Value), Is.EqualTo("ClothingShoesColorBlack"));

            var storage = SEntMan.GetComponent<StorageComponent>(back!.Value);
            var storageProbe = SEntMan.GetComponent<StationSpawningMergeProbeComponent>(back.Value);
            var contained = storage.Container.ContainedEntities;
            Assert.Multiple(() =>
            {
                Assert.That(wearerProbe.StartingGearEvents, Is.EqualTo(1));
                Assert.That(storageProbe.FillEvents, Is.EqualTo(1));
                Assert.That(storageProbe.WasContainedAtFill, Is.False,
                    "the CM fill event must precede SharedStorage insertion");
                Assert.That(storageProbe.CouldInsertAtFill, Is.False,
                    "the inherited one-cell grid must be too small before RMC expansion");
                Assert.That(storageProbe.GridAtFill, Is.EqualTo(new[] { new Box2i(0, 0, 0, 0) }));
                Assert.That(storage.Grid, Is.Not.EqualTo(storageProbe.GridAtFill),
                    "RMCStorage must expand the inherited storage exactly for the item");
                Assert.That(contained, Has.Count.EqualTo(1));
                Assert.That(PrototypeId(contained.Single()), Is.EqualTo("StationSpawningMergeLargeItem"));
                Assert.That(storage.StoredItems.Keys, Is.EquivalentTo(contained));
            });

            var silentWearer = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            var silentProbe = SEntMan.AddComponent<StationSpawningMergeProbeComponent>(silentWearer);
            spawning.EquipStartingGear(silentWearer, gear, raiseEvent: false);
            Assert.That(silentProbe.StartingGearEvents, Is.Zero);

            SEntMan.DeleteEntity(wearer);
            SEntMan.DeleteEntity(silentWearer);
        });
    }

    private string PrototypeId(EntityUid uid)
    {
        return SEntMan.GetComponent<MetaDataComponent>(uid).EntityPrototype!.ID;
    }
}

[RegisterComponent]
public sealed partial class StationSpawningMergeProbeComponent : Component
{
    public int FillEvents;
    public int StartingGearEvents;
    public bool WasContainedAtFill;
    public bool CouldInsertAtFill;
    public Box2i[] GridAtFill = [];
}

public sealed class StationSpawningMergeProbeSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedStorageSystem _storage = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StationSpawningMergeProbeComponent, CMStorageItemFillEvent>(
            OnFill,
            before: [typeof(RMCStorageSystem)]);
        SubscribeLocalEvent<StationSpawningMergeProbeComponent, StartingGearEquippedEvent>(OnEquipped);
    }

    private void OnFill(Entity<StationSpawningMergeProbeComponent> ent, ref CMStorageItemFillEvent args)
    {
        ent.Comp.FillEvents++;
        ent.Comp.WasContainedAtFill = _containers.TryGetContainingContainer(args.Item.Owner, out _);
        ent.Comp.CouldInsertAtFill = _storage.CanInsert(
            ent.Owner,
            args.Item.Owner,
            out _,
            args.Storage,
            args.Item.Comp,
            ignoreStacks: true);
        ent.Comp.GridAtFill = args.Storage.Grid.ToArray();
    }

    private static void OnEquipped(
        Entity<StationSpawningMergeProbeComponent> ent,
        ref StartingGearEquippedEvent args)
    {
        ent.Comp.StartingGearEvents++;
    }
}
