#pragma warning disable RA0002 // Reduce blast strength to isolate proximity detection from hull destruction.
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Vehicle.VBIED;
using Content.Shared._RMC14.Vehicle;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.CMU14.Vehicle.VBIED;
using Content.Shared.Explosion.Components;
using Content.Shared.Interaction;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.CMU14.Vehicle;

[TestFixture]
public sealed class CMUVBIEDTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task ProximityDetonatesForHostileXenoInsideNearbyVehicle()
    {
        var map = await Pair.CreateTestMap();
        EntityUid rigged = default;
        EntityUid carrier = default;
        EntityUid xeno = default;
        ExplosiveComponent explosive = default!;
        await Server.WaitAssertion(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            for (var x = -2; x <= 8; x++)
            for (var y = -2; y <= 3; y++)
                maps.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);

            rigged = SEntMan.SpawnEntity("VehicleVan", map.GridCoords);
            carrier = SEntMan.SpawnEntity("VehicleVan", map.GridCoords.Offset(new Vector2(5, 0)));
            var entry = SEntMan.GetComponent<VehicleEnterComponent>(carrier).EntryPoints[0];
            xeno = SEntMan.SpawnEntity("CMXenoRunner",
                SEntMan.GetComponent<TransformComponent>(carrier).Coordinates.Offset(entry.Offset));
            SEntMan.EventBus.RaiseLocalEvent(carrier, new ActivateInWorldEvent(xeno, carrier, true));
        });

        await Pair.RunSeconds(1.2f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.System<VehicleSystem>().TryGetOccupants(carrier,
                out var passengers, out var xenos), Is.True);
            Assert.That(xenos, Does.Contain(xeno), "The xeno must enter through the real vehicle boarding path.");
            Assert.That(passengers, Does.Not.Contain(xeno));
            var planter = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            SEntMan.System<GunIFFSystem>().SetUserFaction(planter, "FactionMarine");
            var ied = SEntMan.SpawnEntity("AU14IED", map.GridCoords);
            explosive = SEntMan.GetComponent<ExplosiveComponent>(ied);
            explosive.TotalIntensity = 15;
            explosive.IntensitySlope = 5;
            explosive.MaxIntensity = 4;
            Assert.That(SEntMan.System<CMUVBIEDTriggerSystem>().TryRig(
                rigged, ied, planter, CMUVBIEDTrigger.Proximity), Is.True);
        });

        await Pair.RunSeconds(0.6f);
        await Server.WaitAssertion(() => Assert.That(explosive.Exploded, Is.True,
            "A hostile xeno on a nearby vehicle's separate interior map must trigger proximity detonation."));
    }
}
