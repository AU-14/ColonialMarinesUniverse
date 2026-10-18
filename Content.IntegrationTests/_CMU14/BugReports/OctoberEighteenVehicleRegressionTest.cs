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
}
