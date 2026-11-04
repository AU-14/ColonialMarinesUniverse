using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Xenomorphs.Swarm;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.Gravity;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.CMU14.Xenomorphs;

[TestFixture]
public sealed class CMUSwarmStairsTest : GameTest
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task SwarmFollowsAnchorUpAndDownLinkedFloors(bool directional)
    {
        var maps = new List<EntityUid>();
        var lessers = new List<EntityUid>();
        EntityUid network = default;
        EntityUid anchor = default;
        try
        {
            await Server.WaitAssertion(() =>
            {
                var mapSystem = SEntMan.System<SharedMapSystem>();
                var floor = new Tile(Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);
                for (var i = 0; i < 3; i++)
                {
                    var map = mapSystem.CreateMap(runMapInit: true);
                    maps.Add(map);
                    var grid = SEntMan.EnsureComponent<MapGridComponent>(map);
                    var gravity = SEntMan.EnsureComponent<GravityComponent>(map);
                    gravity.Enabled = true;
                    gravity.Inherent = true;
                    for (var x = -12; x <= 12; x++)
                    for (var y = -12; y <= 12; y++)
                        mapSystem.SetTile(map, grid, new Vector2i(x, y), floor);
                }
                var z = SEntMan.System<CMUZLevelsSystem>();
                var net = z.CreateZNetwork();
                network = net;
                Assert.That(z.TryAddMapsIntoZNetwork(net, new() { [maps[0]] = 0, [maps[1]] = 1, [maps[2]] = 2 }), Is.True);

                for (var i = 0; i < 2; i++)
                {
                    var center = new Vector2(i * 4 + 0.5f, 0.5f);
                    if (directional)
                    {
                        var up = SEntMan.SpawnEntity("CMUSwarmGuardMarker", new EntityCoordinates(maps[i], center));
                        var upStair = SEntMan.AddComponent<CMUZLevelStairsComponent>(up);
                        upStair.Direction = Direction.North;
                        var down = SEntMan.SpawnEntity("CMUSwarmGuardMarker", new EntityCoordinates(maps[i + 1], center));
                        var downStair = SEntMan.AddComponent<CMUZLevelStairsComponent>(down);
                        downStair.Direction = Direction.North;
                        downStair.Offset = -1;
                    }
                    else
                    {
                        var stair = SEntMan.SpawnEntity("CMUMultiZStairs", new EntityCoordinates(maps[i], center));
                        SEntMan.System<SharedTransformSystem>().SetLocalRotation(stair, Direction.South.ToAngle());
                        mapSystem.SetTile(maps[i + 1], SEntMan.GetComponent<MapGridComponent>(maps[i + 1]),
                            new Vector2i(i * 4, 0), Tile.Empty);
                    }
                }
            });

            // Fresh maps have no navmesh yet, lessers spawned now get NoPath and sit out the stair cooldown
            await Pair.RunTicksSync(60);
            await Server.WaitAssertion(() =>
            {
                var hive = SEntMan.SpawnEntity("CMXenoHive", new EntityCoordinates(maps[0], new Vector2(0.5f, -3.5f)));
                anchor = SEntMan.SpawnEntity("CMUSwarmGuardMarker", new EntityCoordinates(maps[2], new Vector2(4.5f, 3.5f)));
                var swarm = SEntMan.AddComponent<CMUSwarmControllerComponent>(hive);
                swarm.Anchor = anchor;
                swarm.Passive = true;
                for (var i = 0; i < 15; i++)
                {
                    var lesser = SEntMan.SpawnEntity("CMUSwarmLesser", new EntityCoordinates(maps[0], new Vector2(0.5f, -3.5f)));
                    SEntMan.System<SharedXenoHiveSystem>().SetHive(lesser, hive);
                    swarm.Members.Add(lesser);
                    lessers.Add(lesser);
                }
            });

            await Pair.RunTicksSync(2400);
            await Server.WaitAssertion(() =>
            {
                foreach (var lesser in lessers)
                    Assert.That(SEntMan.GetComponent<TransformComponent>(lesser).MapUid, Is.EqualTo(maps[2]),
                        $"Every lesser must climb both floors: {SEntMan.GetComponent<TransformComponent>(lesser).Coordinates}");
                SEntMan.System<SharedTransformSystem>().SetCoordinates(anchor, new EntityCoordinates(maps[0], new Vector2(0.5f, -3.5f)));
            });
            await Pair.RunTicksSync(2400);
            await Server.WaitAssertion(() =>
            {
                foreach (var lesser in lessers)
                {
                    var xform = SEntMan.GetComponent<TransformComponent>(lesser);
                    Assert.That(xform.MapUid, Is.EqualTo(maps[0]), "Every lesser must descend to follow the anchor.");
                    Assert.That(Vector2.Distance(xform.Coordinates.Position, new Vector2(0.5f, -3.5f)), Is.LessThan(3));
                }
            });
        }
        finally
        {
            foreach (var map in maps)
                await Pair.DeleteEntityTreeLeafFirst(map);
            if (network.IsValid())
                await Pair.DeleteEntityTreeLeafFirst(network);
        }
    }
}
