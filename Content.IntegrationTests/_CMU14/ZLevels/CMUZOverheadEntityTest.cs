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
                    scene.Z.PrepareOverheadPvs();
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
