using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Dropship.Fabricator;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Dropship.AttachmentPoint;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.Rangefinder;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.Marines;
using Content.Shared.CMU14;
using Content.Shared.CMU14.Guidance;
using Content.Shared.CMU14.Dropship.Cas;
using Content.Shared.CMU14.Radio;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Verbs;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Dropship;

[TestFixture]
public sealed class CMUGuidanceTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    [TestCase("1111-1", true)]
    [TestCase("0000-0", true)]
    [TestCase("9999-9", true)]
    [TestCase("11111", false)]
    [TestCase("1111-A", false)]
    [TestCase("1111-11", false)]
    [TestCase(" 1111-1", false)]
    public void LaserCodeFormat(string code, bool valid) =>
        Assert.That(CMUGuidanceSystem.ValidCode(code), Is.EqualTo(valid));

    [Test]
    public async Task ReprogrammedDesignatorFiltersDropshipTargetsAndCameras()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            SEntMan.EnsureComponent<DropshipComponent>(map.Grid);
            var user = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            var designator = SEntMan.SpawnEntity(null, map.GridCoords);
#pragma warning disable RA0002 // Arrange a designator independently of vendor/faction prototypes.
            SEntMan.AddComponent<RangefinderComponent>(designator).CanDesignate = true;
#pragma warning restore RA0002
            var marker = SEntMan.SpawnEntity("RMCLaserDesignatorTarget", map.GridCoords);
#pragma warning disable RA0002
            SEntMan.GetComponent<LaserDesignatorTargetComponent>(marker).LaserDesignator = designator;
