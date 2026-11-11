using Content.Client.CMU14.UserInterface;
#pragma warning disable RA0002 // Integration regression intentionally inspects restricted component state.

using System.Numerics;
using System.Reflection;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.Inventory;
using Content.Client.UserInterface.Systems.Inventory.Controls;
using Content.IntegrationTests.Fixtures;
using Content.Server.Hands.Systems;
using Content.Server.VoiceTrigger;
using Content.Shared._RMC14.IconLabel;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Storage;
using Content.Shared.Inventory;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Trigger;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Serilog.Events;

namespace Content.IntegrationTests.Tests.Inventory;

[TestFixture]
public sealed class InventoryStorageUiMergeRegressionTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = """
- type: Tag
  id: InventoryMergeRestricted

- type: entity
  parent: ClothingBackpack
  id: InventoryMergeStorage
  components:
  - type: StorageVoiceControl
  - type: StorageStoreSkillRequired
    entries:
    - whitelist:
        tags:
        - InventoryMergeRestricted
      skills:
        all:
          RMCSkillMedical: 2

- type: entity
  parent: ClothingBackpack
  id: InventoryMergeLimitedStorage
  components:
  - type: LimitedStorage
    limits:
    - popup: rmc-storage-limit-cant-fit
      count: 0
      whitelist:
        tags:
        - InventoryMergeRestricted

- type: entity
  parent: CMPillCanister
  id: InventoryMergeRestrictedItem
  components:
  - type: Tag
    tags:
    - InventoryMergeRestricted

- type: entity
  parent: InventoryMergeRestrictedItem
  id: InventoryMergeLabeledItem
  components:
  - type: IconLabel
    labelTextLocId: comp-label-format
    textSize: 4
    textColor: Red
    labelMaxSize: 5
""";

    [Test]
    public async Task GenericCapacityAndRmcUserSkillComposeForUiAndVoiceInsertion()
    {
        EntityUid storage = default;
        EntityUid limitedStorage = default;
        EntityUid item = default;
        EntityUid user = default;
        await Server.WaitAssertion(() =>
        {
            storage = SEntMan.SpawnEntity("InventoryMergeStorage", MapCoordinates.Nullspace);
            limitedStorage = SEntMan.SpawnEntity("InventoryMergeLimitedStorage", MapCoordinates.Nullspace);
            item = SEntMan.SpawnEntity("InventoryMergeRestrictedItem", MapCoordinates.Nullspace);
            user = SEntMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);

            var skills = SEntMan.EnsureComponent<SkillsComponent>(user);
            skills.Preset = null;
            skills.Skills.Clear();
            SEntMan.Dirty(user, skills);

            var storageComp = SEntMan.GetComponent<StorageComponent>(storage);
            var limitedComp = SEntMan.GetComponent<StorageComponent>(limitedStorage);
            var generic = Server.System<SharedStorageSystem>();
            var rmc = Server.System<RMCStorageSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(generic.CanInsert(storage, item, out _, storageComp), Is.True,
                    "the upstream capacity/location check must remain user-independent");
                Assert.That(rmc.CanInsert((storage, storageComp), item, null, out _), Is.True,
                    "the compatibility no-user path must not invent a skill failure");
                Assert.That(rmc.CanInsert((storage, storageComp), item, user, out var reason), Is.False);
                Assert.That(reason, Is.EqualTo((LocId) "rmc-storage-store-skill-unable"),
                    "UI and voice callers must retain the RMC denial reason");
                Assert.That(rmc.CanInsert((limitedStorage, limitedComp), item, null, out var limitedReason), Is.False,
                    "null-user compatibility must bypass only skill checks, never RMC storage limits");
                Assert.That(limitedReason, Is.EqualTo((LocId) "rmc-storage-limit-cant-fit"));
                Assert.That(generic.CanInsert(limitedStorage, item, out _, limitedComp), Is.False,
                    "the upstream no-user entry point must still compose the user-independent RMC limit");
            });

            var hands = Server.System<HandsSystem>();
            Assert.That(hands.TryPickupAnyHand(user, item, checkActionBlocker: false), Is.True);
            var denied = new VoiceTriggeredEvent(user, null, "store");
            SEntMan.EventBus.RaiseLocalEvent(storage, ref denied);
            Assert.That(storageComp.StoredItems, Does.Not.ContainKey(item),
                "voice insertion must compose the generic check with the user-aware RMC gate");

            skills.Skills["RMCSkillMedical"] = 2;
            SEntMan.Dirty(user, skills);
            var allowed = new VoiceTriggeredEvent(user, null, "store");
            SEntMan.EventBus.RaiseLocalEvent(storage, ref allowed);
            Assert.That(storageComp.StoredItems, Does.ContainKey(item),
                "a skilled voice user must reach the ordinary storage insertion path");
        });

        await Server.WaitPost(() =>
        {
            SEntMan.DeleteEntity(user);
            SEntMan.DeleteEntity(storage);
            SEntMan.DeleteEntity(limitedStorage);
            if (SEntMan.EntityExists(item))
                SEntMan.DeleteEntity(item);
        });
    }

}

#pragma warning restore RA0002
