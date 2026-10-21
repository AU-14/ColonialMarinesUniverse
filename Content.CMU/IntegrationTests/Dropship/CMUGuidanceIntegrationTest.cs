using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Dropship.Fabricator;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Rangefinder;
using Content.Shared._RMC14.Rules;
using Content.Shared.CMU14.Dropship.Cas;
using Content.Shared.CMU14.Guidance;
using Content.Shared.CMU14.Radio;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Dropship;

[TestFixture]
public sealed class CMUGuidanceIntegrationTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    [Test]
    public async Task RtoGpsRequestsUseValidatedGroundSolutionsWithoutRerolling()
    {
        var ground = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            SEntMan.EnsureComponent<RMCPlanetComponent>(ground.MapUid).Offset = new Vector2i(350, -275);
            var rto = SEntMan.SpawnEntity("CMMobHuman", ground.GridCoords);
            var marine = SEntMan.EnsureComponent<MarineComponent>(rto);
            marine.Faction = "govfor";
            var radio = SEntMan.SpawnEntity("ANPRC117GRadio", ground.GridCoords);
            SEntMan.EnsureComponent<WearingANPRCComponent>(rto).Radio = radio;
            var finder = SEntMan.SpawnEntity("RMCRangefinder", ground.GridCoords);
            var rangefinder = SEntMan.GetComponent<RangefinderComponent>(finder);
            var transform = Server.System<SharedTransformSystem>();
#pragma warning disable RA0002 // Arrange a completed ground measurement.
            rangefinder.LastCoords = transform.ToMapCoordinates(ground.GridCoords);
#pragma warning restore RA0002
            var guidance = Server.System<CMUGuidanceSystem>();
            Assert.That(guidance.RecordGroundReport((finder, rangefinder), rto), Is.True);
            var report = guidance.GetReports(rto).Single();
            Assert.That(guidance.TryGetGroundReport(rto, report.Id, out var solution), Is.True);
            var requests = Server.System<CMUCasRequestSystem>();
            Assert.That(requests.TryCreateGpsRequest(rto, int.MaxValue, CMUCasMunition.Any,
                CMUCasEffect.Any, "", out _), Is.False);
            marine.Faction = "opfor";
            Assert.That(requests.TryCreateGpsRequest(rto, report.Id, CMUCasMunition.Any,
                CMUCasEffect.Any, "", out _), Is.False);
            marine.Faction = "govfor";
            SEntMan.EventBus.RaiseLocalEvent(radio, new CMUCasCallSendMsg(null, CMUCasMunition.Any,
                CMUCasEffect.Any, "", gpsReportId: report.Id) { Actor = rto, UiKey = CMUCasCallUi.Key });
            var request = requests.GetOwnRequests(rto).Single();
            Assert.That(transform.GetMapCoordinates(SEntMan.GetEntity(request.Target)), Is.EqualTo(solution));
            Assert.That(requests.TryAbort(rto, request.Id), Is.True);
            Assert.That(requests.TryCreateGpsRequest(rto, report.Id, CMUCasMunition.Any,
                CMUCasEffect.Any, "", out var second), Is.True);
            Assert.That(requests.TryGetRequest(second, out request), Is.True);
            Assert.That(transform.GetMapCoordinates(SEntMan.GetEntity(request.Target)), Is.EqualTo(solution));
            SEntMan.DeleteEntity(finder);
            Assert.That(requests.TryCreateGpsRequest(rto, report.Id, CMUCasMunition.Any,
                CMUCasEffect.Any, "", out _), Is.False);
        });
    }

    [Test]
    public async Task GpsShellCostsTwiceHeAndInheritsItsExplosivePower()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var standard = SEntMan.SpawnEntity("CMUDropshipAmmoHowitzerHE", map.GridCoords);
            var guided = SEntMan.SpawnEntity("CMUDropshipAmmoHowitzerGPS", map.GridCoords);
            var he = SEntMan.GetComponent<DropshipAmmoComponent>(standard);
            var gps = SEntMan.GetComponent<DropshipAmmoComponent>(guided);
            Assert.That(SEntMan.GetComponent<DropshipFabricatorPrintableComponent>(guided).Cost,
                Is.EqualTo(2 * SEntMan.GetComponent<DropshipFabricatorPrintableComponent>(standard).Cost));
            Assert.That(gps.Explosion, Is.Not.Null);
            Assert.That(gps.Explosion!.Type, Is.EqualTo(he.Explosion!.Type));
            Assert.That(gps.Explosion.Total, Is.EqualTo(he.Explosion.Total));
            Assert.That(gps.Explosion.Slope, Is.EqualTo(he.Explosion.Slope));
            Assert.That(gps.Explosion.Max, Is.EqualTo(he.Explosion.Max));
            Assert.That(gps.Weapon, Is.EqualTo(he.Weapon));
            Assert.That(gps.TargetSpread, Is.Zero);
            Assert.That(SEntMan.GetComponent<CMUMunitionGuidanceComponent>(guided).GpsOnly, Is.True);
            var guidance = Server.System<CMUGuidanceSystem>();
            Assert.That(guidance.UsesGps(guided), Is.True);
            Assert.That(guidance.TryGetGps(guided, out _), Is.False);
            var transform = Server.System<SharedTransformSystem>();
            SEntMan.GetComponent<CMUMunitionGuidanceComponent>(guided).Coordinates =
                transform.ToMapCoordinates(map.GridCoords.Offset(new Vector2(12, -15)));
            Assert.That(guidance.TryGetGps(guided, out var target), Is.True);
            Assert.That(transform.ToMapCoordinates(target),
                Is.EqualTo(SEntMan.GetComponent<CMUMunitionGuidanceComponent>(guided).Coordinates!.Value));
        });
    }
}
