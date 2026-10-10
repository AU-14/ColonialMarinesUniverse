using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Elevators;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared._RMC14.Dialog;
using Content.Shared.CMU14.Elevators;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;

namespace Content.IntegrationTests.CMU14.Elevators;

[TestFixture]
[TestOf(typeof(CMUElevatorSystem))]
public sealed class CMUElevatorTest : GameTest
{
    private EntityUid _lower, _upper, _network, _control, _rider;
    private readonly List<EntityUid> _rails = new();
    private SharedMapSystem _maps = null!;

    [SetUp]
    public async Task SetupElevator()
    {
        await Server.WaitAssertion(() =>
        {
            _maps = SEntMan.System<SharedMapSystem>();
            _lower = _maps.CreateMap(runMapInit: true);
            _upper = _maps.CreateMap(runMapInit: true);
            var lowerGrid = SEntMan.EnsureComponent<MapGridComponent>(_lower);
            SEntMan.EnsureComponent<MapGridComponent>(_upper);
            var floor = new Tile(Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);
            for (var x = 0; x < 3; x++)
            for (var y = 0; y < 3; y++)
            {
                _maps.SetTile(_lower, lowerGrid, new Vector2i(x, y), floor);
                if (x == 1 && y == 1)
                    continue;

                var rail = SEntMan.SpawnEntity("CMUElevatorRail",
                    new EntityCoordinates(_lower, new Vector2(x + 0.5f, y + 0.5f)));
                SComp<CMUElevatorRailComponent>(rail).ElevatorId = "test-lift";
                _rails.Add(rail);
            }

            var center = new EntityCoordinates(_lower, new Vector2(1.5f));
            _control = SEntMan.SpawnEntity("CMUElevatorControl", center);
            SComp<CMUElevatorComponent>(_control).ElevatorId = "test-lift";
            _rider = SEntMan.SpawnEntity("CMMobHuman", center);
            var zLevels = SEntMan.System<CMUZLevelsSystem>();
            var network = zLevels.CreateZNetwork();
            _network = network;
            Assert.That(zLevels.TryAddMapsIntoZNetwork(network, new() { [_lower] = 0, [_upper] = 1 }), Is.True);
        });
    }

