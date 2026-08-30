using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared._RMC14.Movement;
using Content.Shared.Clothing;
using Content.Shared.Clothing.Components;
using Content.Shared.Foldable;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Standing;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests;

[TestFixture]
public sealed class ClothingInventoryMergeRegressionTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: ClothingMergeInventoryDummy
  components:
  - type: Inventory
  - type: ContainerContainer

- type: entity
  parent: ClothingMergeInventoryDummy
  id: ClothingMergeInventoryActor
  components:
  - type: Hands
  - type: Stripping

- type: entity
  parent: ClothingMergeInventoryDummy
  id: ClothingMergeInventoryTarget
  components:
  - type: Strippable

- type: entity
  id: ClothingMergeMovementDummy
  components:
  - type: Inventory
  - type: ContainerContainer
  - type: MovementSpeedModifier
    baseWalkSpeed: 10
    baseSprintSpeed: 20
  - type: StandingState

- type: entity
  id: ClothingMergeHat
  components:
  - type: Item
  - type: Clothing
    slots: [head]

- type: entity
  id: ClothingMergeFoldable
  components:
  - type: Item
  - type: Clothing
    slots: [head]
  - type: Foldable
  - type: FoldableClothing
  - type: HideLayerClothing
  - type: Appearance

- type: entity
  id: ClothingMergeSpeedBoots
  components:
  - type: Item
  - type: Clothing
    slots: [FEET]
  - type: ItemToggle
    requireComplexInteract: false
  - type: ClothingSpeedModifier
    walkModifier: 0.8
    sprintModifier: 0.7
    requireActivated: true
    standing: true
  - type: ClothingSpeedModifierTestProbe
