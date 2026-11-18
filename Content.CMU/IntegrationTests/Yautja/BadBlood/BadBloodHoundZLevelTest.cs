#pragma warning disable RA0002 // the scene writes the maps' gravity directly
using Content.IntegrationTests.Fixtures;
using Content.Server.Atmos.EntitySystems;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared.Atmos;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.Gravity;
using Content.Shared.Mind;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// FLOW-610: a driven hound walks up and down a stair between two levels like a marine does. On the planet maps a
// stair's top end usually butts a wall on the lower level; the climb only carries a body up once it is within ~0.4
// tiles of that end, which a 0.45 body pressed against the wall never reaches. Real movement and solid fixtures:
// moving the transform directly would hide the body stopping against the wall
[TestFixture]
public sealed class BadBloodHoundZLevelTest : GameTest
{
    private const string Wall = "CMWallMetal";

    [TestCase("CMUMobYautjaBadBloodHound")]
    [TestCase("CMUMobYautjaHellhound")]
    [TestCase("CMMobHuman")]
    public async Task DrivenMobClimbsAndDescendsAStairAgainstAWall(string prototype)
    {
        var maps = new List<EntityUid>();
        EntityUid network = default;
        EntityUid mob = default;
        try
        {
            await Server.WaitAssertion(() =>
            {
                var mapSystem = SEntMan.System<SharedMapSystem>();
                var floor = new Tile(Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);
                // the control marine breathes; vacuum damage must not stop it on the way
                var moles = new float[Atmospherics.AdjustedNumberOfGases];
                moles[(int) Gas.Oxygen] = 21.824779f;
                moles[(int) Gas.Nitrogen] = 82.10312f;
                for (var i = 0; i < 2; i++)
                {
                    var map = mapSystem.CreateMap(runMapInit: true);
                    maps.Add(map);
                    var grid = SEntMan.EnsureComponent<MapGridComponent>(map);
                    var gravity = SEntMan.EnsureComponent<GravityComponent>(map);
                    gravity.Enabled = true;
                    gravity.Inherent = true;
                    Server.System<AtmosphereSystem>().SetMapAtmosphere(map, false, new GasMixture(moles, Atmospherics.T20C));
                    for (var x = -6; x <= 6; x++)
                    for (var y = -8; y <= 8; y++)
                        mapSystem.SetTile(map, grid, new Vector2i(x, y), floor);
                }

                var z = SEntMan.System<CMUZLevelsSystem>();
                var net = z.CreateZNetwork();
                network = net;
                Assert.That(z.TryAddMapsIntoZNetwork(net, new() { [maps[0]] = 0, [maps[1]] = 1 }), Is.True);

                // the stair rises north on the lower level, the shaft above it is open, and a wall stands right past
                // its top end. Walls past both ends of the walk stop the mob on each level
                var stair = SEntMan.SpawnEntity("CMUMultiZStairs", new EntityCoordinates(maps[0], new Vector2(0.5f, 0.5f)));
                SEntMan.System<SharedTransformSystem>().SetLocalRotation(stair, Direction.South.ToAngle());
                mapSystem.SetTile(maps[1], SEntMan.GetComponent<MapGridComponent>(maps[1]), Vector2i.Zero, Tile.Empty);
                SEntMan.SpawnEntity(Wall, new EntityCoordinates(maps[0], new Vector2(0.5f, 1.5f)));
                SEntMan.SpawnEntity(Wall, new EntityCoordinates(maps[0], new Vector2(0.5f, -5.5f)));
                SEntMan.SpawnEntity(Wall, new EntityCoordinates(maps[1], new Vector2(0.5f, 5.5f)));
                mob = SEntMan.SpawnEntity(prototype, new EntityCoordinates(maps[0], new Vector2(0.5f, -2.5f)));
            });

            await RunSeconds(1);
            // a player drives it: RMC keeps no input mover on an unoccupied marine, and the hound's NPC sleeps
            // a ghost-role takeover makes the mob sentient first: the honourable hellhound has no input mover until then
            await Server.WaitPost(() =>
            {
                Server.System<SharedMindSystem>().MakeSentient(mob);
                Server.PlayerMan.SetAttachedEntity(ServerSession!, mob);
            });
            await RunSeconds(0.5f);

            await Server.WaitAssertion(() => Hold(mob, Direction.North, true));
            await RunSeconds(4);
            await Server.WaitAssertion(() =>
            {
                Hold(mob, Direction.North, false);
                var xform = SComp<TransformComponent>(mob);
                Assert.That(xform.MapUid, Is.EqualTo(maps[1]),
                    $"{prototype} must climb the stair to the upper level; stopped at {xform.Coordinates}, " +
                    $"height {SComp<CMUZPhysicsComponent>(mob).LocalPosition:F3}");
                Hold(mob, Direction.South, true);
            });

            await RunSeconds(4);
            await Server.WaitAssertion(() =>
            {
                Hold(mob, Direction.South, false);
                var xform = SComp<TransformComponent>(mob);
                Assert.That(xform.MapUid, Is.EqualTo(maps[0]),
                    $"{prototype} must walk back down the stair; stopped at {xform.Coordinates}");
                Assert.That(xform.Coordinates.Position.Y, Is.LessThan(0f),
                    $"{prototype} must come off the bottom of the stair; stopped at {xform.Coordinates}");
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

    private void Hold(EntityUid mob, Direction direction, bool held)
    {
        Server.System<SharedMoverController>().SetVelocityDirection(
            (mob, SComp<InputMoverComponent>(mob)), direction, ushort.MaxValue, held);
    }
}
