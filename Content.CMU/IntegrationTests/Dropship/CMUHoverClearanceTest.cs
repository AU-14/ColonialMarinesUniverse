using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Dropship.TacticalLand;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared._RMC14.Rules;
using Content.Shared.CMU14.Dropship.TacticalLand;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.CMU.Dropship;

// Seed the tactical session without involving pilot UI input in the footprint check.
#pragma warning disable RA0002
public sealed class CMUHoverClearanceTest : GameTest
{
    [TestCase(false, 1)]
    [TestCase(true, 2)]
    public async Task HoverAcceptsOpenAirWithAndWithoutNearbyRoofChunks(bool hasRoofChunk, int depth)
    {
        var ground = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            SEntMan.EnsureComponent<RMCPlanetComponent>(ground.MapUid);
            var sky = maps.CreateMap(runMapInit: true);
            if (hasRoofChunk)
            {
                var skyGrid = SEntMan.EnsureComponent<MapGridComponent>(sky);
                maps.SetTile(sky, skyGrid, new Vector2i(3, 0), ground.Tile.Tile);
            }
            var zLevels = Server.System<CMUZLevelsSystem>();
            var network = zLevels.CreateZNetwork();
            var levels = new Dictionary<EntityUid, int> { [ground.MapUid] = 0, [sky] = depth };
            EntityUid? intermediate = depth > 1 ? maps.CreateMap(runMapInit: true) : null;
            if (intermediate is { } middle)
                levels[middle] = 1;
            Assert.That(zLevels.TryAddMapsIntoZNetwork(network, levels), Is.True);
            var console = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            SEntMan.AddComponent<DropshipTacticalLandSessionComponent>(console).InitialMap = ground.MapUid;
            var eye = SEntMan.SpawnEntity(null, new EntityCoordinates(sky, new Vector2(0.5f)));
            var view = SEntMan.AddComponent<DropshipPilotEyeComponent>(eye);
            view.Console = console;
            view.Footprint = Vector2i.Zero;
            typeof(DropshipTacticalLandSystem).GetMethod("UpdateFootprint", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(Server.System<DropshipTacticalLandSystem>(), new object[]
                {
                    new Entity<DropshipPilotEyeComponent>(eye, view), SComp<TransformComponent>(eye),
                });
            Assert.That(view.ClearForLanding, Is.True,
                "Empty air over the colony must not require a roof tile or a roof-level area marker.");
            SEntMan.DeleteEntity(console);
            SEntMan.DeleteEntity(sky);
            if (intermediate is { } intermediateMap)
                SEntMan.DeleteEntity(intermediateMap);
            SEntMan.DeleteEntity(network);
        });
    }
}