    [TearDown]
    public async Task CleanupElevator()
    {
        foreach (var entity in new[] { _upper, _lower, _network })
            await Pair.DeleteEntityTreeLeafFirst(entity);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task LoadedRiderRoundTripPreservesInventoryAndAnchoring(bool equipBackpack)
    {
        await Server.WaitAssertion(() =>
        {
            var center = new EntityCoordinates(_lower, new Vector2(1.5f));
            var backpack = SEntMan.SpawnEntity("CMBackpack", center);
            var storage = SComp<StorageComponent>(backpack);
            var storageSystem = SEntMan.System<SharedStorageSystem>();
            var contents = new List<EntityUid>();
            for (var i = 0; i < 21; i++)
            {
                var pen = SEntMan.SpawnEntity("CMPen", center);
                Assert.That(storageSystem.Insert(backpack, pen, out _, playSound: false), Is.True);
                contents.Add(pen);
            }

            if (equipBackpack)
                Assert.That(SEntMan.System<InventorySystem>().TryEquip(_rider, backpack, "back", force: true), Is.True);

            var held = SEntMan.SpawnEntity("CMPen", center);
            Assert.That(storageSystem.Insert(backpack, held, out _, playSound: false), Is.False,
                "The backpack must be full before testing travel.");
            Assert.That(SEntMan.System<SharedHandsSystem>().TryPickupAnyHand(_rider, held, animate: false), Is.True);
            var containedEntities = contents.Append(held);
            if (equipBackpack)
                containedEntities = containedEntities.Append(backpack);
            var containedParents = containedEntities
                .ToDictionary(entity => entity, entity => SComp<TransformComponent>(entity).ParentUid);
            var bodyContainers = SComp<ContainerManagerComponent>(_rider).Containers.Values
                .SelectMany(container => container.ContainedEntities.Select(entity => (container, entity))).ToArray();
            Assert.That(SComp<CMUElevatorWeightLimitComponent>(_control).MaxEntities, Is.EqualTo(20));
            var lowerEye = SEntMan.SpawnEntity("CMUZLevelEye", center);
            var upperEye = SEntMan.SpawnEntity("CMUZLevelEye", new EntityCoordinates(_upper, new Vector2(1.5f)));

            var session = Pair.Player;
            var previousActor = session.AttachedEntity;
            Server.PlayerMan.SetAttachedEntity(session, _rider);
            try
            {
                foreach (var destination in new[] { _upper, _lower })
                {
                    ActivateAndConfirm();
                    Assert.That(SComp<TransformComponent>(_rider).MapUid, Is.EqualTo(destination));
                    Assert.That(SComp<CMUElevatorComponent>(_control).Disabled, Is.False);
                    Assert.That(storage.Container.ContainedEntities, Is.EquivalentTo(contents));
                    foreach (var (entity, parent) in containedParents)
                    {
                        Assert.That(SComp<TransformComponent>(entity).ParentUid, Is.EqualTo(parent));
                        Assert.That(SComp<TransformComponent>(entity).MapUid, Is.EqualTo(destination));
                    }
                    foreach (var (container, entity) in bodyContainers)
                    {
                        Assert.That(container.Contains(entity), Is.True);
                        Assert.That(SComp<TransformComponent>(entity).MapUid, Is.EqualTo(destination));
                    }
                    foreach (var structure in _rails.Append(_control))
                    {
                        var xform = SComp<TransformComponent>(structure);
                        Assert.That(xform.MapUid, Is.EqualTo(destination));
                        Assert.That(xform.Anchored, Is.True);
                        Assert.That(SComp<PhysicsComponent>(structure).BodyType, Is.EqualTo(BodyType.Static));
                        Assert.That(_maps.GetAnchoredEntities(destination, SComp<MapGridComponent>(destination),
                            _maps.TileIndicesFor(destination, SComp<MapGridComponent>(destination), xform.Coordinates)),
                            Does.Contain(structure));
                    }
                    Assert.That(SComp<TransformComponent>(lowerEye).MapUid, Is.EqualTo(_lower));
                    Assert.That(SComp<TransformComponent>(upperEye).MapUid, Is.EqualTo(_upper));
                    var source = destination == _upper ? _lower : _upper;
                    for (var x = 0; x < 3; x++)
                    for (var y = 0; y < 3; y++)
                    {
                        Assert.That(_maps.GetTileRef(destination, SComp<MapGridComponent>(destination),
                            new Vector2i(x, y)).Tile.IsEmpty, Is.False);
                        Assert.That(_maps.GetTileRef(source, SComp<MapGridComponent>(source),
                            new Vector2i(x, y)).Tile.IsEmpty, Is.True);
                    }
                }
                Assert.That(SComp<CMUElevatorComponent>(_control).TravelDirection, Is.EqualTo(1));
            }
            finally
            {
                Server.PlayerMan.SetAttachedEntity(session, previousActor);
            }
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task RealObstructionsAndUncontainedOverloadStillPreventTravel(bool obstruction)
    {
        await Server.WaitAssertion(() =>
        {
            if (obstruction)
                SEntMan.SpawnEntity("CMPen", new EntityCoordinates(_upper, new Vector2(1.5f)));
            else
                SComp<CMUElevatorWeightLimitComponent>(_control).MaxEntities = 0;

            SEntMan.EventBus.RaiseLocalEvent(_control, new CMUElevatorMoveConfirmedEvent(SEntMan.GetNetEntity(_rider)));
            Assert.That(SComp<TransformComponent>(_control).MapUid, Is.EqualTo(_lower));
            Assert.That(SComp<TransformComponent>(_rider).MapUid, Is.EqualTo(_lower));
            Assert.That(SComp<CMUElevatorComponent>(_control).TravelDirection, Is.EqualTo(1));
            Assert.That(SComp<CMUElevatorComponent>(_control).Disabled, Is.EqualTo(!obstruction));
        });
    }

    private void ActivateAndConfirm()
    {
        var activation = new ActivateInWorldEvent(_rider, _control, true);
        SEntMan.EventBus.RaiseLocalEvent(_control, activation);
        Assert.That(activation.Handled, Is.True);
        Assert.That(SEntMan.System<SharedUserInterfaceSystem>().IsUiOpen(_control, DialogUiKey.Key, _rider), Is.True);
        Assert.That(SComp<DialogComponent>(_control).ConfirmEvent, Is.TypeOf<CMUElevatorMoveConfirmedEvent>());
        SEntMan.EventBus.RaiseLocalEvent(_control, new DialogConfirmBuiMsg
        {
            Actor = _rider,
            Entity = SEntMan.GetNetEntity(_control),
            UiKey = DialogUiKey.Key,
        });
    }
}
