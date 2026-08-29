#pragma warning disable RA0002 // Integration regression intentionally inspects restricted component state.

using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Inventory;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Storage;

[TestFixture]
[TestOf(typeof(ItemSlotsSystem))]
public sealed class ItemSlotsMergeRegressionTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: ItemSlotsMergeOwner
  components:
  - type: ItemSlots
    slots:
      configured:
        whitelist:
          components:
          - Item
        blacklist:
          components:
          - Damageable
        insertSound: /Audio/Effects/pop.ogg
        ejectSound: /Audio/Effects/snap.ogg
        name: item-slots-merge-name
        startingItem: Crowbar
        locked: true
        disableEject: true
        insertOnInteract: false
        ejectOnInteract: true
        ejectOnUse: true
        insertVerbText: item-slots-merge-insert
        ejectVerbText: item-slots-merge-eject
        ejectOnDeconstruct: false
        ejectOnBreak: true
        whitelistFailPopup: item-slots-merge-whitelist
        lockedFailPopup: item-slots-merge-locked
        insertSuccessPopup: item-slots-merge-success
        swap: false
        priority: 7
      mutable:
        disableEject: false
        insertOnInteract: true
        priority: 2
      empty-first:
        priority: 1

- type: entity
  id: ItemSlotsMergeCMOwner
  components:
  - type: CMItemSlots
    count: 2
    slot:
      name: merge-pocket
      whitelist:
        components:
        - Item
      blacklist:
        components:
        - Damageable
      disableEject: true
      insertOnInteract: false
      priority: 4
    startingItems:
    - Crowbar
    - Wrench

- type: entity
  id: ItemSlotsMergeHardpointOwner
  components:
  - type: HardpointSlots
    slots:
    - id: protected
      hardpointType: Weapon
      disableEject: true
    - id: ordinary
      hardpointType: Support
      disableEject: false
