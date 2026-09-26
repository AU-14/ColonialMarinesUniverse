using System.Numerics;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Hijack;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared.CMU14.Hijack;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.CMU14.TileMovement;
using Content.Shared.Gravity;
using Content.Shared.Maps;
using Content.Server.Physics.Controllers;
using Content.Shared.DoAfter;
using Content.Shared.Verbs;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.CMU14.Hijack;

[TestFixture]
public sealed class AlmayerStairsTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task MiddleThroughLadderClimbsInBothDirections()
    {
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            var transforms = Server.System<SharedTransformSystem>();
            var z = Server.System<CMUZLevelsSystem>();
            var lower = maps.CreateMap(out _, runMapInit: true);
            var middle = maps.CreateMap(out _, runMapInit: true);
            var upper = maps.CreateMap(out _, runMapInit: true);
            Assert.That(z.TryAddMapsIntoZNetwork(z.CreateZNetwork(), new() { [lower] = 0, [middle] = 1, [upper] = 2 }), Is.True);
            var coordinates = new EntityCoordinates(middle, new Vector2(.5f));
            var ladder = SEntMan.SpawnEntity(null, coordinates);
            var component = SEntMan.AddComponent<CMUZLevelLadderComponent>(ladder);
            component.Offset = 1;
            component.AdditionalOffset = -1;
            component.Delay = System.TimeSpan.Zero;
            var user = SEntMan.SpawnEntity(null, coordinates);
            SEntMan.AddComponent<DoAfterComponent>(user);
            foreach (var (key, target) in new[] { ("cmu-zlevel-ladder-climb-up", upper), ("cmu-zlevel-ladder-climb-down", lower) })
            {
                transforms.SetCoordinates(user, coordinates);
                var verbs = new GetVerbsEvent<AlternativeVerb>(user, ladder, null, null, true, true, true, []);
                SEntMan.EventBus.RaiseLocalEvent(ladder, verbs, true);
                var verb = verbs.Verbs.Single(v => v.Text == Robust.Shared.Localization.Loc.GetString(key));
                verb.Act!();
                Assert.That(SEntMan.GetComponent<TransformComponent>(user).MapUid, Is.EqualTo(target));
            }
        });
    }

    [Test]
    public async Task DirectedStairsRespectSideLandingAndObstruction()
    {
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            var transforms = Server.System<SharedTransformSystem>();
            var z = Server.System<CMUZLevelsSystem>();
            var stairsSystem = Server.System<AlmayerStairsSystem>();
            var lower = maps.CreateMap(out var lowerId, runMapInit: true);
            var upper = maps.CreateMap(out var upperId, runMapInit: true);
            var bottomGrid = maps.CreateGridEntity(lowerId);
            var topGrid = maps.CreateGridEntity(upperId);
            SEntMan.AddComponent<CMUZLevelDeckComponent>(bottomGrid);
            SEntMan.AddComponent<CMUZLevelDeckComponent>(topGrid);
            foreach (var grid in new[] { bottomGrid, topGrid })
            {
                transforms.SetWorldPosition(grid, new Vector2(15, 20));
                for (var x = -1; x <= 2; x++)
                for (var y = -1; y <= 1; y++)
                    maps.SetTile(grid, grid.Comp, new Vector2i(x, y), new Tile(1));
            }
            Assert.That(z.TryAddMapsIntoZNetwork(z.CreateZNetwork(), new() { [lower] = 0, [upper] = 1 }), Is.True);
            var stair = SEntMan.SpawnEntity(null, new EntityCoordinates(bottomGrid, new Vector2(.5f)));
            transforms.AnchorEntity(stair, SEntMan.GetComponent<TransformComponent>(stair));
            var comp = SEntMan.AddComponent<CMUAlmayerStairsComponent>(stair);
            comp.Direction = Vector2.UnitX;
            comp.Offset = 1;
            var actor = SEntMan.SpawnEntity(null, new EntityCoordinates(bottomGrid, new Vector2(.5f)));
            SEntMan.AddComponent<MobStateComponent>(actor);
            SEntMan.AddComponent<InputMoverComponent>(actor);

            // Sideways movement does not change deck.
            transforms.SetCoordinates(actor, new EntityCoordinates(bottomGrid, new Vector2(.5f, .9f)));
            stairsSystem.Update(.05f);
            Assert.That(SEntMan.GetComponent<TransformComponent>(actor).MapUid, Is.EqualTo(lower));

            transforms.SetCoordinates(actor, new EntityCoordinates(bottomGrid, new Vector2(.9f, .5f)));
            stairsSystem.Update(.05f);
            Assert.That(SEntMan.GetComponent<TransformComponent>(actor).MapUid, Is.EqualTo(upper));
            Assert.That(transforms.GetWorldPosition(actor), Is.EqualTo(new Vector2(16.5f, 20.5f)));

            var wall = SEntMan.SpawnEntity("CMWallReinforcedAlmayer", new EntityCoordinates(topGrid, new Vector2(1.5f, .5f)));
            transforms.SetCoordinates(actor, new EntityCoordinates(bottomGrid, new Vector2(.5f)));
            Assert.That(stairsSystem.Traverse(actor, (stair, comp)), Is.False);
            Assert.That(SEntMan.GetComponent<TransformComponent>(actor).MapUid, Is.EqualTo(lower));
            SEntMan.DeleteEntity(wall);

            // A fast step crossing the tile boundary must still find the old stair tile.
            transforms.SetCoordinates(actor, new EntityCoordinates(bottomGrid, new Vector2(1.1f, .5f)));
            stairsSystem.Update(.05f);
            Assert.That(SEntMan.GetComponent<TransformComponent>(actor).MapUid, Is.EqualTo(upper));
        });
    }

    [TestCase("CMMobHuman", false)]
    [TestCase("CMMobHuman", true)]
    [TestCase("MobObserver", false)]
    public async Task RealMoverAutomaticallyWalksBothWaysPastTheSourceWall(string prototype, bool tileMovement)
    {
        EntityUid lower = default, upper = default, actor = default;
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            var transforms = Server.System<SharedTransformSystem>();
            var z = Server.System<CMUZLevelsSystem>();
            lower = maps.CreateMap(out var lowerId, runMapInit: true);
            upper = maps.CreateMap(out var upperId, runMapInit: true);
            var settings = Server.ProtoMan.Index<GameMapPrototype>("Almayer").ZLevelsComponentOverrides;
            SEntMan.AddComponents(lower, settings);
            SEntMan.AddComponents(upper, settings);
            var bottomGrid = maps.CreateGridEntity(lowerId);
            var topGrid = maps.CreateGridEntity(upperId);
            foreach (var grid in new[] { bottomGrid, topGrid })
            {
                SEntMan.AddComponent<CMUZLevelDeckComponent>(grid);
                var gravity = SEntMan.EnsureComponent<GravityComponent>(grid);
                gravity.Enabled = true;
                gravity.Inherent = true;
                for (var x = -4; x <= 6; x++)
                for (var y = -2; y <= 2; y++)
                    maps.SetTile(grid, grid.Comp, new Vector2i(x, y), new Tile(Server.ResolveDependency<ITileDefinitionManager>()["CMFloorPlating"].TileId));
            }
            Assert.That(z.TryAddMapsIntoZNetwork(z.CreateZNetwork(), new() { [lower] = 0, [upper] = 1 }), Is.True);
            foreach (var (grid, x, direction, offset) in new[]
                     { (bottomGrid, .5f, Vector2.UnitX, 1), (topGrid, 1.5f, -Vector2.UnitX, -1) })
            {
                var stairs = SEntMan.SpawnEntity(null, new EntityCoordinates(grid, new Vector2(x, .5f)));
                transforms.AnchorEntity(stairs, SComp<TransformComponent>(stairs));
                var component = SEntMan.AddComponent<CMUAlmayerStairsComponent>(stairs);
                component.Direction = direction;
                component.Offset = offset;
            }
            SEntMan.SpawnEntity("CMWallReinforcedAlmayer", new EntityCoordinates(bottomGrid, new Vector2(1.5f, .5f)));
            actor = SEntMan.SpawnEntity(prototype, new EntityCoordinates(bottomGrid, new Vector2(-.5f, .5f)));
            Server.PlayerMan.SetAttachedEntity(Pair.Player!, actor);
            if (prototype == "MobObserver")
                transforms.SetCoordinates(actor, new EntityCoordinates(lower, new Vector2(-.5f, .5f)));
            if (tileMovement)
                SEntMan.EnsureComponent<CMUTileMovementComponent>(actor);
            SetDirection(Direction.East, true);
        });
        await WalkTo(upper);
        await Server.WaitAssertion(() => SetDirection(Direction.West, true));
        await WalkTo(lower);

        async Task WalkTo(EntityUid destination)
        {
            var arrived = false;
            var last = string.Empty;
            for (var tick = 0; tick < 60 && !arrived; tick++)
            {
                await Server.WaitRunTicks(1);
                await Server.WaitAssertion(() =>
                {
                    arrived = SComp<TransformComponent>(actor).MapUid == destination;
                    last = $"map={SComp<TransformComponent>(actor).MapUid}, target={destination}, position={SComp<TransformComponent>(actor).LocalPosition}, input={SEntMan.HasComponent<InputMoverComponent>(actor)}";
                    if (arrived)
                    {
                        SetDirection(Direction.East, false);
                        SetDirection(Direction.West, false);
                    }
                });
            }
            Assert.That(arrived, Is.True, $"{prototype}, tile={tileMovement}: ordinary movement must change deck without activating the stairs. {last}");
            NetEntity actorNet = default, destinationNet = default;
            await Server.WaitAssertion(() =>
            {
                actorNet = SEntMan.GetNetEntity(actor);
                destinationNet = SEntMan.GetNetEntity(destination);
            });
            await Pair.RunUntilSynced();
            await Client.WaitAssertion(() =>
            {
                var clientActor = CEntMan.GetEntity(actorNet);
                Assert.That(CEntMan.GetComponent<TransformComponent>(clientActor).MapUid,
                    Is.EqualTo(CEntMan.GetEntity(destinationNet)), "The controlled client's map must follow the stair transition.");
                var clientMap = CEntMan.GetEntity(destinationNet);
                Assert.That(CEntMan.GetComponent<CMUZLevelMapComponent>(clientMap).VisualOffset, Is.Zero,
                    "Aligned Almayer decks must not acquire the terrain's 3/4-tile parallax after replication.");
                Assert.That(Client.System<Content.Client.CMU14.ZLevels.Core.CMUClientZLevelsSystem>()
                    .GetZLevelVisualOffset(clientMap), Is.Zero);
            });
        }
        void SetDirection(Direction direction, bool pressed) => Server.System<MoverController>()
            .SetVelocityDirection((actor, SComp<InputMoverComponent>(actor)), direction, 0, pressed);
    }
}
