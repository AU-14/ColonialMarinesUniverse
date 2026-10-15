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
using Content.Shared.CMU14.Guidance;
using Content.Shared.Hands.Components;
using Content.Shared.Verbs;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

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
    public async Task DropshipGpsReleaseUsesProgrammedPointWithoutAMarkOrOffsets()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            Server.ResolveDependency<IConfigurationManager>().SetCVar(RMCCVars.RMCDropshipCASDebug, true);
            SEntMan.EnsureComponent<DropshipComponent>(map.Grid);
            SEntMan.EnsureComponent<RMCPlanetComponent>(map.Grid);
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
            Server.System<CMUGuidanceSystem>().Open(host, user);
            var mapUid = Server.Transform(point).MapUid!.Value;
            SEntMan.EventBus.RaiseLocalEvent(host, new CMUProgramGpsMessage(SEntMan.GetNetEntity(ammo),
                CMUGuidanceMode.Gps, SEntMan.GetNetEntity(mapUid), 12, -15)
                { Actor = user, UiKey = CMUGuidanceUi.Key });
            Assert.That(terminal.Target, Is.Null);
            Assert.That(weapons.TryFireWeapon(weapon, map.GridCoords.Offset(new Vector2(99)),
                DropshipWeaponStrikeType.Direct, terminalComp: terminal), Is.True);
            var flight = SEntMan.QueryEntities<AmmoInFlightComponent>().Single();
            Assert.That(Server.System<SharedTransformSystem>().ToMapCoordinates(flight.Comp.Target).Position,
                Is.EqualTo(new Vector2(12, -15)));
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
