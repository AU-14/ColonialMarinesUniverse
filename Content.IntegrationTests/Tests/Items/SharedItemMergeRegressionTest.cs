using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server.Verbs;
using Content.Shared._RMC14.Hands;
using Content.Shared._RMC14.Item;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Storage;
using Content.Shared.Verbs;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.Items;

[TestFixture]
[TestOf(typeof(SharedItemSystem))]
public sealed class SharedItemMergeRegressionTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: SharedItemMergePickup
  components:
  - type: Item
  - type: ItemMergeProbe

- type: entity
  parent: SharedItemMergePickup
  id: SharedItemMergeDenied
  components:
  - type: ItemMergeDenyPickup

- type: entity
  id: SharedItemMergeShape
  components:
  - type: Item
    size: Small

- type: entity
  id: SharedItemMergeFixedStorage
  components:
  - type: Storage
    grid:
    - 0,0,9,9
  - type: FixedItemSizeStorage
    size: 3,2

- type: entity
  id: SharedItemMergeOrdinaryStorage
  components:
  - type: Storage
    grid:
    - 0,0,9,9

- type: entity
  id: SharedItemMergeInvalidSize
  components:
  - type: Item
    size: Invalid

- type: entity
  id: SharedItemMergeClothing
  components:
  - type: Item
  - type: Clothing
    slots: [head]
";

    [Test]
    public async Task PickupEventIsEntityLocalAndBroadcastOnlyAfterSuccess()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var probeSystem = Server.System<SharedItemMergeProbeSystem>();
            probeSystem.Reset();
            var user = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            var deniedUser = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            var success = SEntMan.SpawnEntity("SharedItemMergePickup", map.GridCoords);
            var denied = SEntMan.SpawnEntity("SharedItemMergeDenied", map.GridCoords);
            var alreadyHandled = SEntMan.SpawnEntity("SharedItemMergePickup", map.GridCoords);

            try
            {
                var successInteraction = new InteractHandEvent(user, success);
                SEntMan.EventBus.RaiseLocalEvent(success, successInteraction);
                var successProbe = SEntMan.GetComponent<SharedItemMergeProbeComponent>(success);
                Assert.Multiple(() =>
                {
                    Assert.That(successInteraction.Handled, Is.True);
                    Assert.That(successProbe.LocalPickups, Is.EqualTo(1));
                    Assert.That(successProbe.LastUser, Is.EqualTo(user));
                    Assert.That(successProbe.LastItem, Is.EqualTo(success));
                    Assert.That(probeSystem.BroadcastPickups, Is.EqualTo(1));
                    Assert.That(probeSystem.LastBroadcastUser, Is.EqualTo(user));
                    Assert.That(probeSystem.LastBroadcastItem, Is.EqualTo(success));
                });

                var deniedInteraction = new InteractHandEvent(deniedUser, denied);
                SEntMan.EventBus.RaiseLocalEvent(denied, deniedInteraction);
                Assert.Multiple(() =>
                {
                    Assert.That(deniedInteraction.Handled, Is.False);
                    Assert.That(probeSystem.DenyAttempts, Is.EqualTo(1),
                        "the fresh-handed user must reach GettingPickedUpAttemptEvent before cancellation");
                    Assert.That(SEntMan.GetComponent<SharedItemMergeProbeComponent>(denied).LocalPickups, Is.Zero);
                    Assert.That(probeSystem.BroadcastPickups, Is.EqualTo(1),
                        "failed pickup must not emit ItemPickedUpEvent");
                });

                var handledInteraction = new InteractHandEvent(user, alreadyHandled)
                {
                    Handled = true,
                };
                SEntMan.EventBus.RaiseLocalEvent(alreadyHandled, handledInteraction);
                Assert.Multiple(() =>
                {
                    Assert.That(SEntMan.GetComponent<SharedItemMergeProbeComponent>(alreadyHandled).LocalPickups,
                        Is.Zero);
                    Assert.That(probeSystem.BroadcastPickups, Is.EqualTo(1),
                        "an already-handled interaction must return before pickup and broadcast");
                });
            }
            finally
            {
                SEntMan.DeleteEntity(alreadyHandled);
                SEntMan.DeleteEntity(denied);
                SEntMan.DeleteEntity(success);
                SEntMan.DeleteEntity(deniedUser);
                SEntMan.DeleteEntity(user);
            }
        });
    }

    [Test]
    public async Task FixedStorageOverridesAndCachesItemShapeIncludingRotation()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var itemSystem = Server.System<SharedItemSystem>();
            var item = SEntMan.SpawnEntity("SharedItemMergeShape", map.GridCoords);
            var fixedStorage = SEntMan.SpawnEntity("SharedItemMergeFixedStorage", map.GridCoords);
            var ordinaryStorage = SEntMan.SpawnEntity("SharedItemMergeOrdinaryStorage", map.GridCoords);

            try
            {
                var itemComponent = SEntMan.GetComponent<ItemComponent>(item);
                var fixedStorageComponent = SEntMan.GetComponent<StorageComponent>(fixedStorage);
                var ordinaryStorageComponent = SEntMan.GetComponent<StorageComponent>(ordinaryStorage);
                var fixedSize = SEntMan.GetComponent<FixedItemSizeStorageComponent>(fixedStorage);
                itemSystem.SetShape(item, [new Box2i(0, 0, 4, 1)], itemComponent);

                var itemEntity = new Entity<ItemComponent?>(item, itemComponent);
                var fixedEntity = new Entity<StorageComponent?>(fixedStorage, fixedStorageComponent);
                var ordinaryEntity = new Entity<StorageComponent?>(ordinaryStorage, ordinaryStorageComponent);
                var fixedShape = itemSystem.GetItemShape(fixedEntity, itemEntity);
                var ordinaryShape = itemSystem.GetItemShape(ordinaryEntity, itemEntity);

                Assert.Multiple(() =>
                {
                    Assert.That(fixedShape, Is.EqualTo(new[] { new Box2i(0, 0, 2, 1) }),
                        "FixedItemSizeStorage uses its 3x2 storage shape instead of the item's custom shape");
                    Assert.That(ordinaryShape, Is.EqualTo(new[] { new Box2i(0, 0, 4, 1) }),
                        "ordinary storage retains the item-specific shape");
                });

                typeof(FixedItemSizeStorageComponent)
                    .GetField(nameof(FixedItemSizeStorageComponent.Size), BindingFlags.Instance | BindingFlags.Public)!
                    .SetValue(fixedSize, new Vector2i(1, 1));
                Assert.That(itemSystem.GetItemShape(fixedEntity, itemEntity), Is.SameAs(fixedShape),
                    "the fixed storage shape is cached after its first resolution");

                var adjusted = itemSystem.GetAdjustedItemShape(
                    fixedEntity,
                    itemEntity,
                    Angle.FromDegrees(90),
                    new Vector2i(5, 7));
                Assert.That(adjusted, Is.EqualTo(new[] { new Box2i(5, 7, 6, 9) }),
                    "rotation and placement must operate on the storage-specific cached shape");
            }
            finally
            {
                SEntMan.DeleteEntity(ordinaryStorage);
                SEntMan.DeleteEntity(fixedStorage);
                SEntMan.DeleteEntity(item);
            }
        });
    }

    [Test]
    public async Task InvalidSizeHasNoExamineLineAndClothingRetainsPickupVerb()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var localization = Server.ResolveDependency<ILocalizationManager>();
            var verbs = Server.System<VerbSystem>();
            var examiner = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            var invalid = SEntMan.SpawnEntity("SharedItemMergeInvalidSize", map.GridCoords);
            var clothing = SEntMan.SpawnEntity("SharedItemMergeClothing", map.GridCoords);

            try
            {
                var examined = new ExaminedEvent(new FormattedMessage(), invalid, examiner, true, false);
                SEntMan.EventBus.RaiseLocalEvent(invalid, examined);
                Assert.That(examined.GetTotalMessage().ToMarkup(), Is.Empty,
                    "Item.Size=Invalid must return before size localization/prototype lookup");

                var pickupText = localization.GetString("pick-up-verb-get-data-text");
                var clothingVerbs = verbs.GetLocalVerbs(clothing, examiner, typeof(InteractionVerb), force: true);
                Assert.That(clothingVerbs.Any(verb => verb.Text == pickupText), Is.True,
                    "Clothing remains eligible for the ordinary upstream pickup verb");
            }
            finally
            {
                SEntMan.DeleteEntity(clothing);
                SEntMan.DeleteEntity(invalid);
                SEntMan.DeleteEntity(examiner);
            }
        });
    }
}