";

    [Test]
    public async Task RangeBypassStillRequiresAccessibleItemAndHonorsOccupiedSlot()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var entMan = SEntMan;
            var inventory = entMan.System<InventorySystem>();
            var actorCoordinates = map.GridCoords.Offset(new Vector2(0.5f, 0.5f));
            var targetCoordinates = map.GridCoords.Offset(new Vector2(10.5f, 0.5f));
            var actor = entMan.SpawnEntity("ClothingMergeInventoryDummy", actorCoordinates);
            var target = entMan.SpawnEntity("ClothingMergeInventoryDummy", targetCoordinates);
            var accessible = entMan.SpawnEntity("ClothingMergeHat", actorCoordinates);
            var inaccessible = entMan.SpawnEntity("ClothingMergeHat", targetCoordinates);
            var occupied = entMan.SpawnEntity("ClothingMergeHat", targetCoordinates);

            try
            {
                Assert.Multiple(() =>
                {
                    Assert.That(inventory.CanEquip(
                        actor,
                        target,
                        accessible,
                        "head",
                        out var defaultReason),
                        Is.False,
                        "default cross-target access must enforce actor-to-target range");
                    Assert.That(defaultReason, Is.EqualTo("interaction-system-user-interaction-cannot-reach"));
                    Assert.That(inventory.CanEquip(
                        actor,
                        target,
                        accessible,
                        "head",
                        out _,
                        doRangeCheck: false),
                        Is.True,
                        "the explicit vendor path may bypass only actor-to-target range");
                    Assert.That(inventory.CanEquip(
                        actor,
                        target,
                        inaccessible,
                        "head",
                        out _,
                        doRangeCheck: false),
                        Is.False,
                        "range bypass must not make an inaccessible item accessible");
                });

                Assert.That(inventory.TryEquip(target, occupied, "head", force: true), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(inventory.CanEquip(
                        actor,
                        target,
                        accessible,
                        "head",
                        out _,
                        assumeEmpty: false,
                        doRangeCheck: false),
                        Is.False,
                        "an occupied slot remains occupied");
                    Assert.That(inventory.CanEquip(
                        actor,
                        target,
                        accessible,
                        "head",
                        out _,
                        assumeEmpty: true,
                        doRangeCheck: false),
                        Is.True,
                        "assumeEmpty must ignore only the existing slot occupant");
                });
            }
            finally
            {
                entMan.DeleteEntity(actor);
                entMan.DeleteEntity(target);
                if (entMan.EntityExists(accessible))
                    entMan.DeleteEntity(accessible);
                if (entMan.EntityExists(inaccessible))
                    entMan.DeleteEntity(inaccessible);
            }
        });
    }

    [Test]
    public async Task CrossTargetInventoryEventsKeepActorAndEquipTargetDistinct()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var entMan = SEntMan;
            var inventory = entMan.System<InventorySystem>();
            var actor = entMan.SpawnEntity("ClothingMergeInventoryActor", map.GridCoords);
            var target = entMan.SpawnEntity("ClothingMergeInventoryTarget", map.GridCoords);
            var item = entMan.SpawnEntity("ClothingMergeHat", map.GridCoords);
            var selfOnlyItem = entMan.SpawnEntity("ClothingMergeHat", map.GridCoords);

            entMan.EnsureComponent<InventoryEventContractTestProbeComponent>(actor);
            entMan.EnsureComponent<InventoryEventContractTestProbeComponent>(target);
            entMan.EnsureComponent<InventoryEventContractTestProbeComponent>(item);
            entMan.EnsureComponent<SelfEquipOnlyComponent>(selfOnlyItem);

            var actorProbe = entMan.GetComponent<InventoryEventContractTestProbeComponent>(actor);
            var targetProbe = entMan.GetComponent<InventoryEventContractTestProbeComponent>(target);
            var itemProbe = entMan.GetComponent<InventoryEventContractTestProbeComponent>(item);

            try
            {
                Assert.That(actor, Is.Not.EqualTo(target));
                Assert.That(inventory.TryEquip(actor, target, item, "head", silent: true), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(itemProbe.BeingEquipped, Is.Not.Null);
                    Assert.That(itemProbe.BeingEquipped!.User, Is.EqualTo(actor));
                    Assert.That(itemProbe.BeingEquipped.EquipTarget, Is.EqualTo(target));
                    Assert.That(itemProbe.BeingEquipped.Equipment, Is.EqualTo(item));
                    Assert.That(itemProbe.BeingEquipped.Slot, Is.EqualTo("head"));

                    Assert.That(targetProbe.DidEquip, Is.Not.Null);
                    Assert.That(targetProbe.DidEquip!.EquipTarget, Is.EqualTo(target));
                    Assert.That(targetProbe.DidEquip.Equipment, Is.EqualTo(item));
                    Assert.That(targetProbe.DidEquip.Slot, Is.EqualTo("head"));
                    Assert.That(actorProbe.DidEquip, Is.Null,
                        "the completed target event must not be routed to the actor");
                    Assert.That(itemProbe.DidEquip, Is.Null);

                    Assert.That(itemProbe.GotEquipped, Is.Not.Null);
                    Assert.That(itemProbe.GotEquipped!.EquipTarget, Is.EqualTo(target));
                    Assert.That(itemProbe.GotEquipped.Equipment, Is.EqualTo(item));
                    Assert.That(itemProbe.GotEquipped.Slot, Is.EqualTo("head"));
                    Assert.That(actorProbe.GotEquipped, Is.Null);
                    Assert.That(targetProbe.GotEquipped, Is.Null,
                        "the completed equipment event must remain directed to the item");
                });

                Assert.That(
                    inventory.TryUnequip(actor, target, "head", out var removedItem, silent: true),
                    Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(removedItem, Is.EqualTo(item));
                    Assert.That(itemProbe.BeingUnequipped, Is.Not.Null);
                    Assert.That(itemProbe.BeingUnequipped!.User, Is.EqualTo(actor));
                    Assert.That(itemProbe.BeingUnequipped.UnEquipTarget, Is.EqualTo(target));
                    Assert.That(itemProbe.BeingUnequipped.Equipment, Is.EqualTo(item));
                    Assert.That(itemProbe.BeingUnequipped.Slot, Is.EqualTo("head"));

                    Assert.That(targetProbe.DidUnequip, Is.Not.Null);
                    Assert.That(targetProbe.DidUnequip!.EquipTarget, Is.EqualTo(target));
                    Assert.That(targetProbe.DidUnequip.Equipment, Is.EqualTo(item));
                    Assert.That(targetProbe.DidUnequip.Slot, Is.EqualTo("head"));
                    Assert.That(actorProbe.DidUnequip, Is.Null,
                        "the completed target event must not be routed to the actor");
                    Assert.That(itemProbe.DidUnequip, Is.Null);

                    Assert.That(itemProbe.GotUnequipped, Is.Not.Null);
                    Assert.That(itemProbe.GotUnequipped!.EquipTarget, Is.EqualTo(target));
                    Assert.That(itemProbe.GotUnequipped.Equipment, Is.EqualTo(item));
                    Assert.That(itemProbe.GotUnequipped.Slot, Is.EqualTo("head"));
                    Assert.That(actorProbe.GotUnequipped, Is.Null);
                    Assert.That(targetProbe.GotUnequipped, Is.Null,
                        "the completed equipment event must remain directed to the item");
                });

                Assert.That(
                    inventory.TryEquip(actor, target, selfOnlyItem, "head", silent: true),
                    Is.False,
                    "SelfEquipOnly must compare the attempt User with the EquipTarget");
                Assert.That(
                    inventory.TryEquip(target, target, selfOnlyItem, "head", silent: true),
                    Is.True,
                    "the same valid item must remain self-equippable");
                Assert.That(
                    inventory.TryUnequip(actor, target, "head", out _, silent: true),
                    Is.False,
                    "SelfEquipOnly must compare the unequip attempt User with the UnEquipTarget");
                Assert.That(inventory.TryGetSlotEntity(target, "head", out var stillEquipped), Is.True);
                Assert.That(stillEquipped, Is.EqualTo(selfOnlyItem));
            }
            finally
            {
                entMan.DeleteEntity(actor);
                entMan.DeleteEntity(target);
                if (entMan.EntityExists(item))
                    entMan.DeleteEntity(item);
                if (entMan.EntityExists(selfOnlyItem))
                    entMan.DeleteEntity(selfOnlyItem);
            }
        });
    }

    [Test]
    public async Task FoldingMutatesOnlyLayersOwnedByFoldableClothing()
    {
        await Server.WaitAssertion(() =>
        {
            var entMan = SEntMan;
            var foldable = entMan.System<FoldableSystem>();
            var clothing = entMan.Spawn("ClothingMergeFoldable");

            try
            {
                var fold = entMan.GetComponent<FoldableComponent>(clothing);
                var foldClothing = entMan.GetComponent<FoldableClothingComponent>(clothing);
                var hidden = entMan.GetComponent<HideLayerClothingComponent>(clothing);
                foldClothing.FoldedHideLayers.Add(HumanoidVisualLayers.Hair);
                foldClothing.UnfoldedHideLayers.Add(HumanoidVisualLayers.FacialHair);
                hidden.Layers.Clear();
                hidden.Layers[HumanoidVisualLayers.HeadTop] = SlotFlags.EARS;

                foldable.SetFolded(clothing, fold, true);
                Assert.Multiple(() =>
                {
                    Assert.That(hidden.Layers[HumanoidVisualLayers.HeadTop], Is.EqualTo(SlotFlags.EARS));
                    Assert.That(hidden.Layers[HumanoidVisualLayers.Hair], Is.EqualTo(SlotFlags.HEAD));
                    Assert.That(hidden.Layers.ContainsKey(HumanoidVisualLayers.FacialHair), Is.False);
                    Assert.That(hidden.Layers.Keys,
                        Is.EquivalentTo(new[] { HumanoidVisualLayers.HeadTop, HumanoidVisualLayers.Hair }));
                });

                foldable.SetFolded(clothing, fold, false);
                Assert.Multiple(() =>
                {
                    Assert.That(hidden.Layers[HumanoidVisualLayers.HeadTop], Is.EqualTo(SlotFlags.EARS));
                    Assert.That(hidden.Layers[HumanoidVisualLayers.FacialHair], Is.EqualTo(SlotFlags.HEAD));
                    Assert.That(hidden.Layers.ContainsKey(HumanoidVisualLayers.Hair), Is.False);
                    Assert.That(hidden.Layers.Keys,
                        Is.EquivalentTo(new[] { HumanoidVisualLayers.HeadTop, HumanoidVisualLayers.FacialHair }));
                });
            }
            finally
            {
                entMan.DeleteEntity(clothing);
            }
        });
    }

    [Test]
    public async Task ClothingSpeedGatesPrecedeRmcAdjustmentAndUseAdjustedValues()
    {
        await Server.WaitAssertion(() =>
        {
            var entMan = SEntMan;
            var inventory = entMan.System<InventorySystem>();
            var movement = entMan.System<MovementSpeedModifierSystem>();
            var standing = entMan.System<StandingStateSystem>();
            var toggle = entMan.System<ItemToggleSystem>();
            var wearer = entMan.Spawn("ClothingMergeMovementDummy");
            var boots = entMan.Spawn("ClothingMergeSpeedBoots");

            try
            {
                Assert.That(inventory.TryEquip(wearer, boots, "shoes", force: true), Is.True);
                var speed = entMan.GetComponent<MovementSpeedModifierComponent>(wearer);
                var probe = entMan.GetComponent<ClothingSpeedModifierTestProbeComponent>(boots);

                movement.RefreshMovementSpeedModifiers(wearer);
                Assert.Multiple(() =>
                {
                    Assert.That(probe.Events, Is.Zero,
                        "RequireActivated must return before the RMC adjustment event");
                    Assert.That(speed.WalkSpeedModifier, Is.EqualTo(1f));
                    Assert.That(speed.SprintSpeedModifier, Is.EqualTo(1f));
                });

                Assert.That(standing.Down(wearer, playSound: false, dropHeldItems: false, force: true), Is.True);
                Assert.That(toggle.TryActivate(boots, predicted: false, showPopup: false), Is.True);
                movement.RefreshMovementSpeedModifiers(wearer);
                Assert.Multiple(() =>
                {
                    Assert.That(probe.Events, Is.Zero,
                        "the standing requirement must return before the RMC adjustment event");
                    Assert.That(speed.WalkSpeedModifier, Is.EqualTo(1f));
                    Assert.That(speed.SprintSpeedModifier, Is.EqualTo(1f));
                });

                Assert.That(standing.Stand(wearer, force: true), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(probe.Events, Is.EqualTo(1));
                    Assert.That(speed.WalkSpeedModifier, Is.EqualTo(0.5f));
                    Assert.That(speed.SprintSpeedModifier, Is.EqualTo(0.4f));
                    Assert.That(speed.CurrentWalkSpeed, Is.EqualTo(5f));
                    Assert.That(speed.CurrentSprintSpeed, Is.EqualTo(8f));
                });
            }
            finally
            {
                entMan.DeleteEntity(wearer);
            }
        });
    }
}

