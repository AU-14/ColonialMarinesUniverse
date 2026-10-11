using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Elevators;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared._RMC14.Dialog;
using Content.Shared.CMU14.Elevators;
using Content.Shared.CMU14.ZLevels.Core.Components;
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
    private readonly List<EntityUid> _extraLevels = new();
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
        foreach (var entity in _extraLevels.Concat(new[] { _upper, _lower, _network }))
            await Pair.DeleteEntityTreeLeafFirst(entity);
        _extraLevels.Clear();
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
                    ActivateAndSelect(destination);
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

            SEntMan.EventBus.RaiseLocalEvent(_control, new CMUElevatorFloorSelectedEvent(SEntMan.GetNetEntity(_rider), SEntMan.GetNetEntity(_upper)));
            Assert.That(SComp<TransformComponent>(_control).MapUid, Is.EqualTo(_lower));
            Assert.That(SComp<TransformComponent>(_rider).MapUid, Is.EqualTo(_lower));
            Assert.That(SComp<CMUElevatorComponent>(_control).Disabled, Is.EqualTo(!obstruction));
        });
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public async Task DirectionalRailFenceDefinesPlatform(bool twoByTwo, bool leaveGap)
    {
        await Server.WaitAssertion(() =>
        {
            foreach (var rail in _rails)
                SEntMan.DeleteEntity(rail);
            _rails.Clear();

            // Rails fence the side of their tile that they face, like the railings mappers place.
            var fences = twoByTwo
                ? new (Vector2i Tile, Direction Facing)[]
                {
                    (new(1, 1), Direction.South), (new(1, 1), Direction.West),
                    (new(2, 1), Direction.South), (new(2, 1), Direction.East),
                    (new(1, 2), Direction.North), (new(1, 2), Direction.West),
                    (new(2, 2), Direction.North), (new(2, 2), Direction.East),
                }
                : new (Vector2i Tile, Direction Facing)[]
                {
                    (new(1, 1), Direction.North), (new(1, 1), Direction.East),
                    (new(1, 1), Direction.South), (new(1, 1), Direction.West),
                };
            if (leaveGap)
                fences = fences[..^1];

            var xformSystem = SEntMan.System<SharedTransformSystem>();
            foreach (var (tile, facing) in fences)
            {
                var rail = SEntMan.SpawnEntity("CMUElevatorRail",
                    new EntityCoordinates(_lower, new Vector2(tile.X + 0.5f, tile.Y + 0.5f)));
                xformSystem.SetLocalRotation(rail, facing.ToAngle());
                SComp<CMUElevatorRailComponent>(rail).ElevatorId = "test-lift";
                _rails.Add(rail);
            }

            var footprint = twoByTwo
                ? new[] { new Vector2i(1, 1), new(2, 1), new(1, 2), new(2, 2) }
                : new[] { new Vector2i(1, 1) };

            var session = Pair.Player;
            var previousActor = session.AttachedEntity;
            Server.PlayerMan.SetAttachedEntity(session, _rider);
            try
            {
                if (leaveGap)
                {
                    var activation = new ActivateInWorldEvent(_rider, _control, true);
                    SEntMan.EventBus.RaiseLocalEvent(_control, activation);
                    Assert.That(SEntMan.System<SharedUserInterfaceSystem>().IsUiOpen(_control, DialogUiKey.Key, _rider),
                        Is.False);
                    SEntMan.EventBus.RaiseLocalEvent(_control,
                        new CMUElevatorFloorSelectedEvent(SEntMan.GetNetEntity(_rider), SEntMan.GetNetEntity(_upper)));
                    Assert.That(SComp<TransformComponent>(_control).MapUid, Is.EqualTo(_lower));
                    Assert.That(SComp<TransformComponent>(_rider).MapUid, Is.EqualTo(_lower));
                    return;
                }

                foreach (var destination in new[] { _upper, _lower })
                {
                    ActivateAndSelect(destination);
                    var source = destination == _upper ? _lower : _upper;
                    Assert.That(SComp<TransformComponent>(_rider).MapUid, Is.EqualTo(destination));
                    Assert.That(SComp<TransformComponent>(_control).MapUid, Is.EqualTo(destination));
                    for (var i = 0; i < _rails.Count; i++)
                    {
                        var xform = SComp<TransformComponent>(_rails[i]);
                        Assert.That(xform.MapUid, Is.EqualTo(destination));
                        Assert.That(xform.Anchored, Is.True);
                        Assert.That(xform.LocalRotation.GetCardinalDir(), Is.EqualTo(fences[i].Facing));
                    }

                    for (var x = 0; x < 3; x++)
                    for (var y = 0; y < 3; y++)
                    {
                        var tile = new Vector2i(x, y);
                        var onPlatform = footprint.Contains(tile);
                        Assert.That(_maps.GetTileRef(destination, SComp<MapGridComponent>(destination), tile).Tile.IsEmpty,
                            Is.EqualTo(!onPlatform && destination == _upper));
                        Assert.That(_maps.GetTileRef(source, SComp<MapGridComponent>(source), tile).Tile.IsEmpty,
                            Is.EqualTo(onPlatform || source == _upper));
                    }
                }
            }
            finally
            {
                Server.PlayerMan.SetAttachedEntity(session, previousActor);
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ElevatorWallsAndDoorsCloseTheOutline(bool leaveGap)
    {
        await Server.WaitAssertion(() =>
        {
            Assert.That(SComp<PhysicsComponent>(_control).CanCollide, Is.False);

            foreach (var rail in _rails)
                SEntMan.DeleteEntity(rail);
            _rails.Clear();

            for (var x = 0; x < 3; x++)
            for (var y = 0; y < 3; y++)
            {
                if (x == 1 && y == 1 || leaveGap && x == 2 && y == 1)
                    continue;

                var prototype = x == 1 && y == 0 ? "CMUElevatorDoor" : "CMUElevatorWall";
                var piece = SEntMan.SpawnEntity(prototype, new EntityCoordinates(_lower, new Vector2(x + 0.5f, y + 0.5f)));
                SComp<CMUElevatorRailComponent>(piece).ElevatorId = "test-lift";
                _rails.Add(piece);
            }

            var session = Pair.Player;
            var previousActor = session.AttachedEntity;
            Server.PlayerMan.SetAttachedEntity(session, _rider);
            try
            {
                if (leaveGap)
                {
                    SEntMan.EventBus.RaiseLocalEvent(_control, new ActivateInWorldEvent(_rider, _control, true));
                    Assert.That(SEntMan.System<SharedUserInterfaceSystem>().IsUiOpen(_control, DialogUiKey.Key, _rider),
                        Is.False);
                    return;
                }

                foreach (var destination in new[] { _upper, _lower })
                {
                    ActivateAndSelect(destination);
                    Assert.That(SComp<TransformComponent>(_rider).MapUid, Is.EqualTo(destination));
                    foreach (var piece in _rails.Append(_control))
                    {
                        var xform = SComp<TransformComponent>(piece);
                        Assert.That(xform.MapUid, Is.EqualTo(destination));
                        Assert.That(xform.Anchored, Is.True);
                    }
                }
            }
            finally
            {
                Server.PlayerMan.SetAttachedEntity(session, previousActor);
            }
        });
    }

    [Test]
    public async Task ControlListsEveryFloorAndSkipsPastOpenLevels()
    {
        await Server.WaitAssertion(() =>
        {
            var top = CreateLevel(2);
            var session = Pair.Player;
            var previousActor = session.AttachedEntity;
            Server.PlayerMan.SetAttachedEntity(session, _rider);
            try
            {
                SEntMan.EventBus.RaiseLocalEvent(_control, new ActivateInWorldEvent(_rider, _control, true));
                var options = SComp<DialogComponent>(_control).Options;
                Assert.That(options.Select(option => option.Text), Is.EqualTo(new[] { "Floor 2", "Floor 3" }));

                SelectFloor(top);
                Assert.That(SComp<TransformComponent>(_rider).MapUid, Is.EqualTo(top));
                Assert.That(SComp<TransformComponent>(_control).MapUid, Is.EqualTo(top));
                Assert.That(_maps.GetTileRef(_upper, SComp<MapGridComponent>(_upper), new Vector2i(1, 1)).Tile.IsEmpty,
                    Is.True, "Passing through a level must not leave tiles on it.");

                ActivateAndSelect(_lower);
                Assert.That(SComp<TransformComponent>(_rider).MapUid, Is.EqualTo(_lower));
            }
            finally
            {
                Server.PlayerMan.SetAttachedEntity(session, previousActor);
            }
        });
    }

    [Test]
    public async Task FloorInTheShaftBlocksMultiLevelTravel()
    {
        await Server.WaitAssertion(() =>
        {
            var top = CreateLevel(2);
            var floor = new Tile(Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);
            _maps.SetTile(_upper, SComp<MapGridComponent>(_upper), new Vector2i(1, 1), floor);

            SEntMan.EventBus.RaiseLocalEvent(_control,
                new CMUElevatorFloorSelectedEvent(SEntMan.GetNetEntity(_rider), SEntMan.GetNetEntity(top)));
            Assert.That(SComp<TransformComponent>(_control).MapUid, Is.EqualTo(_lower));
            Assert.That(SComp<TransformComponent>(_rider).MapUid, Is.EqualTo(_lower));
        });
    }

    [Test]
    public async Task CallButtonSummonsTheElevatorToItsFloor()
    {
        await Server.WaitAssertion(() =>
        {
            var top = CreateLevel(2);
            var caller = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(top, new Vector2(5.5f)));
            var topButton = SEntMan.SpawnEntity("CMUElevatorCallButton", new EntityCoordinates(top, new Vector2(4.5f)));
            var lowerButton = SEntMan.SpawnEntity("CMUElevatorCallButton", new EntityCoordinates(_lower, new Vector2(4.5f)));
            var otherButton = SEntMan.SpawnEntity("CMUElevatorCallButton", new EntityCoordinates(top, new Vector2(6.5f)));
            SComp<CMUElevatorCallButtonComponent>(topButton).ElevatorId = "test-lift";
            SComp<CMUElevatorCallButtonComponent>(lowerButton).ElevatorId = "test-lift";
            SComp<CMUElevatorCallButtonComponent>(otherButton).ElevatorId = "other-lift";

            Press(otherButton);
            Assert.That(SComp<TransformComponent>(_control).MapUid, Is.EqualTo(_lower));

            Press(topButton);
            Assert.That(SComp<TransformComponent>(_control).MapUid, Is.EqualTo(top));
            Assert.That(SComp<TransformComponent>(_rider).MapUid, Is.EqualTo(top));
            Assert.That(_rails.All(rail => SComp<TransformComponent>(rail).MapUid == top), Is.True);

            SComp<CMUElevatorComponent>(_control).Disabled = true;
            Press(lowerButton);
            Assert.That(SComp<TransformComponent>(_control).MapUid, Is.EqualTo(top));

            SComp<CMUElevatorComponent>(_control).Disabled = false;
            Press(lowerButton);
            Assert.That(SComp<TransformComponent>(_control).MapUid, Is.EqualTo(_lower));

            void Press(EntityUid button)
            {
                var activation = new ActivateInWorldEvent(caller, button, true);
                SEntMan.EventBus.RaiseLocalEvent(button, activation);
                Assert.That(activation.Handled, Is.True);
            }
        });
    }

    private EntityUid CreateLevel(int depth)
    {
        var map = _maps.CreateMap(runMapInit: true);
        SEntMan.EnsureComponent<MapGridComponent>(map);
        _extraLevels.Add(map);
        var network = new Entity<CMUZLevelsNetworkComponent>(_network, SComp<CMUZLevelsNetworkComponent>(_network));
        Assert.That(SEntMan.System<CMUZLevelsSystem>().TryAddMapsIntoZNetwork(network, new() { [map] = depth }),
            Is.True);
        return map;
    }

    private void ActivateAndSelect(EntityUid destination)
    {
        var activation = new ActivateInWorldEvent(_rider, _control, true);
        SEntMan.EventBus.RaiseLocalEvent(_control, activation);
        Assert.That(activation.Handled, Is.True);
        Assert.That(SEntMan.System<SharedUserInterfaceSystem>().IsUiOpen(_control, DialogUiKey.Key, _rider), Is.True);
        SelectFloor(destination);
    }

    private void SelectFloor(EntityUid destination)
    {
        var options = SComp<DialogComponent>(_control).Options;
        var index = options.FindIndex(option =>
            option.Event is CMUElevatorFloorSelectedEvent selected &&
            selected.TargetMap == SEntMan.GetNetEntity(destination));
        Assert.That(index, Is.GreaterThanOrEqualTo(0), "The destination floor must be offered.");
        SEntMan.EventBus.RaiseLocalEvent(_control, new DialogOptionBuiMsg(index)
        {
            Actor = _rider,
            Entity = SEntMan.GetNetEntity(_control),
            UiKey = DialogUiKey.Key,
        });
    }
}
