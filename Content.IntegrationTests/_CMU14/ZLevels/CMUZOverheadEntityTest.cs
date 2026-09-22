using System.Numerics;
using Content.Client.CMU14.ZLevels.Core;
using Content.Client.Viewport;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared.CMU14.Threats.Mobs.Xeno.ZJump;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.DoAfter;
using Content.Shared.Item;
using Content.Shared.Throwing;
using Robust.Client.Graphics;
using Robust.Server.GameObjects;
using Robust.Server.GameStates;
using Robust.Shared.GameObjects;
using Robust.Shared.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.CMU14.ZLevels;

[TestFixture]
public sealed class CMUZOverheadEntityTest : GameTest
{
    [Test]
    public async Task UpwardThrowRemainsVisibleAfterHorizontalThrowEnds()
    {
        await Server.WaitAssertion(() =>
        {
            using var scene = new Scene(SEntMan, Server.ResolveDependency<ITileDefinitionManager>());
            var user = SEntMan.SpawnEntity(null, new EntityCoordinates(scene.Levels[0], new Vector2(0.5f)));
            // Set up the action state without depending on a player-controlled mob.
#pragma warning disable RA0002
            SEntMan.AddComponent<CMUZLevelViewerComponent>(user).LookUp = true;
#pragma warning restore RA0002
            var item = scene.SpawnItem(0);
            Assert.That(SEntMan.System<ThrowingSystem>().TryThrow(item, Vector2.UnitX * 5f,
                user: user, playSound: false), Is.True);
            Assert.That(SComp<CMUZPhysicsComponent>(item).Velocity, Is.GreaterThan(0f));
            Assert.That(SEntMan.HasComponent<CMUZFallingComponent>(item), Is.True);
            Assert.That(scene.Z.TryGetOverheadEntityProjection(item, scene.Levels[0], out _, out _), Is.False,
                "On the viewer's own map the normal sprite is already visible.");

            // Advance the real Z boundary controller past the first upward crossing.
            scene.Z.SetZLocalPosition(item, 1.1f);
            scene.Z.Update(0f);
            Assert.That(SComp<TransformComponent>(item).MapUid, Is.EqualTo(scene.Levels[1]));
            Assert.That(scene.Z.TryGetOverheadEntityProjection(item, scene.Levels[0], out var projection, out var height), Is.True);
            Assert.That(projection.MapId, Is.EqualTo(SComp<MapComponent>(scene.Levels[0]).MapId));
            Assert.That(height, Is.EqualTo(1.1f).Within(0.001f));

            SEntMan.System<ThrownItemSystem>().StopThrow(item, SComp<ThrownItemComponent>(item));
            Assert.That(SEntMan.HasComponent<ThrownItemComponent>(item), Is.False);
            Assert.That(scene.Z.TryGetOverheadEntityProjection(item, scene.Levels[0], out _, out _), Is.True,
                "Ending horizontal flight must not hide an item still overhead.");

            scene.Transform.SetCoordinates(item, new EntityCoordinates(scene.Levels[0], new Vector2(0.5f)));
            scene.Z.SetZLocalPosition(item, 0f);
            scene.Z.SetZVelocity(item, 0f);
            Assert.That(scene.Z.TryGetOverheadEntityProjection(item, scene.Levels[0], out _, out _), Is.False);
        });
    }

    [TestCase("CrateGenericSteel")]
    [TestCase("CMXenoRunner")]
    public async Task FallingObjectsAndMobsRemainVisibleUntilTheyReachTheViewersMap(string prototype)
    {
        await Server.WaitAssertion(() =>
        {
            using var scene = new Scene(SEntMan, Server.ResolveDependency<ITileDefinitionManager>());
            var entity = scene.SpawnEntity(2, prototype);
            Assert.That(SEntMan.HasComponent<ItemComponent>(entity), Is.False);
            Assert.That(SEntMan.HasComponent<ThrownItemComponent>(entity), Is.False);
            scene.Z.SetZVelocity(entity, -1f);
            Assert.That(scene.Z.TryGetOverheadEntityProjection(entity, scene.Levels[0], out _, out var height), Is.True);
            Assert.That(height, Is.EqualTo(2.5f));

            // Cross an actual downward boundary, retaining visibility from the map below.
            scene.Z.SetZLocalPosition(entity, -0.1f);
            scene.Z.Update(0f);
            Assert.That(SComp<TransformComponent>(entity).MapUid, Is.EqualTo(scene.Levels[1]));
            Assert.That(scene.Z.TryGetOverheadEntityProjection(entity, scene.Levels[0], out _, out height), Is.True);
            Assert.That(height, Is.EqualTo(1.9f).Within(0.001f));

            scene.Transform.SetCoordinates(entity, new EntityCoordinates(scene.Levels[0], new Vector2(0.5f)));
            scene.Z.SetZLocalPosition(entity, 0f);
            scene.Z.SetZVelocity(entity, 0f);
            Assert.That(scene.Z.TryGetOverheadEntityProjection(entity, scene.Levels[0], out _, out _), Is.False);
        });
    }