[RegisterComponent]
public sealed partial class ClothingSpeedModifierTestProbeComponent : Component
{
    public int Events;
}

public sealed class ClothingSpeedModifierTestProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ClothingSpeedModifierTestProbeComponent, RMCMovementSpeedRefreshedEvent>(OnRefresh);
    }

    private static void OnRefresh(
        Entity<ClothingSpeedModifierTestProbeComponent> ent,
        ref RMCMovementSpeedRefreshedEvent args)
    {
        ent.Comp.Events++;
        args.WalkModifier = 0.5f;
        args.SprintModifier = 0.4f;
    }
}

[RegisterComponent]
public sealed partial class InventoryEventContractTestProbeComponent : Component
{
    public BeingEquippedAttemptEvent? BeingEquipped;
    public BeingUnequippedAttemptEvent? BeingUnequipped;
    public DidEquipEvent? DidEquip;
    public DidUnequipEvent? DidUnequip;
    public GotEquippedEvent? GotEquipped;
    public GotUnequippedEvent? GotUnequipped;
}

public sealed class InventoryEventContractTestProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<InventoryEventContractTestProbeComponent, BeingEquippedAttemptEvent>(OnBeingEquipped);
        SubscribeLocalEvent<InventoryEventContractTestProbeComponent, BeingUnequippedAttemptEvent>(OnBeingUnequipped);
        SubscribeLocalEvent<InventoryEventContractTestProbeComponent, DidEquipEvent>(OnDidEquip);
        SubscribeLocalEvent<InventoryEventContractTestProbeComponent, DidUnequipEvent>(OnDidUnequip);
        SubscribeLocalEvent<InventoryEventContractTestProbeComponent, GotEquippedEvent>(OnGotEquipped);
        SubscribeLocalEvent<InventoryEventContractTestProbeComponent, GotUnequippedEvent>(OnGotUnequipped);
    }

    private static void OnBeingEquipped(
        Entity<InventoryEventContractTestProbeComponent> ent,
        ref BeingEquippedAttemptEvent args)
    {
        ent.Comp.BeingEquipped = args;
    }

    private static void OnBeingUnequipped(
        Entity<InventoryEventContractTestProbeComponent> ent,
        ref BeingUnequippedAttemptEvent args)
    {
        ent.Comp.BeingUnequipped = args;
    }

    private static void OnDidEquip(
        Entity<InventoryEventContractTestProbeComponent> ent,
        ref DidEquipEvent args)
    {
        ent.Comp.DidEquip = args;
    }

    private static void OnDidUnequip(
        Entity<InventoryEventContractTestProbeComponent> ent,
        ref DidUnequipEvent args)
    {
        ent.Comp.DidUnequip = args;
    }

    private static void OnGotEquipped(
        Entity<InventoryEventContractTestProbeComponent> ent,
        ref GotEquippedEvent args)
    {
        ent.Comp.GotEquipped = args;
    }

    private static void OnGotUnequipped(
        Entity<InventoryEventContractTestProbeComponent> ent,
        ref GotUnequippedEvent args)
    {
        ent.Comp.GotUnequipped = args;
    }
}
