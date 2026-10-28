using System.Linq;
using System.Numerics;
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Dropship.TacticalLand;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Dropship.AttachmentPoint;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.CMU14.Dropship.TacticalLand;
using Content.Shared.CMU14.Radio;
using Content.Shared.Interaction;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.CMU14.BugReports;

[TestFixture]
public sealed class OctoberEighteenVehicleRegressionTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    // the chin gun inherited the port fore location, so it shared a number with the port fore
    // weapon and the console's 4-weapon cap dropped the fifth mount entirely
    [Test]
    public async Task MidwayWeaponMountsAllHaveTheirOwnLocation()
    {
        EntityUid ship = default;
        await Server.WaitAssertion(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            maps.CreateMap(out var mapId);
            Assert.That(SEntMan.System<MapLoaderSystem>().TryLoadGrid(mapId,
                new ResPath("/Maps/CMU14/Dropships/Mohawk/midway.yml"), out var loaded), Is.True);
            ship = loaded!.Value.Owner;
        });
        await Pair.RunSeconds(1);
        await Server.WaitAssertion(() =>
        {
            var points = SEntMan.GetComponent<DropshipComponent>(ship).AttachmentPoints
                .Where(SEntMan.HasComponent<DropshipWeaponPointComponent>)
                .ToList();
            var locations = points.Select(p => SEntMan.GetComponent<DropshipWeaponPointComponent>(p).Location).ToList();

            Assert.That(points, Has.Count.EqualTo(5));
            Assert.That(locations, Is.Unique);
            var chin = points.Single(p => SEntMan.GetComponent<MetaDataComponent>(p).EntityPrototype?.ID == "CMUMohawkM90Point");
            Assert.That(SEntMan.GetComponent<DropshipWeaponPointComponent>(chin).Location,
                Is.EqualTo(DropshipWeaponPointLocation.Chin));

            SEntMan.DeleteEntity(ship);
        });
    }

    // the loader sat inside the unanchored blocker column, so every interaction ray hit the blocker first
    [Test]
    public async Task ArmedSppVanAmmoLoaderCanBeReached()
    {
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.System<MapLoaderSystem>().TryLoadMap(
                new ResPath("/Maps/_RMC14/Vehicles/spp_van_armed_interior.yml"), out var loadedMap, out var grids,
                DeserializationOptions.Default with { InitializeMaps = true }), Is.True);
            var grid = grids!.Single().Owner;
            var transforms = SEntMan.System<SharedTransformSystem>();

            var loader = SEntMan.AllEntityQueryEnumerator<VehicleAmmoLoaderComponent, TransformComponent>();
            Assert.That(loader.MoveNext(out var loaderUid, out _, out _), Is.True);

            // standing in the aisle behind the seats
            var standing = transforms.ToMapCoordinates(new EntityCoordinates(grid, new Vector2(2.9f, 0.03f)));
            Assert.That(SEntMan.System<SharedInteractionSystem>().InRangeUnobstructed(standing, loaderUid), Is.True);

            SEntMan.DeleteEntity(loadedMap!.Value.Owner);
        });
    }

    // every other dropship carries a vehicle radio set, the mohawk decks didn't, so the cockpit was off the net
    [TestCase("midway")]
    [TestCase("midway_navy")]
    [TestCase("omaha")]
    [TestCase("omaha_navy")]
    public async Task MohawkCarriesARadioRelay(string variant)
    {
        EntityUid ship = default;
        await Server.WaitAssertion(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            maps.CreateMap(out var mapId);
            Assert.That(SEntMan.System<MapLoaderSystem>().TryLoadGrid(mapId,
                new ResPath($"/Maps/CMU14/Dropships/Mohawk/{variant}.yml"), out var loaded), Is.True);
            ship = loaded!.Value.Owner;
        });
        await Pair.RunTicksSync(5);
        await Server.WaitAssertion(() =>
        {
            var found = false;
            var anchors = SEntMan.AllEntityQueryEnumerator<ANPRCRelayAnchorComponent, TransformComponent>();
            while (anchors.MoveNext(out _, out _, out var xform))
            {
                if (xform.GridUid == ship)
                    found = true;
            }

            Assert.That(found, Is.True);
            SEntMan.DeleteEntity(ship);
        });
    }

    // a turned hull lands a tile further over than its turned index says, check it matches turning the tile center
    [Test]
    public void RotatedFootprintTilesMatchWhereTheHullLands()
    {
        var rotate = typeof(DropshipTacticalLandSystem).GetMethod("RotateGridTileOffset",
            BindingFlags.Static | BindingFlags.NonPublic)!;

        for (byte turns = 0; turns < 4; turns++)
        {
            foreach (var tile in new[] { new Vector2i(0, 0), new Vector2i(3, -2), new Vector2i(-4, 7) })
            {
                var center = Angle.FromDegrees(-turns * 90).RotateVec(new Vector2(tile.X + 0.5f, tile.Y + 0.5f));
                var expected = new Vector2i((int) MathF.Floor(center.X), (int) MathF.Floor(center.Y));
                Assert.That((Vector2i) rotate.Invoke(null, new object[] { tile, turns })!, Is.EqualTo(expected),
                    $"tile {tile}, {turns} quarter turns");
            }
        }
    }

    // nests aren't hard so the turf check let ships land on them and gib the hosts, and other grids on the
    // landing map (a landed mohawk, another dropship) were never looked at
    [Test]
    public async Task TacticalLandingIsBlockedByNestsAndOtherGrids()
    {
        var map = await Pair.CreateTestMap();
        EntityUid eye = default;
        await Server.WaitAssertion(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            var floor = new Tile(Server.ResolveDependency<ITileDefinitionManager>()["CMFloorSteel"].TileId);
            for (var x = -2; x <= 3; x++)
            for (var y = -2; y <= 2; y++)
                maps.SetTile(map.Grid, new Vector2i(x, y), floor);

            // two tile ship parked on its own map
            maps.CreateMap(out var shipMap);
            var ship = maps.CreateGridEntity(shipMap);
            maps.SetTile(ship, Vector2i.Zero, floor);
            maps.SetTile(ship, new Vector2i(1, 0), floor);
            SEntMan.EnsureComponent<DropshipComponent>(ship);
            var console = SEntMan.SpawnEntity("CMComputerDropshipNavigationERTThirdParty",
                new EntityCoordinates(ship, 0.5f, 0.5f));

            eye = SEntMan.SpawnEntity("CMUDropshipPilotEye", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
            var pilotEye = SEntMan.EnsureComponent<DropshipPilotEyeComponent>(eye);
            typeof(DropshipPilotEyeComponent).GetField(nameof(DropshipPilotEyeComponent.Console))!
                .SetValue(pilotEye, (EntityUid?) console);
        });

        await Pair.RunSeconds(0.5f);
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<DropshipPilotEyeComponent>(eye).ClearForLanding, Is.True));

        EntityUid nest = default;
        await Server.WaitAssertion(() =>
            nest = SEntMan.SpawnEntity("XenoNest", new EntityCoordinates(map.Grid, 1.5f, 0.5f)));
        await Pair.RunSeconds(0.5f);
        await Server.WaitAssertion(() =>
        {
            var pilotEye = SEntMan.GetComponent<DropshipPilotEyeComponent>(eye);
            Assert.That(pilotEye.ClearForLanding, Is.False);
            Assert.That(pilotEye.BlockedTiles, Does.Contain(new Vector2i(1, 0)));
            SEntMan.DeleteEntity(nest);
        });

        await Pair.RunSeconds(0.5f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<DropshipPilotEyeComponent>(eye).ClearForLanding, Is.True);

            var maps = SEntMan.System<SharedMapSystem>();
            var floor = new Tile(Server.ResolveDependency<ITileDefinitionManager>()["CMFloorSteel"].TileId);
            var parked = maps.CreateGridEntity(map.MapId);
            SEntMan.System<SharedTransformSystem>().SetWorldPosition(parked,
                SEntMan.System<SharedTransformSystem>().GetWorldPosition(map.Grid));
            maps.SetTile(parked, Vector2i.Zero, floor);
        });

        await Pair.RunSeconds(0.5f);
        await Server.WaitAssertion(() =>
        {
            var pilotEye = SEntMan.GetComponent<DropshipPilotEyeComponent>(eye);
            Assert.That(pilotEye.ClearForLanding, Is.False);
            Assert.That(pilotEye.BlockedTiles, Does.Contain(Vector2i.Zero));
        });
    }
}