    [Test]
    public async Task LeapingXenoRemainsVisibleAfterCrossingUpward()
    {
        await Server.WaitAssertion(() =>
        {
            using var scene = new Scene(SEntMan, Server.ResolveDependency<ITileDefinitionManager>());
            var xeno = scene.SpawnEntity(0, "CMXenoRunner");
            Assert.That(SEntMan.HasComponent<ItemComponent>(xeno), Is.False);
            var target = new EntityCoordinates(scene.Levels[0], new Vector2(5.5f, 0.5f));
            var leap = new CMUXenoZJumpDoAfterEvent(SEntMan.GetNetCoordinates(target));
            leap.DoAfter = new DoAfter(0,
                new DoAfterArgs(SEntMan, xeno, TimeSpan.Zero, leap, xeno), TimeSpan.Zero);
            SEntMan.EventBus.RaiseLocalEvent(xeno, leap);
            Assert.That(leap.Handled, Is.True);
            Assert.That(SComp<CMUZPhysicsComponent>(xeno).Velocity, Is.GreaterThan(0f));
            Assert.That(SEntMan.HasComponent<CMUZFallingComponent>(xeno), Is.True);

            scene.Z.SetZLocalPosition(xeno, 1.1f);
            scene.Z.Update(0f);
            Assert.That(SComp<TransformComponent>(xeno).MapUid, Is.EqualTo(scene.Levels[1]));
            Assert.That(scene.Z.TryGetOverheadEntityProjection(xeno, scene.Levels[0], out _, out var height), Is.True);
            Assert.That(height, Is.EqualTo(1.1f).Within(0.001f));
        });
    }

    [TestCase(1, "Pen")]
    [TestCase(2, "Pen")]
    [TestCase(1, "CMXenoRunner")]
    [TestCase(2, "CMXenoRunner")]
    public async Task FloorsBlockProjectionAtEveryInterveningLevel(int floorDepth, string prototype)
    {
        await Server.WaitAssertion(() =>
        {
            using var scene = new Scene(SEntMan, Server.ResolveDependency<ITileDefinitionManager>());
            var item = scene.SpawnEntity(2, prototype);
            Assert.That(scene.Z.TryGetOverheadEntityProjection(item, scene.Levels[0], out _, out var height), Is.True);
            Assert.That(height, Is.EqualTo(2.5f));

            scene.Maps.SetTile(scene.Levels[floorDepth], scene.Grids[floorDepth], Vector2i.Zero, scene.Floor);
            Assert.That(scene.Z.TryGetOverheadEntityProjection(item, scene.Levels[0], out _, out _), Is.False);
            scene.Maps.SetTile(scene.Levels[floorDepth], scene.Grids[floorDepth], Vector2i.Zero, Tile.Empty);
            Assert.That(scene.Z.TryGetOverheadEntityProjection(item, scene.Levels[0], out _, out _), Is.True);

            var nextPosition = new Vector2(2.5f, 0.5f);
            scene.Transform.SetCoordinates(item, new EntityCoordinates(scene.Levels[2], nextPosition));
            Assert.That(scene.Z.TryGetOverheadEntityProjection(item, scene.Levels[0], out var projection, out _), Is.True);
            Assert.That(projection.Position, Is.EqualTo(nextPosition), "Projection follows the item, not its landing target.");
        });
    }

    [Test]
    public async Task GroundedHeldAndDisconnectedItemsAreNotProjected()
    {
        await Server.WaitAssertion(() =>
        {
            using var scene = new Scene(SEntMan, Server.ResolveDependency<ITileDefinitionManager>());
            var item = scene.SpawnItem(2);
            SEntMan.RemoveComponent<CMUZFallingComponent>(item);
            Assert.That(scene.Z.TryGetOverheadEntityProjection(item, scene.Levels[0], out _, out _), Is.False);
            SEntMan.EnsureComponent<CMUZFallingComponent>(item);
            var holder = SEntMan.SpawnEntity(null, new EntityCoordinates(scene.Levels[2], Vector2.Zero));
            scene.Transform.SetCoordinates(item, new EntityCoordinates(holder, Vector2.Zero));
            Assert.That(scene.Z.TryGetOverheadEntityProjection(item, scene.Levels[0], out _, out _), Is.False);
            scene.Transform.SetCoordinates(item, new EntityCoordinates(scene.Levels[2], new Vector2(0.5f)));
            SEntMan.EnsureComponent<CMUZFallingComponent>(item);
            Assert.That(scene.Z.TryGetOverheadEntityProjection(item, scene.Levels[0], out _, out _), Is.True);
            Assert.That(scene.Z.TryRemoveMapFromZNetwork(scene.Levels[1]), Is.True);
            Assert.That(scene.Z.TryGetOverheadEntityProjection(item, scene.Levels[0], out _, out _), Is.False,
                "A gap in the network must not project into an unrelated lower map.");
        });
    }

