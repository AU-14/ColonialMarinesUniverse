using Content.IntegrationTests.Fixtures;
using Content.Server.Storage.EntitySystems;
using Content.Shared.Storage.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.Storage;

[TestFixture]
[TestOf(typeof(EntityStorageSystem))]
public sealed class EntityStorageSuccessorMergeRegressionTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: EntityStorageMergeOpen
  components:
  - type: EntityStorage
    open: true
  - type: EntityStorageMergeProbe

- type: entity
  id: EntityStorageMergeClosed
  components:
  - type: EntityStorage
  - type: EntityStorageMergeProbe

- type: entity
  parent: BaseItem
  id: EntityStorageMergeItem
  components:
  - type: EntityStorageMergeProbe
";

    [Test]
    public async Task XenoToggleIsOpenOnlyWhileOrdinaryUsersCanCloseAndOpen()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var storage = Server.System<EntityStorageSystem>();
            var locker = SEntMan.SpawnEntity("EntityStorageMergeOpen", map.MapCoords);
            var ordinary = SEntMan.Spawn("CMMobHuman", MapCoordinates.Nullspace);
            var xeno = SEntMan.Spawn("CMXenoDrone", MapCoordinates.Nullspace);
            var component = SEntMan.GetComponent<EntityStorageComponent>(locker);
            var probe = SEntMan.GetComponent<EntityStorageMergeProbeComponent>(locker);

            storage.ToggleOpen(xeno, locker, component);
            Assert.Multiple(() =>
            {
                Assert.That(component.Open, Is.True,
                    "a Xeno may open entity storage but may not close an already-open storage");
                Assert.That(probe.BeforeCloseCalls, Is.Zero);
                Assert.That(probe.AfterCloseCalls, Is.Zero);
            });

            storage.ToggleOpen(ordinary, locker, component);
            Assert.Multiple(() =>
            {
                Assert.That(component.Open, Is.False);
                Assert.That(probe.BeforeCloseCalls, Is.EqualTo(1));
                Assert.That(probe.AfterCloseCalls, Is.EqualTo(1));
                Assert.That(probe.BeforeCloseUser, Is.EqualTo(ordinary));
                Assert.That(probe.AfterCloseUser, Is.EqualTo(ordinary));
            });

            storage.ToggleOpen(ordinary, locker, component);
            Assert.Multiple(() =>
            {
                Assert.That(component.Open, Is.True);
                Assert.That(probe.BeforeOpenCalls, Is.EqualTo(1));
                Assert.That(probe.AfterOpenCalls, Is.EqualTo(1));
                Assert.That(probe.BeforeOpenUser, Is.EqualTo(ordinary));
                Assert.That(probe.AfterOpenUser, Is.EqualTo(ordinary));
            });
        });
    }

    [Test]
    public async Task InsertChecksAndContainerLifecycleKeepTheAuthoritativeContainerIdentity()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var storage = Server.System<EntityStorageSystem>();
            var locker = SEntMan.SpawnEntity("EntityStorageMergeClosed", map.MapCoords);
            var item = SEntMan.SpawnEntity("EntityStorageMergeItem", map.MapCoords);
            var component = SEntMan.GetComponent<EntityStorageComponent>(locker);
            var lockerProbe = SEntMan.GetComponent<EntityStorageMergeProbeComponent>(locker);
            var itemProbe = SEntMan.GetComponent<EntityStorageMergeProbeComponent>(item);

            Assert.That(storage.CanInsert(item, locker, component), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(itemProbe.ItemAttemptCalls, Is.EqualTo(1));
                Assert.That(lockerProbe.ContainerAttemptCalls, Is.EqualTo(1));
                Assert.That(itemProbe.ItemAttemptContainer, Is.SameAs(component.Contents));
                Assert.That(lockerProbe.ContainerAttemptContainer, Is.SameAs(component.Contents));
                Assert.That(lockerProbe.AttemptedItem, Is.EqualTo(item));
            });

            itemProbe.CancelItemAttempt = true;
            Assert.That(storage.CanInsert(item, locker, component), Is.False);
            Assert.Multiple(() =>
            {
                Assert.That(itemProbe.ItemAttemptCalls, Is.EqualTo(2));
                Assert.That(lockerProbe.ContainerAttemptCalls, Is.EqualTo(1),
                    "item-local cancellation stops before the storage-owner insertion check");
            });
            itemProbe.CancelItemAttempt = false;

            Assert.That(storage.Insert(item, locker, component), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(lockerProbe.InsertedCalls, Is.EqualTo(1));
                Assert.That(lockerProbe.InsertedContainer, Is.SameAs(component.Contents));
                Assert.That(lockerProbe.InsertedEntity, Is.EqualTo(item));
                Assert.That(component.Contents.Contains(item), Is.True);
                Assert.That(SEntMan.GetComponent<InsideEntityStorageComponent>(item).Storage, Is.EqualTo(locker));
            });

            Assert.That(storage.Remove(item, locker, component), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(lockerProbe.RemovedCalls, Is.EqualTo(1));
                Assert.That(lockerProbe.RemovedContainer, Is.SameAs(component.Contents));
                Assert.That(lockerProbe.RemovedEntity, Is.EqualTo(item));
                Assert.That(component.Contents.Contains(item), Is.False);
                Assert.That(SEntMan.HasComponent<InsideEntityStorageComponent>(item), Is.False);
            });
        });
    }
}

