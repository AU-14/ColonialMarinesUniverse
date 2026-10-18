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
                new ResPath("/Maps/CMU14/ShuttlesDropships/Mohawk/midway.yml"), out var loaded), Is.True);
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
                new ResPath($"/Maps/CMU14/ShuttlesDropships/Mohawk/{variant}.yml"), out var loaded), Is.True);
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
}