    [TestCase("Pen")]
    [TestCase("CrateGenericSteel")]
    [TestCase("CMXenoRunner")]
    public async Task PvsIncludesNearbyOverheadEntityAndDropsBlockedOrDistantEntities(string prototype)
    {
        await Server.WaitAssertion(() =>
        {
            using var scene = new Scene(SEntMan, Server.ResolveDependency<ITileDefinitionManager>());
            var item = scene.SpawnEntity(2, prototype);
            var view = SEntMan.SpawnEntity(null, new EntityCoordinates(scene.Levels[0], new Vector2(0.5f)));
            SEntMan.EnsureComponent<EyeComponent>(view);
            var subscribers = SEntMan.System<ViewSubscriberSystem>();
            subscribers.AddViewSubscriber(view, ServerSession!);
            try
            {
                bool ReceivesItem()
                {
                    var ev = new ExpandPvsEvent(ServerSession!, 1);
                    SEntMan.EventBus.RaiseEvent(EventSource.Local, ref ev);
                    return ev.Entities?.Contains(item) == true;
                }

                Assert.That(ReceivesItem(), Is.True);
                scene.Maps.SetTile(scene.Levels[1], scene.Grids[1], Vector2i.Zero, scene.Floor);
                Assert.That(ReceivesItem(), Is.False);
                scene.Maps.SetTile(scene.Levels[1], scene.Grids[1], Vector2i.Zero, Tile.Empty);
                scene.Transform.SetCoordinates(view, new EntityCoordinates(scene.Levels[0], new Vector2(1000f)));
                Assert.That(ReceivesItem(), Is.False);
                scene.Transform.SetCoordinates(view, new EntityCoordinates(scene.Levels[0], new Vector2(0.5f)));
                Assert.That(ReceivesItem(), Is.True);
                SEntMan.RemoveComponent<CMUZFallingComponent>(item);
                Assert.That(ReceivesItem(), Is.False);
            }
            finally
            {
                subscribers.RemoveViewSubscriber(view, ServerSession!);
            }
        });
    }

    [Test]
    public void OnlyHighestViewportPassProjectsOverheadEntities()
    {
        Assert.That(CMUZOverheadEntityOverlay.ShouldDrawForEye(new Eye()), Is.True);
        Assert.That(CMUZOverheadEntityOverlay.ShouldDrawForEye(new ScalingViewport.ZEye { Depth = -1, HighestDepth = 0 }), Is.False);
        Assert.That(CMUZOverheadEntityOverlay.ShouldDrawForEye(new ScalingViewport.ZEye { Depth = 0, HighestDepth = 1 }), Is.False);
        Assert.That(CMUZOverheadEntityOverlay.ShouldDrawForEye(new ScalingViewport.ZEye { Depth = 1, HighestDepth = 1 }), Is.True);
    }

    private sealed class Scene : IDisposable
    {
        private readonly IEntityManager _entities;
        public readonly SharedMapSystem Maps;
        public readonly SharedTransformSystem Transform;
        public readonly CMUZLevelsSystem Z;
        public readonly EntityUid[] Levels = new EntityUid[3];
        public readonly MapGridComponent[] Grids = new MapGridComponent[3];
        public readonly Tile Floor;
        private readonly EntityUid _network;

        public Scene(IEntityManager entities, ITileDefinitionManager tiles)
        {
            _entities = entities;
            Maps = entities.System<SharedMapSystem>();
            Transform = entities.System<SharedTransformSystem>();
            Z = entities.System<CMUZLevelsSystem>();
            Floor = new Tile(tiles["Plating"].TileId);
            var network = Z.CreateZNetwork();
            _network = network;
            var levels = new Dictionary<EntityUid, int>();
            for (var depth = 0; depth < Levels.Length; depth++)
            {
                var map = Maps.CreateMap(runMapInit: true);
                Levels[depth] = map;
                Grids[depth] = entities.EnsureComponent<MapGridComponent>(map);
                Maps.SetTile(map, Grids[depth], new Vector2i(8, 8), Floor);
                levels.Add(map, depth);
            }
            Maps.SetTile(Levels[0], Grids[0], Vector2i.Zero, Floor);
            Assert.That(Z.TryAddMapsIntoZNetwork(network, levels), Is.True);
        }

        public EntityUid SpawnItem(int depth) => SpawnEntity(depth, "Pen");

        public EntityUid SpawnEntity(int depth, string prototype)
        {
            var item = _entities.SpawnEntity(prototype, new EntityCoordinates(Levels[depth], new Vector2(0.5f)));
            _entities.EnsureComponent<CMUZPhysicsComponent>(item);
            Z.SetZLocalPosition(item, depth == 0 ? 0f : 0.5f);
            return item;
        }

        public void Dispose()
        {
            foreach (var map in Levels)
                if (!_entities.Deleted(map)) _entities.DeleteEntity(map);
            if (!_entities.Deleted(_network)) _entities.DeleteEntity(_network);
        }
    }
}
