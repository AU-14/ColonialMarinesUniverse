using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Elevators;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared._RMC14.Dialog;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared.CMU14.Elevators;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Systems;
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
            SComp<CMUElevatorComponent>(_control).TravelTime = TimeSpan.Zero;
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
            Assert.That(SComp<CMUElevatorWeightLimitComponent>(_control).MaxEntities, Is.EqualTo(250));
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
                        // The lower level is the elevator's home floor, so it keeps plating when the car leaves.
                        Assert.That(_maps.GetTileRef(source, SComp<MapGridComponent>(source),
                            new Vector2i(x, y)).Tile.IsEmpty, Is.EqualTo(source == _upper));
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

    [TestCase(false, 0)]
    [TestCase(true, 0)]
    [TestCase(true, 1)]
    [TestCase(true, 2)]
    public async Task DirectionalRailFenceDefinesPlatform(bool twoByTwo, int openSides)
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

            // One open side is allowed; a second one leaves the platform undefined.
            var openFacings = new[] { Direction.North, Direction.East }.Take(openSides).ToArray();
            fences = fences.Where(fence => !openFacings.Contains(fence.Facing)).ToArray();
            var leaveGap = openSides > 1;

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
                            Is.EqualTo(source == _upper));
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
                if (x == 1 && y == 1 || leaveGap && x != 1 && y == 1)
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

    [Test]
    public async Task TravelCycleShutsAndBoltsDoorsUntilArrival()
    {
        EntityUid door = default;
        await Server.WaitAssertion(() =>
        {
            foreach (var rail in _rails)
                SEntMan.DeleteEntity(rail);
            _rails.Clear();

            for (var x = 0; x < 3; x++)
            for (var y = 0; y < 3; y++)
            {
                if (x == 1 && y == 1)
                    continue;

                var prototype = x == 1 && y == 0 ? "CMUElevatorDoor" : "CMUElevatorWall";
                var piece = SEntMan.SpawnEntity(prototype, new EntityCoordinates(_lower, new Vector2(x + 0.5f, y + 0.5f)));
                SComp<CMUElevatorRailComponent>(piece).ElevatorId = "test-lift";
                _rails.Add(piece);
                if (prototype == "CMUElevatorDoor")
                    door = piece;
            }

            SComp<CMUElevatorComponent>(_control).TravelTime = TimeSpan.FromSeconds(3);
        });

        await Pair.RunTicksSync(5);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.System<SharedDoorSystem>().TryOpen(door), Is.True);
        });
        await Pair.RunSeconds(1.5f);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SComp<DoorComponent>(door).State, Is.EqualTo(DoorState.Open));
            Select(_upper);
            Assert.That(SComp<CMUElevatorComponent>(_control).PendingTarget, Is.EqualTo(_upper));
            Assert.That(SComp<TransformComponent>(_control).MapUid, Is.EqualTo(_lower), "The trip should not be instant.");
        });

        await Pair.RunSeconds(1.5f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SComp<DoorComponent>(door).State, Is.EqualTo(DoorState.Closed));
            Assert.That(SComp<DoorBoltComponent>(door).BoltsDown, Is.True);
            Assert.That(SComp<TransformComponent>(_rider).MapUid, Is.EqualTo(_lower));

            // Requests made while the car is moving are refused.
            Select(_lower);
            Assert.That(SComp<CMUElevatorComponent>(_control).PendingTarget, Is.EqualTo(_upper));
        });

        await Pair.RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SComp<CMUElevatorComponent>(_control).PendingTarget, Is.Null);
            Assert.That(SComp<TransformComponent>(_control).MapUid, Is.EqualTo(_upper));
            Assert.That(SComp<TransformComponent>(_rider).MapUid, Is.EqualTo(_upper));
            Assert.That(SComp<TransformComponent>(door).MapUid, Is.EqualTo(_upper));
            Assert.That(SComp<DoorComponent>(door).State, Is.EqualTo(DoorState.Closed));
            Assert.That(SComp<DoorBoltComponent>(door).BoltsDown, Is.False);
        });

        void Select(EntityUid target)
        {
            SEntMan.EventBus.RaiseLocalEvent(_control,
                new CMUElevatorFloorSelectedEvent(SEntMan.GetNetEntity(_rider), SEntMan.GetNetEntity(target)));
        }
    }

    [Test]
    public async Task LandingDoorsStayBoltedUntilTheCarWaitsOnTheirFloor()
    {
        EntityUid lowerLanding = default, upperLanding = default;
        await Server.WaitAssertion(() =>
        {
            var plating = new Tile(Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);
            _maps.SetTile(_upper, SComp<MapGridComponent>(_upper), new Vector2i(1, -1), plating);
            _maps.SetTile(_lower, SComp<MapGridComponent>(_lower), new Vector2i(1, -1), plating);
            lowerLanding = Spawn(_lower);
            upperLanding = Spawn(_upper);
        });

        await Pair.RunSeconds(1.5f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SComp<DoorBoltComponent>(upperLanding).BoltsDown, Is.True,
                "The landing door of a floor the car isn't on should be bolted.");
            Assert.That(SComp<DoorBoltComponent>(lowerLanding).BoltsDown, Is.False);

            SEntMan.EventBus.RaiseLocalEvent(_control,
                new CMUElevatorFloorSelectedEvent(SEntMan.GetNetEntity(_rider), SEntMan.GetNetEntity(_upper)));
            Assert.That(SComp<TransformComponent>(_control).MapUid, Is.EqualTo(_upper));
            Assert.That(SComp<TransformComponent>(lowerLanding).MapUid, Is.EqualTo(_lower),
                "Landing doors are not part of the car.");
            Assert.That(SComp<DoorBoltComponent>(upperLanding).BoltsDown, Is.False);
        });

        await Pair.RunSeconds(1.5f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SComp<DoorBoltComponent>(lowerLanding).BoltsDown, Is.True);
            Assert.That(SComp<DoorBoltComponent>(upperLanding).BoltsDown, Is.False);
        });

        EntityUid Spawn(EntityUid map)
        {
            var door = SEntMan.SpawnEntity("CMUElevatorLandingDoor", new EntityCoordinates(map, new Vector2(1.5f, -0.5f)));
            SComp<CMUElevatorRailComponent>(door).ElevatorId = "test-lift";
            return door;
        }
    }

    [Test]
    public async Task RetractableRailingRisesInTransitAndLowersOnArrival()
    {
        EntityUid gate = default;
        await Server.WaitAssertion(() =>
        {
            foreach (var rail in _rails)
                SEntMan.DeleteEntity(rail);
            _rails.Clear();

            var xformSystem = SEntMan.System<SharedTransformSystem>();
            foreach (var facing in new[] { Direction.North, Direction.East, Direction.South, Direction.West })
            {
                var prototype = facing == Direction.South ? "CMUElevatorRetractableRail" : "CMUElevatorRail";
                var rail = SEntMan.SpawnEntity(prototype, new EntityCoordinates(_lower, new Vector2(1.5f)));
                xformSystem.SetLocalRotation(rail, facing.ToAngle());
                SComp<CMUElevatorRailComponent>(rail).ElevatorId = "test-lift";
                _rails.Add(rail);
                if (facing == Direction.South)
                    gate = rail;
            }

            SComp<CMUElevatorComponent>(_control).TravelTime = TimeSpan.FromSeconds(2);
        });

        await Pair.RunTicksSync(5);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SComp<RequisitionsRailingComponent>(gate).Mode, Is.EqualTo(RequisitionsRailingMode.Lowered));
            SEntMan.EventBus.RaiseLocalEvent(_control,
                new CMUElevatorFloorSelectedEvent(SEntMan.GetNetEntity(_rider), SEntMan.GetNetEntity(_upper)));
            Assert.That(SComp<CMUElevatorComponent>(_control).PendingTarget, Is.EqualTo(_upper));
            Assert.That(SComp<RequisitionsRailingComponent>(gate).Mode, Is.EqualTo(RequisitionsRailingMode.Raising));
        });

        await Pair.RunSeconds(3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SComp<TransformComponent>(_control).MapUid, Is.EqualTo(_upper));
            Assert.That(SComp<TransformComponent>(gate).MapUid, Is.EqualTo(_upper));
            Assert.That(SComp<RequisitionsRailingComponent>(gate).Mode, Is.EqualTo(RequisitionsRailingMode.Lowering));
        });
    }

    [Test]
    public async Task HomeFloorKeepsPlatingAndAcceptsTheCarBack()
    {
        await Server.WaitAssertion(() =>
        {
            var tiles = Server.ResolveDependency<ITileDefinitionManager>();
            SComp<CMUElevatorComponent>(_control).HomeFloorTile = "CMFloorPlating";
            var lowerGrid = SComp<MapGridComponent>(_lower);

            Select(_upper);
            Assert.That(SComp<TransformComponent>(_control).MapUid, Is.EqualTo(_upper));
            Assert.That(_maps.GetTileRef(_lower, lowerGrid, new Vector2i(1, 1)).Tile.TypeId,
                Is.EqualTo(tiles["CMFloorPlating"].TileId));

            Select(_lower);
            Assert.That(SComp<TransformComponent>(_control).MapUid, Is.EqualTo(_lower));
            Assert.That(_maps.GetTileRef(_lower, lowerGrid, new Vector2i(1, 1)).Tile.TypeId,
                Is.EqualTo(tiles["Plating"].TileId), "The car's own floor replaces the home plating.");
            Assert.That(_maps.GetTileRef(_upper, SComp<MapGridComponent>(_upper), new Vector2i(1, 1)).Tile.IsEmpty,
                Is.True, "Only the home floor keeps plating.");
        });

        void Select(EntityUid target)
        {
            SEntMan.EventBus.RaiseLocalEvent(_control,
                new CMUElevatorFloorSelectedEvent(SEntMan.GetNetEntity(_rider), SEntMan.GetNetEntity(target)));
        }
    }

    [Test]
    public async Task MobsInThePathAreCrushedWhileGhostsAndIntangiblesAreIgnored()
    {
        EntityUid victim = default;
        await Server.WaitAssertion(() =>
        {
            victim = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(_upper, new Vector2(0.5f)));
            var upperGhost = SEntMan.SpawnEntity("AdminObserver", new EntityCoordinates(_upper, new Vector2(2.5f)));
            var lowerGhost = SEntMan.SpawnEntity("AdminObserver", new EntityCoordinates(_lower, new Vector2(1.5f)));
            var marker = SEntMan.SpawnEntity(null, new EntityCoordinates(_upper, new Vector2(1.5f, 0.5f)));

            SEntMan.EventBus.RaiseLocalEvent(_control,
                new CMUElevatorFloorSelectedEvent(SEntMan.GetNetEntity(_rider), SEntMan.GetNetEntity(_upper)));

            Assert.That(SComp<TransformComponent>(_control).MapUid, Is.EqualTo(_upper));
            Assert.That(SComp<TransformComponent>(_rider).MapUid, Is.EqualTo(_upper));
            Assert.That(SEntMan.Deleted(victim) ||
                        SEntMan.IsQueuedForDeletion(victim) ||
                        SEntMan.System<MobStateSystem>().IsDead(victim),
                Is.True, "A mob in the car's path must be crushed.");
            Assert.That(SComp<TransformComponent>(lowerGhost).MapUid, Is.EqualTo(_lower), "Ghosts are not carried.");
            Assert.That(SEntMan.Deleted(upperGhost), Is.False, "Ghosts are not crushed.");
            Assert.That(SEntMan.Deleted(marker), Is.False);

            // Mindless observers carry dangling action references that PVS reports as errors.
            SEntMan.DeleteEntity(upperGhost);
            SEntMan.DeleteEntity(lowerGhost);
        });
    }

    [TestCase(21, false)]
    [TestCase(22, true)]
    public async Task ContainedEntitiesCountTowardTheWeightLimit(int maxEntities, bool moves)
    {
        await Server.WaitAssertion(() =>
        {
            SEntMan.DeleteEntity(_rider);
            var center = new EntityCoordinates(_lower, new Vector2(1.5f));
            var backpack = SEntMan.SpawnEntity("CMBackpack", center);
            var storage = SEntMan.System<SharedStorageSystem>();
            for (var i = 0; i < 21; i++)
                Assert.That(storage.Insert(backpack, SEntMan.SpawnEntity("CMPen", center), out _, playSound: false), Is.True);

            SComp<CMUElevatorWeightLimitComponent>(_control).MaxEntities = maxEntities;
            var caller = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(_upper, new Vector2(5.5f)));
            var button = SEntMan.SpawnEntity("CMUElevatorCallButton", new EntityCoordinates(_upper, new Vector2(4.5f)));
            SComp<CMUElevatorCallButtonComponent>(button).ElevatorId = "test-lift";
            SEntMan.EventBus.RaiseLocalEvent(button, new ActivateInWorldEvent(caller, button, true));

            // The backpack plus its 21 pens is 22 entities.
            Assert.That(SComp<TransformComponent>(_control).MapUid, Is.EqualTo(moves ? _upper : _lower));
            Assert.That(SComp<TransformComponent>(backpack).MapUid, Is.EqualTo(moves ? _upper : _lower));
            Assert.That(SComp<CMUElevatorComponent>(_control).Disabled, Is.EqualTo(!moves));
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