[RegisterComponent]
public sealed partial class EntityStorageMergeProbeComponent : Component
{
    public bool CancelItemAttempt;
    public int ItemAttemptCalls;
    public int ContainerAttemptCalls;
    public BaseContainer? ItemAttemptContainer;
    public BaseContainer? ContainerAttemptContainer;
    public EntityUid AttemptedItem;

    public int BeforeOpenCalls;
    public int AfterOpenCalls;
    public int BeforeCloseCalls;
    public int AfterCloseCalls;
    public EntityUid? BeforeOpenUser;
    public EntityUid? AfterOpenUser;
    public EntityUid? BeforeCloseUser;
    public EntityUid? AfterCloseUser;

    public int InsertedCalls;
    public int RemovedCalls;
    public BaseContainer? InsertedContainer;
    public BaseContainer? RemovedContainer;
    public EntityUid InsertedEntity;
    public EntityUid RemovedEntity;
}

public sealed class EntityStorageMergeProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<EntityStorageMergeProbeComponent, InsertIntoEntityStorageAttemptEvent>(OnItemAttempt);
        SubscribeLocalEvent<EntityStorageMergeProbeComponent, EntityStorageInsertedIntoAttemptEvent>(OnContainerAttempt);
        SubscribeLocalEvent<EntityStorageMergeProbeComponent, StorageBeforeOpenEvent>(OnBeforeOpen);
        SubscribeLocalEvent<EntityStorageMergeProbeComponent, StorageAfterOpenEvent>(OnAfterOpen);
        SubscribeLocalEvent<EntityStorageMergeProbeComponent, StorageBeforeCloseEvent>(OnBeforeClose);
        SubscribeLocalEvent<EntityStorageMergeProbeComponent, StorageAfterCloseEvent>(OnAfterClose);
        SubscribeLocalEvent<EntityStorageMergeProbeComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<EntityStorageMergeProbeComponent, EntRemovedFromContainerMessage>(OnRemoved);
    }

    private static void OnItemAttempt(
        Entity<EntityStorageMergeProbeComponent> ent,
        ref InsertIntoEntityStorageAttemptEvent args)
    {
        ent.Comp.ItemAttemptCalls++;
        ent.Comp.ItemAttemptContainer = args.Container;
        args.Cancelled |= ent.Comp.CancelItemAttempt;
    }

    private static void OnContainerAttempt(
        Entity<EntityStorageMergeProbeComponent> ent,
        ref EntityStorageInsertedIntoAttemptEvent args)
    {
        ent.Comp.ContainerAttemptCalls++;
        ent.Comp.ContainerAttemptContainer = args.Container;
        ent.Comp.AttemptedItem = args.ItemToInsert;
    }

    private static void OnBeforeOpen(
        Entity<EntityStorageMergeProbeComponent> ent,
        ref StorageBeforeOpenEvent args)
    {
        ent.Comp.BeforeOpenCalls++;
        ent.Comp.BeforeOpenUser = args.User;
    }

    private static void OnAfterOpen(
        Entity<EntityStorageMergeProbeComponent> ent,
        ref StorageAfterOpenEvent args)
    {
        ent.Comp.AfterOpenCalls++;
        ent.Comp.AfterOpenUser = args.User;
    }

    private static void OnBeforeClose(
        Entity<EntityStorageMergeProbeComponent> ent,
        ref StorageBeforeCloseEvent args)
    {
        ent.Comp.BeforeCloseCalls++;
        ent.Comp.BeforeCloseUser = args.User;
    }

    private static void OnAfterClose(
        Entity<EntityStorageMergeProbeComponent> ent,
        ref StorageAfterCloseEvent args)
    {
        ent.Comp.AfterCloseCalls++;
        ent.Comp.AfterCloseUser = args.User;
    }

    private static void OnInserted(
        Entity<EntityStorageMergeProbeComponent> ent,
        ref EntInsertedIntoContainerMessage args)
    {
        ent.Comp.InsertedCalls++;
        ent.Comp.InsertedContainer = args.Container;
        ent.Comp.InsertedEntity = args.Entity;
    }

    private static void OnRemoved(
        Entity<EntityStorageMergeProbeComponent> ent,
        ref EntRemovedFromContainerMessage args)
    {
        ent.Comp.RemovedCalls++;
        ent.Comp.RemovedContainer = args.Container;
        ent.Comp.RemovedEntity = args.Entity;
    }
}