";

    [Test]
    public async Task CopyPreservesEveryConfigurationFieldButNotRuntimeOwnership()
    {
        await Server.WaitAssertion(() =>
        {
            var owner = SEntMan.Spawn("ItemSlotsMergeOwner");
            var source = SEntMan.GetComponent<ItemSlotsComponent>(owner).Slots["configured"];
            Assert.That(source.ContainerSlot, Is.Not.Null);
            Assert.That(source.HasItem, Is.True);

            source.Local = false;
            var copy = new ItemSlot(source);

            Assert.Multiple(() =>
            {
                Assert.That(copy.Whitelist, Is.SameAs(source.Whitelist));
                Assert.That(copy.Blacklist, Is.SameAs(source.Blacklist));
                Assert.That(copy.InsertSound, Is.SameAs(source.InsertSound));
                Assert.That(copy.EjectSound, Is.SameAs(source.EjectSound));
                Assert.That(copy.Name, Is.EqualTo(source.Name));
                Assert.That(copy.StartingItem, Is.EqualTo(source.StartingItem));
                Assert.That(copy.Locked, Is.EqualTo(source.Locked));
                Assert.That(copy.DisableEject, Is.EqualTo(source.DisableEject));
                Assert.That(copy.InsertOnInteract, Is.EqualTo(source.InsertOnInteract));
                Assert.That(copy.EjectOnInteract, Is.EqualTo(source.EjectOnInteract));
                Assert.That(copy.EjectOnUse, Is.EqualTo(source.EjectOnUse));
                Assert.That(copy.InsertVerbText, Is.EqualTo(source.InsertVerbText));
                Assert.That(copy.EjectVerbText, Is.EqualTo(source.EjectVerbText));
                Assert.That(copy.EjectOnDeconstruct, Is.EqualTo(source.EjectOnDeconstruct));
                Assert.That(copy.EjectOnBreak, Is.EqualTo(source.EjectOnBreak));
                Assert.That(copy.WhitelistFailPopup, Is.EqualTo(source.WhitelistFailPopup));
                Assert.That(copy.LockedFailPopup, Is.EqualTo(source.LockedFailPopup));
                Assert.That(copy.InsertSuccessPopup, Is.EqualTo(source.InsertSuccessPopup));
                Assert.That(copy.Swap, Is.EqualTo(source.Swap));
                Assert.That(copy.Priority, Is.EqualTo(source.Priority));
                Assert.That(copy.ContainerSlot, Is.Null);
                Assert.That(copy.Item, Is.Null);
                Assert.That(copy.Local, Is.True,
                    "the copy constructor creates a fresh local registration");
            });
        });
    }

    [Test]
    public async Task PublicMutatorsDirtyBothOverloadsAndEmptySortUsesPriority()
    {
        var map = await Pair.CreateTestMap();
        NetEntity ownerNet = default;
        await Server.WaitAssertion(() =>
        {
            var owner = SEntMan.SpawnEntity("ItemSlotsMergeOwner", map.MapCoords);
            ownerNet = SEntMan.GetNetEntity(owner);
            var slots = SEntMan.GetComponent<ItemSlotsComponent>(owner);
            var mutable = slots.Slots["mutable"];
            var itemSlots = Server.System<ItemSlotsSystem>();

            itemSlots.SetDisableEject(owner, "mutable", true, slots);
            itemSlots.SetInsertOnInteract(owner, mutable, false, slots);

            var ordered = slots.Slots.Values.ToList();
            ordered.Sort(ItemSlotsSystem.SortEmpty);
            Assert.That(ordered.Select(slot => slot.ID), Is.EqualTo(new[]
            {
                "empty-first",
                "mutable",
                "configured",
            }));
        });
        await Pair.RunTicksSync(3);

        await Client.WaitAssertion(() =>
        {
            var owner = CEntMan.GetEntity(ownerNet);
            var mutable = CEntMan.GetComponent<ItemSlotsComponent>(owner).Slots["mutable"];
            Assert.Multiple(() =>
            {
                Assert.That(mutable.DisableEject, Is.True);
                Assert.That(mutable.InsertOnInteract, Is.False);
            });
        });

        await Server.WaitAssertion(() =>
        {
            var owner = SEntMan.GetEntity(ownerNet);
            var slots = SEntMan.GetComponent<ItemSlotsComponent>(owner);
            var mutable = slots.Slots["mutable"];
            var itemSlots = Server.System<ItemSlotsSystem>();
            itemSlots.SetDisableEject(owner, mutable, false, slots);
            itemSlots.SetInsertOnInteract(owner, "mutable", true, slots);
        });
        await Pair.RunTicksSync(3);

        await Client.WaitAssertion(() =>
        {
            var owner = CEntMan.GetEntity(ownerNet);
            var mutable = CEntMan.GetComponent<ItemSlotsComponent>(owner).Slots["mutable"];
            Assert.Multiple(() =>
            {
                Assert.That(mutable.DisableEject, Is.False);
                Assert.That(mutable.InsertOnInteract, Is.True);
            });
        });
    }

    [Test]
    public async Task CmDynamicCopiesRetainFiltersNamesAndSeparateStartingItems()
    {
        await Server.WaitAssertion(() =>
        {
            var owner = SEntMan.Spawn("ItemSlotsMergeCMOwner");
            var slots = SEntMan.GetComponent<ItemSlotsComponent>(owner).Slots;
            Assert.That(slots.Keys, Is.EquivalentTo(new[] { "merge-pocket1", "merge-pocket2" }));

            var first = slots["merge-pocket1"];
            var second = slots["merge-pocket2"];
            Assert.Multiple(() =>
            {
                Assert.That(first.Name, Is.EqualTo("merge-pocket 1"));
                Assert.That(second.Name, Is.EqualTo("merge-pocket 2"));
                Assert.That(first.Whitelist!.Components, Is.EqualTo(new[] { "Item" }));
                Assert.That(second.Whitelist!.Components, Is.EqualTo(new[] { "Item" }));
                Assert.That(first.Blacklist!.Components, Is.EqualTo(new[] { "Damageable" }));
                Assert.That(second.Blacklist!.Components, Is.EqualTo(new[] { "Damageable" }));
                Assert.That(first.DisableEject, Is.True);
                Assert.That(second.DisableEject, Is.True);
                Assert.That(first.InsertOnInteract, Is.False);
                Assert.That(second.InsertOnInteract, Is.False);
                Assert.That(first.Priority, Is.EqualTo(4));
                Assert.That(second.Priority, Is.EqualTo(4));
                Assert.That(SEntMan.GetComponent<MetaDataComponent>(first.Item!.Value).EntityPrototype?.ID,
                    Is.EqualTo("Crowbar"));
                Assert.That(SEntMan.GetComponent<MetaDataComponent>(second.Item!.Value).EntityPrototype?.ID,
                    Is.EqualTo("Wrench"));
            });
        });
    }

    [Test]
    public async Task HardpointsDisableGenericInsertAndRespectPerSlotEjectionPolicy()
    {
        await Server.WaitAssertion(() =>
        {
            _ = Server.System<HardpointSystem>();
            var owner = SEntMan.Spawn("ItemSlotsMergeHardpointOwner");
            var slots = SEntMan.GetComponent<ItemSlotsComponent>(owner).Slots;

            Assert.Multiple(() =>
            {
                Assert.That(slots.Keys, Is.EquivalentTo(new[] { "protected", "ordinary" }));
                Assert.That(slots["protected"].InsertOnInteract, Is.False);
                Assert.That(slots["ordinary"].InsertOnInteract, Is.False);
                Assert.That(slots["protected"].DisableEject, Is.True);
                Assert.That(slots["ordinary"].DisableEject, Is.False);
                Assert.That(slots["protected"].Whitelist!.Components,
                    Is.EqualTo(new[] { HardpointItemComponent.ComponentId }));
                Assert.That(slots["ordinary"].Whitelist!.Components,
                    Is.EqualTo(new[] { HardpointItemComponent.ComponentId }));
            });
        });
    }
}

#pragma warning restore RA0002
