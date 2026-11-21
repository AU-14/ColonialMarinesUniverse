using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Xenomorphs.Swarm;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared._RMC14.Ladder;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.Gravity;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.CMU14.Xenomorphs;

[TestFixture]
public sealed class CMUSwarmTransitionTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [TestCase(false)]
    [TestCase(true)]
    public async Task LesserUsesLadderToFollowAnchorAndReturn(bool linked)
    {
        EntityUid lower = default;
        EntityUid upper = default;
        EntityUid network = default;
        EntityUid anchor = default;
        EntityUid lesser = default;
        try
        {
            await Server.WaitAssertion(() =>
            {
                var maps = Server.System<SharedMapSystem>();
                lower = FloorMap(maps);
                upper = FloorMap(maps);
                if (!linked)
                {
                    var z = Server.System<CMUZLevelsSystem>();
                    var createdNetwork = z.CreateZNetwork();
                    network = createdNetwork;
                    Assert.That(z.TryAddMapsIntoZNetwork(createdNetwork, new() { [lower] = 0, [upper] = 1 }), Is.True);
                }

                var bottom = SEntMan.SpawnEntity("CMUSwarmGuardMarker", new EntityCoordinates(lower, 0.5f, 0.5f));
                var top = SEntMan.SpawnEntity("CMUSwarmGuardMarker", new EntityCoordinates(upper, 0.5f, 0.5f));
                if (linked)
                {
                    SEntMan.AddComponent<LadderComponent>(bottom).Other = top;
                    SEntMan.AddComponent<LadderComponent>(top).Other = bottom;
                }
                else
                {
                    SEntMan.AddComponent<CMUZLevelLadderComponent>(bottom);
                    SEntMan.AddComponent<CMUZLevelLadderComponent>(top);
                }
                anchor = SEntMan.SpawnEntity("CMUSwarmGuardMarker", new EntityCoordinates(upper, 2.5f, 0.5f));
            });

            await Pair.RunTicksSync(60);
            await Server.WaitAssertion(() =>
            {
                lesser = CreateFollower(new EntityCoordinates(lower, -2.5f, 0.5f), anchor);
            });
            await Pair.RunSeconds(8);
            await Server.WaitAssertion(() =>
            {
                Assert.That(SEntMan.GetComponent<TransformComponent>(lesser).MapUid, Is.EqualTo(upper),
                    "The lesser must use the ordinary ladder climb to follow its anchor.");
                Server.System<SharedTransformSystem>().SetCoordinates(anchor, new EntityCoordinates(lower, -2.5f, 0.5f));
            });
            await Pair.RunSeconds(8);
            await Server.WaitAssertion(() =>
                Assert.That(SEntMan.GetComponent<TransformComponent>(lesser).MapUid, Is.EqualTo(lower),
                    "The lesser must also find the return ladder route."));
        }
        finally
        {
            if (lower.IsValid())
                await Pair.DeleteEntityTreeLeafFirst(lower);
            if (upper.IsValid())
                await Pair.DeleteEntityTreeLeafFirst(upper);
            if (network.IsValid())
                await Pair.DeleteEntityTreeLeafFirst(network);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task LesserBoardsSeparateGridAndDisembarksOnlyAcrossFloor(bool openAir)
    {
        EntityUid map = default;
        EntityUid ground = default;
        EntityUid ship = default;
        EntityUid lesser = default;
        EntityUid anchor = default;
        try
        {
            await Server.WaitAssertion(() =>
            {
                var maps = Server.System<SharedMapSystem>();
                var floor = new Tile(Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);
                map = maps.CreateMap(out var mapId, runMapInit: true);
                ground = maps.CreateGridEntity(mapId);
                ship = maps.CreateGridEntity(mapId);
                var groundGrid = SEntMan.GetComponent<MapGridComponent>(ground);
                var shipGrid = SEntMan.GetComponent<MapGridComponent>(ship);
                for (var x = -5; x <= -1; x++)
                for (var y = -2; y <= 2; y++)
                    maps.SetTile(ground, groundGrid, new Vector2i(x, y), floor);
                for (var x = 0; x <= 4; x++)
                for (var y = -2; y <= 2; y++)
                    maps.SetTile(ship, shipGrid, new Vector2i(x, y), floor);
                if (openAir)
                {
                    var hole = new Tile(Server.ResolveDependency<ITileDefinitionManager>()["CMUFloorOpenAirSealed"].TileId);
                    for (var y = -2; y <= 2; y++)
                        maps.SetTile(ship, shipGrid, new Vector2i(0, y), hole);
                }
                anchor = SEntMan.SpawnEntity("CMUSwarmGuardMarker", new EntityCoordinates(ship, 3.5f, 0.5f));
            });

            await Pair.RunTicksSync(60);
            await Server.WaitAssertion(() =>
                lesser = CreateFollower(new EntityCoordinates(ground, -3.5f, 0.5f), anchor));
            await Pair.RunSeconds(6);
            if (openAir)
            {
                await Server.WaitAssertion(() =>
                    Assert.That(SEntMan.GetComponent<TransformComponent>(lesser).GridUid, Is.EqualTo(ground),
                        "A nonempty open-air tile must not make a gap qualify as a boarding path."));
                return;
            }
            await Server.WaitAssertion(() =>
            {
                Assert.That(SEntMan.GetComponent<TransformComponent>(lesser).GridUid, Is.EqualTo(ship),
                    "A separate navmesh must not stop the lesser crossing a physically connected boarding path.");
                Server.System<SharedTransformSystem>().SetCoordinates(anchor, new EntityCoordinates(ground, -3.5f, 0.5f));
            });
            await Pair.RunSeconds(6);
            await Server.WaitAssertion(() =>
                Assert.That(SEntMan.GetComponent<TransformComponent>(lesser).GridUid, Is.EqualTo(ground),
                    "The same path must let the lesser leave the ship."));
        }
        finally
        {
            if (map.IsValid())
                await Pair.DeleteEntityTreeLeafFirst(map);
        }
    }

    private EntityUid FloorMap(SharedMapSystem maps)
    {
        var map = maps.CreateMap(runMapInit: true);
        var grid = SEntMan.EnsureComponent<MapGridComponent>(map);
        var gravity = SEntMan.EnsureComponent<GravityComponent>(map);
        gravity.Enabled = true;
        gravity.Inherent = true;
        var floor = new Tile(Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);
        for (var x = -5; x <= 5; x++)
        for (var y = -2; y <= 2; y++)
            maps.SetTile(map, grid, new Vector2i(x, y), floor);
        return map;
    }

    private EntityUid CreateFollower(EntityCoordinates coordinates, EntityUid anchor)
    {
        var hive = SEntMan.SpawnEntity("CMXenoHive", coordinates);
        var swarm = SEntMan.AddComponent<CMUSwarmControllerComponent>(hive);
        swarm.Anchor = anchor;
        swarm.Passive = true;
        var lesser = SEntMan.SpawnEntity("CMUSwarmLesser", coordinates);
        Server.System<SharedXenoHiveSystem>().SetHive(lesser, hive);
        swarm.Members.Add(lesser);
        return lesser;
    }
}
