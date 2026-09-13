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

    [Test]
    public async Task SlotControlsPreserveLabelsPreviewsAndNonDisposingContainerLifecycle()
    {
        EntityUid labeled = default;
        NetEntity labeledNet = default;
        var expectedNamelessSlotWarnings = 0;

        bool JudgeNamelessSlotWarning(string sawmill, LogEvent message)
        {
            if (sawmill != "ctrl" ||
                message.Level != LogEventLevel.Warning ||
                message.RenderMessage() != "nameless merge fixture because it has no slot name")
            {
                return false;
            }

            expectedNamelessSlotWarnings++;
            return true;
        }

        await Server.WaitAssertion(() =>
        {
            labeled = SEntMan.SpawnEntity("InventoryMergeLabeledItem", MapCoordinates.Nullspace);
            var icon = SEntMan.GetComponent<IconLabelComponent>(labeled);
            icon.LabelTextParams =
            [
                ("baseName", "ABCDEFG"),
                ("label", "XYZ")
            ];
            SEntMan.Dirty(labeled, icon);
            labeledNet = SEntMan.GetNetEntity(labeled);
        });
        await Pair.RunUntilSynced();

        Pair.ClientLogHandler.JudgeLog += JudgeNamelessSlotWarning;
        try
        {
            await Client.WaitAssertion(() =>
            {
                var labeled = CEntMan.GetEntity(labeledNet);
                var button = new SlotButton { SlotName = "labeled" };
                button.SetEntity(labeled);
                Assert.Multiple(() =>
                {
                    Assert.That(button.Entity, Is.EqualTo(labeled));
                    Assert.That(button.IconLabel.Text, Is.EqualTo("ABCDE"),
                        "localized icon text must retain parameters and truncate after localization");
                    Assert.That(button.IconLabel.FontColorOverride, Is.EqualTo(Color.Red));
                    Assert.That(button.IconLabel.SetSize, Is.EqualTo(new Vector2(4)));
                });

                var spriteView = GetPrivate<Control>(button, "SpriteView");
                var protoView = GetPrivate<Control>(button, "ProtoView");
                Assert.Multiple(() =>
                {
                    Assert.That(spriteView.Visible, Is.True);
                    Assert.That(protoView.Visible, Is.False);
                });

                button.SetPrototype("InventoryMergeLabeledItem", fade: false);
                Assert.Multiple(() =>
                {
                    Assert.That(button.Entity, Is.Null,
                        "prototype previews must not retain a stale live UI entity");
                    Assert.That(spriteView.Visible, Is.False);
                    Assert.That(protoView.Visible, Is.True);
                    Assert.That(button.IconLabel.Text, Is.Empty,
                        "switching away from the live entity must clear its dynamic icon label");
                });

                var overlays = button.AdminOverlays.ChildCount;
                button.AddAdminOverlay(new ResPath("/Textures/Interface/examine-star.png"));
                Assert.That(button.AdminOverlays.ChildCount, Is.EqualTo(overlays + 1));

                var container = new ItemSlotButtonContainer();
                var blank = new SlotButton { Name = "nameless merge fixture" };
                Assert.That(container.TryAddButton(blank), Is.False);
                Assert.That(container.TryAddButton(button), Is.True);

                var duplicate = new SlotButton { SlotName = "labeled" };
                Assert.That(container.TryAddButton(duplicate), Is.False);
                var adoptedParent = new Control();
                var adopted = new SlotButton { SlotName = "adopted" };
                adoptedParent.AddChild(adopted);
                Assert.That(container.TryAddButton(adopted), Is.False);

                Assert.That(container.TryRemoveButton("labeled", out var removed), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(removed, Is.SameAs(button));
                    Assert.That(button.Parent, Is.Null);
                    Assert.That(button.Disposed, Is.False,
                        "removing a slot preview must detach it without taking ownership of disposal");
                });

                Assert.That(container.TryAddButton(button), Is.True);
                var second = new SlotButton { SlotName = "second" };
                Assert.That(container.TryAddButton(second), Is.True);
                container.ClearButtons();
                Assert.Multiple(() =>
                {
                    Assert.That(button.Parent, Is.Null);
                    Assert.That(second.Parent, Is.Null);
                    Assert.That(button.Disposed, Is.False);
                    Assert.That(second.Disposed, Is.False);
                    Assert.That(container.TryGetButton("labeled", out _), Is.False);
                    Assert.That(container.TryGetButton("second", out _), Is.False);
                });

                adoptedParent.Dispose();
                container.Dispose();
                button.Dispose();
                blank.Dispose();
                duplicate.Dispose();
                second.Dispose();
            });
        }
        finally
        {
            Pair.ClientLogHandler.JudgeLog -= JudgeNamelessSlotWarning;
        }

        Assert.That(expectedNamelessSlotWarnings, Is.EqualTo(1),
            "the nameless slot rejection must emit exactly one intentional warning");

        await Server.WaitPost(() => SEntMan.DeleteEntity(labeled));
    }

    [Test]
    public async Task InventoryHoverUsesGenericCapacityAndUserAwareRmcSkillGate()
    {
        var session = ServerSession!;
        var originalAttached = session.AttachedEntity;
        EntityUid user = default;
        await Server.WaitAssertion(() =>
        {
            user = SEntMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var skills = SEntMan.EnsureComponent<SkillsComponent>(user);
            skills.Preset = null;
            skills.Skills.Clear();
            SEntMan.Dirty(user, skills);

            var storage = SEntMan.SpawnEntity("InventoryMergeStorage", MapCoordinates.Nullspace);
            var held = SEntMan.SpawnEntity("InventoryMergeRestrictedItem", MapCoordinates.Nullspace);
            Assert.That(Server.System<InventorySystem>().TryEquip(user, storage, "back", force: true), Is.True);
            Assert.That(Server.System<HandsSystem>().TryPickupAnyHand(user, held, checkActionBlocker: false), Is.True);
            Server.PlayerMan.SetAttachedEntity(session, user);
        });

        try
        {
            await Pair.RunTicksSync(4);
            await Client.WaitAssertion(() =>
            {
                var player = Client.Session!.AttachedEntity!.Value;
                var controller = Client.ResolveDependency<IUserInterfaceManager>()
                    .GetUIController<InventoryUIController>();
                SetPrivate(controller, "_playerUid", (EntityUid?) player);

                var button = new SlotButton
                {
                    SlotName = "back",
                    MouseIsHovering = true
                };
                controller.UpdateHover(button);
                Assert.That(button.HoverSpriteView.Entity, Is.Not.Null);
                var hover = CEntMan.GetComponent<SpriteComponent>(button.HoverSpriteView.Entity!.Value);
                Assert.That(hover.Color, Is.EqualTo(new Color(255, 0, 0, 127)),
                    "an unskilled local user must see the RMC storage preview as rejected");
                button.ClearHover();
                button.Dispose();
            });

            await Server.WaitAssertion(() =>
            {
                Server.System<SkillsSystem>().SetSkill(user, "RMCSkillMedical", 2);
            });
            await Pair.RunUntilSynced();

            await Client.WaitAssertion(() =>
            {
                var player = Client.Session!.AttachedEntity!.Value;
                var skills = CEntMan.GetComponent<SkillsComponent>(player);
                Assert.That(skills.Skills.GetValueOrDefault("RMCSkillMedical"), Is.EqualTo(2),
                    "the authoritative skill update must replicate before the client recomputes the hover gate");
                var controller = Client.ResolveDependency<IUserInterfaceManager>()
                    .GetUIController<InventoryUIController>();
                SetPrivate(controller, "_playerUid", (EntityUid?) player);

                var button = new SlotButton
                {
                    SlotName = "back",
                    MouseIsHovering = true
                };
                controller.UpdateHover(button);
                Assert.That(button.HoverSpriteView.Entity, Is.Not.Null);
                var hover = CEntMan.GetComponent<SpriteComponent>(button.HoverSpriteView.Entity!.Value);
                Assert.That(hover.Color, Is.EqualTo(new Color(0, 255, 0, 127)),
                    "the same generic fit must become green only after the user-aware RMC gate passes");
                button.ClearHover();
                button.Dispose();
            });
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(session, originalAttached));
            await Pair.RunUntilSynced();
            await Pair.DeleteEntityTreeLeafFirst(user);
        }
    }

    private static T GetPrivate<T>(object instance, string name)
    {
        return (T) instance.GetType().BaseType!
            .GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(instance)!;
    }

    private static void SetPrivate(object instance, string name, object? value)
    {
        instance.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(instance, value);
    }
}

#pragma warning restore RA0002
