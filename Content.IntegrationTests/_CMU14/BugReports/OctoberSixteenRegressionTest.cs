using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Interaction;
using Content.Shared.Storage.EntitySystems;
using Content.Shared._RMC14.Xenonids.Weeds;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.BugReports;

[TestFixture]
public sealed class OctoberSixteenRegressionTest : GameTest
{
    // the queen's eye could plant a node past a closed resin door but never expand weeds out
    // of the door tile, because the door's own fixture read as a barricade in every direction
    [Test]
    public async Task WeedsExpandOutOfAClosedResinDoorTile()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            var floor = new Tile(Server.ResolveDependency<ITileDefinitionManager>()["CMFloorSteel"].TileId);
            for (var x = -2; x <= 2; x++)
            for (var y = -2; y <= 2; y++)
                maps.SetTile(map.Grid, new Vector2i(x, y), floor);

            var doorTile = maps.GridTileToLocal(map.Grid, map.Grid, Vector2i.Zero);
            var weeds = SEntMan.SpawnEntity("XenoWeeds", doorTile);
            var door = SEntMan.SpawnEntity("DoorXenoResin", doorTile);
            SEntMan.System<SharedDoorSystem>().SetState(door, DoorState.Closed);

            var system = SEntMan.System<SharedXenoWeedsSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(system.CanSpreadWeedsPopup(map.Grid, new Vector2(1.5f, .5f), null, weeds), Is.True);
                Assert.That(system.CanSpreadWeedsPopup(map.Grid, new Vector2(.5f, -.5f), null, weeds), Is.True);
            });

            SEntMan.DeleteEntity(door);
            SEntMan.DeleteEntity(weeds);
        });
    }

    [Test]
    public async Task DefibrillatorsDoNotFitInTheMedicalPouch()
    {
        await Server.WaitAssertion(() =>
        {
            var pouch = SEntMan.SpawnEntity("RMCPouchMedical", MapCoordinates.Nullspace);
            var defib = SEntMan.SpawnEntity("AU14CMDefibrillator", MapCoordinates.Nullspace);
            var storage = SEntMan.System<SharedStorageSystem>();

            Assert.That(storage.CanInsert(pouch, defib, out _, ignoreLocation: true), Is.False);

            SEntMan.DeleteEntity(defib);
            SEntMan.DeleteEntity(pouch);
        });
    }

    // standing on a coat rack's tile blocked clicks on anything past it, like the light fixture
    [Test]
    public async Task CoatRackDoesNotBlockInteractionsPastIt()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var rack = SEntMan.SpawnEntity("RMCCoatRack", map.GridCoords);
            var origin = new MapCoordinates(map.MapCoords.Position - new Vector2(.3f, 0), map.MapId);
            var past = new MapCoordinates(map.MapCoords.Position + new Vector2(1f, 0), map.MapId);

            Assert.That(SEntMan.System<SharedInteractionSystem>().InRangeUnobstructed(origin, past), Is.True);

            SEntMan.DeleteEntity(rack);
        });
    }
}