#pragma warning restore RA0002
            var terminal = SEntMan.SpawnEntity("CMUMohawkMidwayGunneryea0f8d621b", map.GridCoords);
            var weapons = Server.System<SharedDropshipWeaponSystem>();
            var guidance = Server.System<CMUGuidanceSystem>();
            weapons.MakeDropshipTarget(marker, "CODE", "govfor");
            var console = SEntMan.GetComponent<DropshipTerminalWeaponsComponent>(terminal);
            Assert.That(guidance.CanDetect(terminal, marker), Is.True);
            weapons.SetTerminalTarget((terminal, console), marker);
            Assert.That(console.Target, Is.EqualTo(marker));
            var verbs = new GetVerbsEvent<Verb>(user, designator, null,
                SEntMan.GetComponent<HandsComponent>(user), true, true, true, new());
            SEntMan.EventBus.RaiseLocalEvent(designator, verbs);
            verbs.Verbs.Single(v => v.Text == "Guidance programming").Act!();
            SEntMan.EventBus.RaiseLocalEvent(designator, new CMUSetLaserCodeMessage("2222-2", true)
                { Actor = user, UiKey = CMUGuidanceUi.Key });
            Assert.That(guidance.GetCode(designator), Is.EqualTo("2222-2"));
            Assert.That(console.Target, Is.Null);
            Assert.That(console.Targets.Any(t => t.Id == SEntMan.GetNetEntity(marker)), Is.False);
            Assert.That(weapons.TryUpdateCameraTarget(terminal, marker), Is.False);
            Assert.That(weapons.TrySetCameraTarget(terminal, marker), Is.False);
            weapons.SetTerminalTarget((terminal, console), marker);
            Assert.That(console.Target, Is.Null);
            SEntMan.EventBus.RaiseLocalEvent(designator, new CMUSetLaserCodeMessage("invalid", true)
                { Actor = user, UiKey = CMUGuidanceUi.Key });
            Assert.That(guidance.GetCode(designator), Is.EqualTo("2222-2"));
            SEntMan.EnsureComponent<CMULaserCodeComponent>(map.Grid).Code = "2222-2";
            weapons.RefreshGuidanceTargets();
            Assert.That(guidance.CanDetect(terminal, marker), Is.True);
            Assert.That(console.Targets.Any(t => t.Id == SEntMan.GetNetEntity(marker)), Is.True);
        });
    }

    [Test]
    public async Task DropshipGpsRequiresGroundReportAndConvertsPlanetCoordinatesWithDeviation()
    {
        var map = await Pair.CreateTestMap();
        var ground = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            Server.ResolveDependency<IConfigurationManager>().SetCVar(RMCCVars.RMCDropshipCASDebug, true);
            SEntMan.EnsureComponent<DropshipComponent>(map.Grid);
            SEntMan.EnsureComponent<ShipFactionComponent>(map.Grid).Faction = "govfor";
            SEntMan.EnsureComponent<RMCPlanetComponent>(ground.Grid).Offset = new Vector2i(350, -275);
            SEntMan.EnsureComponent<RMCPlanetComponent>(ground.MapUid).Offset = new Vector2i(350, -275);
            var point = SEntMan.SpawnEntity(null, map.GridCoords);
            var mount = SEntMan.AddComponent<DropshipUtilityPointComponent>(point);
            var weapon = SEntMan.SpawnEntity("CMUDropshipAttachmentHowitzer", map.GridCoords);
            var ammo = SEntMan.SpawnEntity("CMUDropshipAmmoHowitzerGPS", map.GridCoords);
            var containers = Server.System<SharedContainerSystem>();
            Assert.That(containers.Insert(weapon,
                containers.EnsureContainer<ContainerSlot>(point, mount.UtilitySlotId)), Is.True);
            Assert.That(containers.Insert(ammo,
                containers.EnsureContainer<ContainerSlot>(point, mount.AmmoContainerSlotId)), Is.True);
            var host = SEntMan.SpawnEntity("CMUMohawkMidwayGunneryea0f8d621b", map.GridCoords);
            var user = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            var terminal = SEntMan.GetComponent<DropshipTerminalWeaponsComponent>(host);
            var weapons = Server.System<SharedDropshipWeaponSystem>();
            Assert.That(weapons.TryFireWeapon(weapon, map.GridCoords, DropshipWeaponStrikeType.Direct,
                terminalComp: terminal), Is.False, "unprogrammed GPS cannot consume a round");
            Assert.That(SEntMan.GetComponent<DropshipAmmoComponent>(ammo).Rounds, Is.EqualTo(1));
            var guidance = Server.System<CMUGuidanceSystem>();
            guidance.Open(host, user);
            SEntMan.EventBus.RaiseLocalEvent(host, new CMUProgramGpsMessage(SEntMan.GetNetEntity(ammo),
                CMUGuidanceMode.Gps, int.MaxValue)
                { Actor = user, UiKey = CMUGuidanceUi.Key });
            Assert.That(guidance.TryGetGps(ammo, out _), Is.False, "Pilots cannot invent ground reports.");
            var transform = Server.System<SharedTransformSystem>();
            var requested = transform.ToMapCoordinates(ground.GridCoords.Offset(new Vector2(12, -15)));
            var observer = SEntMan.SpawnEntity("CMMobHuman", ground.GridCoords);
            SEntMan.EnsureComponent<MarineComponent>(observer).Faction = "govfor";
            var rangefinder = SEntMan.SpawnEntity("RMCRangefinder", ground.GridCoords);
            var finder = SEntMan.GetComponent<RangefinderComponent>(rangefinder);
#pragma warning disable RA0002 // Arrange a completed ground measurement at the rangefinder boundary.
            finder.LastCoords = requested;
#pragma warning restore RA0002
            Assert.That(guidance.RecordGroundReport((rangefinder, finder), observer), Is.True);
            var report = guidance.GetReports(host).Single();
            Assert.That((report.X, report.Y), Is.EqualTo((362, -290)),
                "Aircraft must receive the same offset planet coordinates as the rangefinder.");
            SEntMan.GetComponent<ShipFactionComponent>(map.Grid).Faction = "opfor";
            SEntMan.EventBus.RaiseLocalEvent(host, new CMUProgramGpsMessage(SEntMan.GetNetEntity(ammo),
                CMUGuidanceMode.Gps, report.Id) { Actor = user, UiKey = CMUGuidanceUi.Key });
            Assert.That(guidance.TryGetGps(ammo, out _), Is.False, "Enemy report IDs must be rejected, not just hidden.");
            SEntMan.GetComponent<ShipFactionComponent>(map.Grid).Faction = "govfor";
            SEntMan.EventBus.RaiseLocalEvent(host, new CMUProgramGpsMessage(SEntMan.GetNetEntity(ammo),
                CMUGuidanceMode.Gps, report.Id) { Actor = user, UiKey = CMUGuidanceUi.Key });
            Assert.That(guidance.TryGetGps(ammo, out var programmed), Is.True);
            var aim = transform.ToMapCoordinates(programmed);
            Assert.That(aim.MapId, Is.EqualTo(requested.MapId));
            Assert.That(MathF.Abs(aim.X - requested.X), Is.LessThanOrEqualTo(CMUGuidanceSystem.GpsDeviation));
            Assert.That(MathF.Abs(aim.Y - requested.Y), Is.LessThanOrEqualTo(CMUGuidanceSystem.GpsDeviation));
            SEntMan.EventBus.RaiseLocalEvent(host, new CMUProgramGpsMessage(SEntMan.GetNetEntity(ammo),
                CMUGuidanceMode.Gps, report.Id) { Actor = user, UiKey = CMUGuidanceUi.Key });
            Assert.That(guidance.TryGetGps(ammo, out var repeated), Is.True);
            Assert.That(transform.ToMapCoordinates(repeated), Is.EqualTo(aim), "Reprogramming cannot reroll error.");
            Assert.That(guidance.RecordGroundReport((rangefinder, finder), observer), Is.True);
            var replacement = guidance.GetReports(host).Single();
            SEntMan.EventBus.RaiseLocalEvent(host, new CMUProgramGpsMessage(SEntMan.GetNetEntity(ammo),
                CMUGuidanceMode.Gps, replacement.Id) { Actor = user, UiKey = CMUGuidanceUi.Key });
            Assert.That(guidance.TryGetGps(ammo, out repeated), Is.True);
            Assert.That(transform.ToMapCoordinates(repeated), Is.EqualTo(aim), "Remeasuring cannot reroll error.");
            SEntMan.DeleteEntity(rangefinder);
            Assert.That(guidance.GetReports(host), Is.Empty);
            Assert.That(guidance.TryGetGps(ammo, out repeated), Is.True, "Programmed rounds retain their solution.");
            Assert.That(terminal.Target, Is.Null);
            Assert.That(weapons.TryFireWeapon(weapon, map.GridCoords.Offset(new Vector2(99)),
                DropshipWeaponStrikeType.Direct, terminalComp: terminal), Is.True);
            var flight = SEntMan.QueryEntities<AmmoInFlightComponent>().Single();
            Assert.That(transform.ToMapCoordinates(flight.Comp.Target), Is.EqualTo(aim),
                "GPS uses the reported solution, not the console's mark or offsets.");
        });
    }

    [Test]
    public async Task CompletedRangefinderMeasurementPublishesOnlyToItsFaction()
    {
        var ground = await Pair.CreateTestMap();
        var ship = await Pair.CreateTestMap();
        EntityUid host = default;
        EntityUid rangefinder = default;
        EntityUid observer = default;
        CMUGuidanceReport report = default!;
        await Server.WaitAssertion(() =>
        {
            SEntMan.EnsureComponent<RMCPlanetComponent>(ground.Grid).Offset = new Vector2i(350, -275);
            SEntMan.EnsureComponent<DropshipComponent>(ship.Grid);
            SEntMan.EnsureComponent<ShipFactionComponent>(ship.Grid).Faction = "govfor";
            host = SEntMan.SpawnEntity("CMUMohawkMidwayGunneryea0f8d621b", ship.GridCoords);
            observer = SEntMan.SpawnEntity("CMMobHuman", ground.GridCoords);
            SEntMan.EnsureComponent<MarineComponent>(observer).Faction = "govfor";
            rangefinder = SEntMan.SpawnEntity("RMCRangefinder", ground.GridCoords);
            Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(observer, rangefinder), Is.True);
            SEntMan.EventBus.RaiseLocalEvent(rangefinder,
                new AfterInteractEvent(observer, rangefinder, null, ground.GridCoords, true));
            Assert.That(SEntMan.GetComponent<RangefinderComponent>(rangefinder).DoAfter, Is.Not.Null);
            Assert.That(Server.System<CMUGuidanceSystem>().GetReports(host), Is.Empty,
                "Starting a measurement is not enough to authorize a strike.");
        });
        await Pair.RunSeconds(11);
        await Server.WaitAssertion(() =>
        {
            var guidance = Server.System<CMUGuidanceSystem>();
            Assert.That(SEntMan.GetComponent<RangefinderComponent>(rangefinder).LastCoords, Is.Not.Null,
                "A completed rangefinder measurement must capture target coordinates.");
            Assert.That(Server.System<RMCPlanetSystem>().IsOnPlanet(Server.Transform(observer)), Is.True);
            Assert.That(Server.System<SharedDropshipWeaponSystem>().GetConsoleFaction(host), Is.EqualTo("govfor"));
            report = guidance.GetReports(host).Single();
            var measured = SEntMan.GetComponent<RangefinderComponent>(rangefinder).LastTarget!.Value;
            Assert.That((report.X, report.Y), Is.EqualTo((measured.X, measured.Y)));
            SEntMan.GetComponent<ShipFactionComponent>(ship.Grid).Faction = "opfor";
            Assert.That(guidance.GetReports(host), Is.Empty, "Enemy ground reports must not be exposed.");
            SEntMan.GetComponent<ShipFactionComponent>(ship.Grid).Faction = "govfor";
        });
        await Pair.RunSeconds((float) CMUGuidanceSystem.ReportLifetime.TotalSeconds + 1);
        await Server.WaitAssertion(() =>
        {
            var guidance = Server.System<CMUGuidanceSystem>();
            Assert.That(guidance.GetReports(host), Is.Empty, "Reports must expire.");
            var point = SEntMan.SpawnEntity(null, ship.GridCoords);
            var mount = SEntMan.AddComponent<DropshipUtilityPointComponent>(point);
            var weapon = SEntMan.SpawnEntity("CMUDropshipAttachmentHowitzer", ship.GridCoords);
            var ammo = SEntMan.SpawnEntity("CMUDropshipAmmoHowitzerGPS", ship.GridCoords);
            var containers = Server.System<SharedContainerSystem>();
            Assert.That(containers.Insert(weapon,
                containers.EnsureContainer<ContainerSlot>(point, mount.UtilitySlotId)), Is.True);
            Assert.That(containers.Insert(ammo,
                containers.EnsureContainer<ContainerSlot>(point, mount.AmmoContainerSlotId)), Is.True);
            var user = SEntMan.SpawnEntity("MobHuman", ship.GridCoords);
            guidance.Open(host, user);
            SEntMan.EventBus.RaiseLocalEvent(host, new CMUProgramGpsMessage(SEntMan.GetNetEntity(ammo),
                CMUGuidanceMode.Gps, report.Id) { Actor = user, UiKey = CMUGuidanceUi.Key });
            Assert.That(guidance.TryGetGps(ammo, out _), Is.False, "Expired IDs cannot authorize programming.");
        });
    }

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
                CMUCasEffect.Any, "", out _), Is.False, "Enemy GPS report IDs cannot create requests.");
            marine.Faction = "govfor";
            SEntMan.EventBus.RaiseLocalEvent(radio, new CMUCasCallSendMsg(null, CMUCasMunition.Any,
                CMUCasEffect.Any, "", gpsReportId: report.Id) { Actor = rto, UiKey = CMUCasCallUi.Key });
            var request = requests.GetOwnRequests(rto).Single();
            Assert.That(transform.GetMapCoordinates(SEntMan.GetEntity(request.Target)), Is.EqualTo(solution),
                "The RTO request must use the same deviated solution as aircraft GPS programming.");
            Assert.That(requests.TryAbort(rto, request.Id), Is.True);
            Assert.That(requests.TryCreateGpsRequest(rto, report.Id, CMUCasMunition.Any,
                CMUCasEffect.Any, "", out var second), Is.True);
            Assert.That(requests.TryGetRequest(second, out request), Is.True);
            Assert.That(transform.GetMapCoordinates(SEntMan.GetEntity(request.Target)), Is.EqualTo(solution));
            SEntMan.DeleteEntity(finder);
            Assert.That(requests.TryCreateGpsRequest(rto, report.Id, CMUCasMunition.Any,
                CMUCasEffect.Any, "", out _), Is.False, "Deleted-source reports cannot create new requests.");
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
            var settings = SEntMan.GetComponent<CMUMunitionGuidanceComponent>(guided);
            Assert.That(settings.GpsOnly, Is.True);
            var guidance = Server.System<CMUGuidanceSystem>();
            Assert.That(guidance.UsesGps(guided), Is.True);
            Assert.That(guidance.TryGetGps(guided, out _), Is.False);
            var transform = Server.System<SharedTransformSystem>();
            settings.Coordinates = transform.ToMapCoordinates(map.GridCoords.Offset(new Vector2(12, -15)));
            Assert.That(guidance.TryGetGps(guided, out var target), Is.True);
            Assert.That(transform.ToMapCoordinates(target), Is.EqualTo(settings.Coordinates.Value));
        });
    }
}
