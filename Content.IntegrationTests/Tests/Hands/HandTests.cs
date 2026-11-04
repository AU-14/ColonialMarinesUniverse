using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Storage.EntitySystems;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Hands;

[TestFixture]
public sealed class HandTests : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: TestPickUpThenDropInContainerTestBox
  name: box
  components:
  - type: EntityStorage
  - type: ContainerContainer
    containers:
      entity_storage: !type:Container
";


    private EntityUid? _originalAttached;
    private EntityUid? _testPlayer;

    [SetUp]
    public async Task SetUpHandsPlayer()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitPost(() =>
        {
            _originalAttached = ServerSession!.AttachedEntity;
            // Round selection can attach the session to an observer, which has no hands.
            _testPlayer = SSpawnAtPosition("MobHuman", map.GridCoords);
            Server.PlayerMan.SetAttachedEntity(ServerSession, _testPlayer);
        });
        await Pair.RunUntilSynced();
    }

    [TearDown]
    public async Task TearDownHandsPlayer()
    {
        if (_testPlayer == null || !Server.IsAlive)
            return;

        await Server.WaitPost(() =>
        {
            Server.PlayerMan.SetAttachedEntity(ServerSession!, _originalAttached);
        });
        await Pair.RunUntilSynced();
    }

    [Test]
    public async Task TestPickupDrop()
    {
        var pair = Pair;
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var playerMan = server.ResolveDependency<IPlayerManager>();
        var sys = entMan.System<SharedHandsSystem>();

        await pair.RunTicksSync(5);

        EntityUid item = default;
        EntityUid player = default;
        HandsComponent hands = default!;
        await server.WaitPost(() =>
        {
            player = playerMan.Sessions.First().AttachedEntity!.Value;
            var xform = entMan.GetComponent<TransformComponent>(player);
            item = SSpawnAtPosition("Crowbar", xform.Coordinates);
            hands = entMan.GetComponent<HandsComponent>(player);
            sys.TryPickup(player, item, hands.ActiveHandId!);
        });

        // run ticks here is important, as errors may happen within the container system's frame update methods.
        await pair.RunTicksSync(5);
        Assert.That(sys.GetActiveItem((player, hands)), Is.EqualTo(item));

        await server.WaitPost(() =>
        {
            sys.TryDrop(player, item);
        });

        await pair.RunTicksSync(5);
        Assert.That(sys.GetActiveItem((player, hands)), Is.Null);

    }

    [Test]
    public async Task TestPickUpThenDropInContainer()
    {
        var pair = Pair;
        var server = pair.Server;
        var map = pair.TestMap!;
        await pair.RunTicksSync(5);

        var entMan = server.ResolveDependency<IEntityManager>();
        var playerMan = server.ResolveDependency<IPlayerManager>();
        var sys = entMan.System<SharedHandsSystem>();
        var tSys = entMan.System<TransformSystem>();
        var containerSystem = server.System<SharedContainerSystem>();

        EntityUid item = default;
        EntityUid box = default;
        EntityUid player = default;
        EntityCoordinates originalPlayerCoordinates = default;
        HandsComponent hands = default!;

        // spawn the elusive box and crowbar at the coordinates
        await server.WaitPost(() => box = SSpawnAtPosition("TestPickUpThenDropInContainerTestBox", map.GridCoords));
        await server.WaitPost(() => item = SSpawnAtPosition("Crowbar", map.GridCoords));
        // place the player at the exact same coordinates and have them grab the crowbar
        await server.WaitPost(() =>
        {
            player = playerMan.Sessions.First().AttachedEntity!.Value;
            originalPlayerCoordinates = entMan.GetComponent<TransformComponent>(player).Coordinates;
            tSys.PlaceNextTo(player, item);
            hands = entMan.GetComponent<HandsComponent>(player);
            sys.TryPickup(player, item, hands.ActiveHandId!);
        });
        await pair.RunTicksSync(5);
        Assert.That(sys.GetActiveItem((player, hands)), Is.EqualTo(item));

        // Open then close the box to place the player, who is holding the crowbar, inside of it
        var storage = server.System<EntityStorageSystem>();
        await server.WaitPost(() =>
        {
            storage.OpenStorage(box);
            storage.CloseStorage(box);
        });
        await pair.RunTicksSync(5);
        Assert.That(containerSystem.IsEntityInContainer(player), Is.True);

        // Dropping the item while the player is inside the box should cause the item
        // to also be inside the same container the player is in now,
        // with the item not being in the player's hands
        await server.WaitPost(() =>
        {
            sys.TryDrop(player, item);
        });
        await pair.RunTicksSync(5);
        var xform = entMan.GetComponent<TransformComponent>(player);
        var itemXform = entMan.GetComponent<TransformComponent>(item);
        Assert.That(sys.GetActiveItem((player, hands)), Is.Not.EqualTo(item));
        Assert.That(containerSystem.IsInSameOrNoContainer((player, xform), (item, itemXform)));

        var removedPlayer = false;
        await server.WaitPost(() =>
        {
            removedPlayer = containerSystem.TryRemoveFromContainer(player, force: true);
            if (removedPlayer)
                tSys.SetCoordinates(player, originalPlayerCoordinates);
        });
        Assert.That(removedPlayer, Is.True, "the test player must leave the storage tree before map cleanup");
        await pair.RunUntilSynced();
    }
}
