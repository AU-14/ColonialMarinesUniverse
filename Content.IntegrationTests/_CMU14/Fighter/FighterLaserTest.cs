using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Fighter;
using Content.Server.Light.EntitySystems;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.Rangefinder;
using Content.Shared.Buckle;
using Content.Shared.CMU14.Fighter;
using Content.Shared.Light.Components;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
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
            var position = Server.Transform(_flare).WorldPosition;
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
        foreach (var payload in SEntMan.EntityQuery<AmmoInFlightComponent>().ToArray()) SEntMan.DeleteEntity(payload.Owner);
        if (!SEntMan.Deleted(_flare)) SEntMan.DeleteEntity(_flare);
        SEntMan.DeleteEntity(Server.Transform(_aircraft).MapUid!.Value);
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
