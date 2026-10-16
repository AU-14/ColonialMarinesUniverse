using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Fighter;
using Content.Server.Light.EntitySystems;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.Rangefinder;
using Content.Shared._RMC14.Rules;
using Content.Shared.Buckle;
using Content.Shared.CMU14.Fighter;
using Content.Shared.CMU14.Guidance;
using Content.Shared.Light.Components;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.IntegrationTests._CMU14.Fighter;

[TestFixture]
public sealed class FighterLaserTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = """
- type: entity
  parent: RMCDropshipAttachmentAmmoRocketKeeper
  id: FighterTestPreciseLaserMissile
  components:
  - type: DropshipAmmo
    targetSpread: 0
""";

    private Entity<FighterAircraftComponent> _aircraft;
    private FighterSeatComponent _seat = null!;
    private EntityUid _user;
    private EntityUid _other;
    private EntityUid _flare;
    private EntityUid _missile;
    private EntityUid _secondMissile;
    private FighterSystem System => SEntMan.System<FighterSystem>();
    private TimeSpan Now => Server.ResolveDependency<IGameTiming>().CurTime;

    private async Task CreateWorld(bool pilot)
    {
        PreFinalizeHook += () => TestContext.Out.WriteLine(TestContext.CurrentContext.Result.Message);
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var area = SEntMan.EnsureComponent<AreaGridComponent>(map.Grid.Owner);
            SEntMan.System<AreaSystem>().ReplaceArea(area, Vector2i.Zero, "RMCAreaDesertDamInterior");
            _flare = SEntMan.SpawnEntity("RMCFlareCAS", new EntityCoordinates(map.Grid.Owner, new Vector2(.5f)));
            SEntMan.System<ExpendableLightSystem>().TryActivate((_flare, SEntMan.GetComponent<ExpendableLightComponent>(_flare)));
            SEntMan.System<SharedDropshipWeaponSystem>().MakeDropshipTarget(_flare, "TEST", "govfor");
            var position = SEntMan.System<SharedTransformSystem>().GetWorldPosition(_flare);
            var terrain = Server.Transform(_flare).MapUid!.Value;
            _aircraft = System.CreateAircraft(terrain, position);
            _aircraft.Comp.Battlefield = new Box2(position - new Vector2(120), position + new Vector2(120));
            _aircraft.Comp.AirspaceRadius = 500;
            _aircraft.Comp.Position = FighterFlight.HoldingPoint(_aircraft.Comp);
            _user = SEntMan.SpawnEntity("MobHuman", new EntityCoordinates(_aircraft, Vector2.Zero));
            _other = SEntMan.SpawnEntity("MobHuman", new EntityCoordinates(_aircraft, Vector2.Zero));
            SEntMan.EnsureComponent<MarineComponent>(_user).Faction = "govfor";
            SEntMan.EnsureComponent<MarineComponent>(_other).Faction = "govfor";
            Assert.That(System.Board(_user, _aircraft, !pilot), Is.True);
            Assert.That(System.Board(_other, _aircraft, pilot), Is.True);
            _seat = SEntMan.GetComponent<FighterSeatComponent>((pilot ? _aircraft.Comp.FrontSeat : _aircraft.Comp.RearSeat)!.Value);
            Assert.That(System.TryLockTarget(_user, SEntMan.GetNetEntity(_flare)), Is.True);
            var weapons = SEntMan.GetComponent<FighterWeaponsComponent>(_aircraft);
            EntityUid Mount(int slot)
            {
                var ammo = SEntMan.SpawnEntity("FighterTestPreciseLaserMissile", new EntityCoordinates(_aircraft, Vector2.Zero));
                var point = weapons.Hardpoints[slot];
                Assert.That(SEntMan.System<FighterHardpointSystem>().TryMount((point, SEntMan.GetComponent<FighterHardpointComponent>(point)), ammo), Is.True);
                return ammo;
            }
            _missile = Mount(0);
            _secondMissile = Mount(1);
        });
    }

    private async Task Advance(double seconds)
    {
        var ticks = 0;
        await Server.WaitPost(() => ticks = (int) Math.Ceiling(seconds * Server.ResolveDependency<IGameTiming>().TickRate));
        await Pair.RunTicksSync(ticks);
    }

    private void CleanWorld()
    {
        foreach (var payload in SEntMan.QueryEntities<AmmoInFlightComponent>().ToArray()) SEntMan.DeleteEntity(payload.Owner);
        if (!SEntMan.Deleted(_flare)) SEntMan.DeleteEntity(_flare);
        SEntMan.DeleteEntity(Server.Transform(_aircraft).MapUid!.Value);
    }

    [Test]
    public async Task CodesFilterDetectionSelectionAndStaleLocks()
    {
        await CreateWorld(false);
        EntityUid marker = default;
        EntityUid designator = default;
        await Server.WaitAssertion(() =>
        {
            var guidance = Server.System<CMUGuidanceSystem>();
            Assert.That(guidance.GetCode(_aircraft), Is.EqualTo("1111-1"));
            designator = SEntMan.SpawnEntity(null, Server.Transform(_flare).Coordinates);
            marker = SEntMan.SpawnEntity("RMCLaserDesignatorTarget", Server.Transform(_flare).Coordinates);
#pragma warning disable RA0002
            SEntMan.GetComponent<LaserDesignatorTargetComponent>(marker).LaserDesignator = designator;
            var active = SEntMan.AddComponent<ActiveLaserDesignatorComponent>(designator);
            active.Target = marker;
            active.Origin = Server.Transform(designator).Coordinates;
#pragma warning restore RA0002
            Server.System<SharedDropshipWeaponSystem>().MakeDropshipTarget(marker, "CODED", "govfor");
            Assert.That(System.TryLockTarget(_user, SEntMan.GetNetEntity(marker)), Is.True);
            SEntMan.EnsureComponent<CMULaserCodeComponent>(designator).Code = "2222-2";
            Assert.That(guidance.CanDetect(_aircraft, marker), Is.False);
            Assert.That(System.TryLockTarget(_user, SEntMan.GetNetEntity(marker)), Is.False);
            Assert.That(System.TryFire(_user, out _), Is.False);
            SEntMan.GetComponent<FighterWeaponsComponent>(_aircraft).NextRefresh = TimeSpan.Zero;
        });
        await Advance(.25);
        await Server.WaitAssertion(() =>
        {
            Assert.That(_seat.Target, Is.Null);
            Assert.That(SEntMan.GetComponent<FighterWeaponsComponent>(_aircraft).Targets.Any(t =>
                t.Id == SEntMan.GetNetEntity(marker)), Is.False);
            var otherSeat = (_aircraft.Comp.FrontSeat == _seat.Owner
                ? _aircraft.Comp.RearSeat : _aircraft.Comp.FrontSeat)!.Value;
            Server.System<CMUGuidanceSystem>().Open(otherSeat, _other);
            SEntMan.EventBus.RaiseLocalEvent(otherSeat, new CMUSetLaserCodeMessage("2222-2", false)
                { Actor = _other, UiKey = CMUGuidanceUi.Key });
            Assert.That(Server.System<CMUGuidanceSystem>().GetCode(_aircraft), Is.EqualTo("2222-2"));
            Assert.That(System.TryLockTarget(_other, SEntMan.GetNetEntity(marker)), Is.True,
                "both seats share the aircraft receiver");
            CleanWorld();
        });
    }

    [Test]
    public async Task GpsProgrammingUsesGroundReportsPerLoadedRoundWithoutAContinuingDesignation()
    {
        await CreateWorld(false);
        MapCoordinates programmed = default;
        await Server.WaitAssertion(() =>
        {
            var guidance = Server.System<CMUGuidanceSystem>();
            var host = _seat.Owner;
            guidance.Open(host, _user);
            var map = _aircraft.Comp.TerrainMap;
            SEntMan.EnsureComponent<RMCPlanetComponent>(map).Offset = new Vector2i(350, -275);
            var coordinates = new EntityCoordinates(map, Vector2.Zero);
            var observer = SEntMan.SpawnEntity("CMMobHuman", coordinates);
            SEntMan.EnsureComponent<MarineComponent>(observer).Faction = "govfor";
            var finder = SEntMan.SpawnEntity("RMCRangefinder", coordinates);
            var rangefinder = SEntMan.GetComponent<RangefinderComponent>(finder);
#pragma warning disable RA0002 // Arrange a completed measurement from an actual ground observer.
            rangefinder.LastCoords = new MapCoordinates(Vector2.Zero, Server.System<SharedTransformSystem>().GetMapId(map));
#pragma warning restore RA0002
            Assert.That(guidance.RecordGroundReport((finder, rangefinder), observer), Is.True);
            var report = guidance.GetReports(host).Single();
            var weapons = SEntMan.GetComponent<FighterWeaponsComponent>(_aircraft);
            weapons.Faction = "opfor";
            SEntMan.EventBus.RaiseLocalEvent(host, new CMUProgramGpsMessage(SEntMan.GetNetEntity(_missile),
                CMUGuidanceMode.Gps, report.Id) { Actor = _user, UiKey = CMUGuidanceUi.Key });
            Assert.That(guidance.TryGetGps(_missile, out _), Is.False);
            weapons.Faction = "govfor";
            _aircraft.Comp.TerrainMap = Server.Transform(host).MapUid!.Value;
            SEntMan.EventBus.RaiseLocalEvent(host, new CMUProgramGpsMessage(SEntMan.GetNetEntity(_missile),
                CMUGuidanceMode.Gps, report.Id) { Actor = _user, UiKey = CMUGuidanceUi.Key });
            Assert.That(guidance.TryGetGps(_missile, out _), Is.False, "Reports from another planet must be rejected.");
            _aircraft.Comp.TerrainMap = map;
            SEntMan.EventBus.RaiseLocalEvent(host, new CMUProgramGpsMessage(SEntMan.GetNetEntity(_missile),
                CMUGuidanceMode.Gps, report.Id)
                { Actor = _user, UiKey = CMUGuidanceUi.Key });
            Assert.That(guidance.TryGetGps(_missile, out var target), Is.True);
            programmed = Server.System<SharedTransformSystem>().ToMapCoordinates(target);
            Assert.That(MathF.Abs(programmed.X), Is.LessThanOrEqualTo(CMUGuidanceSystem.GpsDeviation));
            Assert.That(MathF.Abs(programmed.Y), Is.LessThanOrEqualTo(CMUGuidanceSystem.GpsDeviation));
            Assert.That(Server.System<SharedTransformSystem>().GetMap(target), Is.EqualTo(map));
            Assert.That(guidance.UsesGps(_secondMissile), Is.False);
            SEntMan.EventBus.RaiseLocalEvent(host, new CMUProgramGpsMessage(SEntMan.GetNetEntity(_secondMissile),
                CMUGuidanceMode.Gps, null)
                { Actor = _user, UiKey = CMUGuidanceUi.Key });
            Assert.That(guidance.UsesGps(_secondMissile), Is.False, "invalid GPS programming must not change the mode");
            SEntMan.EventBus.RaiseLocalEvent(host, new CMUProgramGpsMessage(SEntMan.GetNetEntity(_missile),
                CMUGuidanceMode.Gps, report.Id)
                { Actor = _other, UiKey = CMUGuidanceUi.Key });
            Assert.That(guidance.TryGetGps(_missile, out target), Is.True);
            Assert.That(Server.System<SharedTransformSystem>().ToMapCoordinates(target), Is.EqualTo(programmed),
                "the other seat cannot program through this seat's UI host");
        });
        await Advance(.25);
        await Server.WaitAssertion(() =>
        {
            Assert.That(_seat.Target, Is.EqualTo(SEntMan.GetNetEntity(_missile)),
                "programming the selected round must update the cockpit without selecting a flare");
            Assert.That(System.TrySelectWeapon(_user, 0), Is.True);
            _aircraft.Comp.GroundState = FighterGroundState.Airborne;
            _aircraft.Comp.Flying = true;
            _aircraft.Comp.Phase = FighterPhase.Pass;
            _aircraft.Comp.Position = Vector2.Zero;
            Assert.That(System.TrySelectWeapon(_user, 1), Is.True);
            Assert.That(System.TryLockTarget(_user, SEntMan.GetNetEntity(_missile)), Is.False,
                "a GPS solution belongs only to its programmed round");
            Assert.That(System.TryFire(_user, out _), Is.False,
                "switching to an unprogrammed round must not retain the previous GPS solution");
            Assert.That(System.TrySelectWeapon(_user, 0), Is.True);
            var grid = Server.Transform(_flare).GridUid!.Value;
            SEntMan.DeleteEntity(_flare);
            var maps = Server.System<SharedMapSystem>();
            maps.SetTile((grid, SEntMan.GetComponent<MapGridComponent>(grid)), programmed.Position.Floored(), new Tile(
                Server.ResolveDependency<ITileDefinitionManager>()["FloorSteel"].TileId));
            Server.System<AreaSystem>().ReplaceArea(SEntMan.GetComponent<AreaGridComponent>(grid),
                programmed.Position.Floored(), "RMCAreaDesertDamInterior");
            Assert.That(System.TryFire(_user, out var status), Is.True, status.ToString());
            var flight = SEntMan.QueryEntities<AmmoInFlightComponent>().Single();
            Assert.That(Server.System<SharedTransformSystem>().ToMapCoordinates(flight.Comp.Target),
                Is.EqualTo(programmed));
            CleanWorld();
        });
    }

    [Test]
    public async Task FriendlyGroundDesignatorsAreTrackedAndRemovedWhenDesignationStops()
    {
        await CreateWorld(false);
        EntityUid marker = default;
        NetEntity markerNet = default;
        EntityUid designator = default;
        await Server.WaitAssertion(() =>
        {
            designator = SEntMan.SpawnEntity(null, Server.Transform(_flare).Coordinates);
            marker = SEntMan.SpawnEntity("RMCLaserDesignatorTarget", Server.Transform(_flare).Coordinates);
            markerNet = SEntMan.GetNetEntity(marker);
#pragma warning disable RA0002 // Arrange the established rangefinder/designation relationship.
            SEntMan.GetComponent<LaserDesignatorTargetComponent>(marker).LaserDesignator = designator;
            var active = SEntMan.AddComponent<ActiveLaserDesignatorComponent>(designator);
            active.Target = marker;
            active.Origin = Server.Transform(designator).Coordinates;
#pragma warning restore RA0002
            var targets = Server.System<SharedDropshipWeaponSystem>();
            targets.MakeDropshipTarget(marker, "GROUND LASER", "opfor");
            Assert.That(System.TryLockTarget(_user, SEntMan.GetNetEntity(marker)), Is.False);
            targets.MakeDropshipTarget(marker, "GROUND LASER", "GOVFOR");
            Assert.That(System.TryLockTarget(_user, SEntMan.GetNetEntity(marker)), Is.True);
            SEntMan.GetComponent<FighterWeaponsComponent>(_aircraft).NextRefresh = TimeSpan.Zero;
        });
        await Advance(.25);
        await Server.WaitAssertion(() =>
        {
            var targets = SEntMan.GetComponent<FighterWeaponsComponent>(_aircraft).Targets;
            Assert.That(targets.Any(t => t.Id == markerNet), Is.True);
            SEntMan.RemoveComponent<ActiveLaserDesignatorComponent>(designator);
            Assert.That(System.TryLockTarget(_user, markerNet), Is.False);
            SEntMan.GetComponent<FighterWeaponsComponent>(_aircraft).NextRefresh = TimeSpan.Zero;
        });
        await Advance(.25);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<FighterWeaponsComponent>(_aircraft).Targets.Any(t => t.Id == markerNet), Is.False);
            Assert.That(_seat.Target, Is.Null);
            CleanWorld();
        });
    }
}