[RegisterComponent]
public sealed partial class SharedItemMergeProbeComponent : Component
{
    public int LocalPickups;
    public EntityUid LastUser;
    public EntityUid LastItem;
}

[RegisterComponent]
public sealed partial class SharedItemMergeDenyPickupComponent : Component;

public sealed class SharedItemMergeProbeSystem : EntitySystem
{
    public int BroadcastPickups;
    public int DenyAttempts;
    public EntityUid LastBroadcastUser;
    public EntityUid LastBroadcastItem;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SharedItemMergeProbeComponent, ItemPickedUpEvent>(OnLocalPickup);
        SubscribeLocalEvent<SharedItemMergeDenyPickupComponent, GettingPickedUpAttemptEvent>(OnDenyPickup);
        SubscribeLocalEvent<ItemPickedUpEvent>(OnBroadcastPickup);
    }

    public void Reset()
    {
        BroadcastPickups = 0;
        DenyAttempts = 0;
        LastBroadcastUser = default;
        LastBroadcastItem = default;
    }

    private static void OnLocalPickup(Entity<SharedItemMergeProbeComponent> entity, ref ItemPickedUpEvent args)
    {
        entity.Comp.LocalPickups++;
        entity.Comp.LastUser = args.User;
        entity.Comp.LastItem = args.Item;
    }

    private void OnDenyPickup(
        Entity<SharedItemMergeDenyPickupComponent> entity,
        ref GettingPickedUpAttemptEvent args)
    {
        DenyAttempts++;
        args.Cancel();
    }

    private void OnBroadcastPickup(ref ItemPickedUpEvent args)
    {
        BroadcastPickups++;
        LastBroadcastUser = args.User;
        LastBroadcastItem = args.Item;
    }
}
