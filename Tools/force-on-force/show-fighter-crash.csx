using System;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.CMU14.Fighter;
using Content.Shared.CMU14.Fighter;
using Content.Shared.Mobs.Components;
{
    var beginCrash = typeof(FighterSystem).GetMethod("BeginCrash", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("This server build does not contain the new fighter crash sequence.");
    var maps = ressys<SharedMapSystem>();
    var map = maps.CreateMap(out var mapId, runMapInit: true);
    maps.SetAmbientLight(mapId, Color.FromHex("#CED5DA"));
    var grid = maps.CreateGridEntity(mapId);
    var floor = new Tile(res<ITileDefinitionManager>()["RMCFloorVehicleInteriorDarkSterile"].TileId);
    for (var x = -30; x <= 30; x++)
    for (var y = -18; y <= 18; y++)
        maps.SetTile(grid.Owner, grid.Comp, new Vector2i(x, y), floor);
    var session = res<Robust.Server.Player.IPlayerManager>().Sessions.Single();
    var viewer = ent.SpawnEntity("MobObserver", new EntityCoordinates(map, -2, 0));
    ent.EnsureComponent<MobStateComponent>(viewer);
    ressys<MindSystem>().ControlMob(session.UserId, viewer);
    ressys<GameTicker>().PlayerJoinGame(session);
    ressys<SharedEyeSystem>().SetZoom(viewer, new Vector2(1.5f));
    var hull = ent.SpawnEntity("CMUFighterGround", new EntityCoordinates(grid.Owner, -12, 5));
    var aircraftUid = ent.GetComponent<FighterGroundComponent>(hull).Aircraft.Value;
    var aircraft = ent.GetComponent<FighterAircraftComponent>(aircraftUid);
    aircraft.TerrainMap = map;
    aircraft.Battlefield = new Box2(-29, -17, 29, 17);
    aircraft.Position = new Vector2(-12, 5);
    aircraft.Height = 220;
    aircraft.Heading = MathF.PI / 2;
    ent.Dirty(aircraftUid, aircraft);
    var system = ressys<FighterSystem>();
    var combat = ent.GetComponent<FighterAirCombatComponent>(aircraftUid);
    Robust.Shared.Timing.Timer.Spawn(15000, () =>
    {
        try { beginCrash.Invoke(system, new object[] { new Entity<FighterAircraftComponent>(aircraftUid, aircraft), combat }); }
        catch (Exception error) { System.Console.WriteLine("Fighter showcase failed: " + (error.InnerException ?? error)); }
    });
    write("Fighter crash starts in 15 seconds. Close the scripting window and console. Watch the approach, impact, and charred cockpit; you can move around the wreck afterward.");
}
