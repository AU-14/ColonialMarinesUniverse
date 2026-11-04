using Content.IntegrationTests.Fixtures;
using Content.Server._RMC14.TacticalMap;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Sensor;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.Interaction;

namespace Content.IntegrationTests.CMU14.TacticalMap;

[TestFixture]
public sealed class CMUSensorTowerVisionTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

#pragma warning disable RA0002 // Arrange faction feeds and a repaired, inactive sensor array.
    [TestCase("GOVFOR", true)]
    [TestCase("OPFOR", true)]
    [TestCase("GOVFOR", false)]
    [TestCase("OPFOR", false)]
    public async Task ActivatingSensorGrantsOnlyItsFactionVisionAndTurningItOffRevokesIt(string faction, bool live)
    {
        var grid = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var engineer = SEntMan.SpawnEntity("CMMobHuman", grid.GridCoords);
            var enemy = SEntMan.SpawnEntity("CMMobHuman", grid.GridCoords);
            var xeno = SEntMan.SpawnEntity("CMXenoDrone", grid.GridCoords);
            var tower = SEntMan.SpawnEntity("AU14SensorTower", grid.GridCoords);
            var sensor = SEntMan.GetComponent<SensorTowerComponent>(tower);
            sensor.State = SensorTowerState.Off;
            sensor.BreakChance = 0;
            SEntMan.EnsureComponent<UserIFFComponent>(engineer).Factions = [faction];
            Server.System<SkillsSystem>().SetSkill(engineer, "RMCSkillEngineer", 2);

            var viewer = SEntMan.EnsureComponent<TacticalMapUserComponent>(engineer);
            viewer.Marines = false;
            viewer.Govfor = faction == "GOVFOR";
            viewer.Opfor = faction == "OPFOR";
            viewer.LiveUpdate = live;
            var other = SEntMan.EnsureComponent<TacticalMapUserComponent>(enemy);
            other.Marines = false;
            other.Govfor = !viewer.Govfor;
            other.Opfor = !viewer.Opfor;
            other.LiveUpdate = live;
            var hiveViewer = SEntMan.EnsureComponent<TacticalMapUserComponent>(xeno);
            hiveViewer.Xenos = true;
            hiveViewer.LiveUpdate = true;
            var map = new TacticalMapComponent();
            var ownFeed = viewer.Govfor ? map.GovforBlips : map.OpforBlips;
            var enemyFeed = viewer.Govfor ? map.OpforBlips : map.GovforBlips;
            ownFeed[engineer.Id] = new TacticalMapBlip { Indices = new(1, 1) };
            enemyFeed[enemy.Id] = new TacticalMapBlip { Indices = new(2, 1) };
            map.XenoBlips[xeno.Id] = new TacticalMapBlip { Indices = new(3, 1) };
            map.XenoBlips[tower.Id] = new TacticalMapBlip { Indices = new(4, 1) };
            var maps = Server.System<TacticalMapSystem>();

            maps.UpdateUserData((engineer, viewer), map);
            maps.UpdateUserData((xeno, hiveViewer), map);
            Assert.That(hiveViewer.XenoStructureBlips.ContainsKey(tower.Id), Is.True,
                "the sensor structure must be visible even while offline");
            Assert.That(Feed(viewer).ContainsKey(tower.Id), Is.True);
            Assert.That(Feed(viewer).ContainsKey(enemy.Id), Is.False);

            SEntMan.EventBus.RaiseLocalEvent(tower, new InteractHandEvent(engineer, tower));
            Assert.That(sensor.Faction, Is.EqualTo(faction), "activation must claim the array for the operator");
            maps.UpdateUserData((engineer, viewer), map);
            maps.UpdateUserData((enemy, other), map);
            Assert.That(Feed(viewer)[enemy.Id].Image?.RsiState, Is.EqualTo("enemy_blip"));
            Assert.That(Feed(viewer).ContainsKey(xeno.Id), Is.True);
            Assert.That(Feed(other).ContainsKey(engineer.Id), Is.False, "the opposing side must not get this feed");
            Assert.That(ownFeed.ContainsKey(enemy.Id), Is.False, "sensor overlays must not contaminate faction source buckets");

            // Preserve a published sensor snapshot to exercise revocation of stale intel too.
            if (viewer.Govfor) map.LastUpdateGovforBlips = new(Feed(viewer));
            else map.LastUpdateOpforBlips = new(Feed(viewer));
            SEntMan.EventBus.RaiseLocalEvent(tower, new InteractHandEvent(engineer, tower));
            maps.UpdateUserData((engineer, viewer), map);
            Assert.That(Feed(viewer).ContainsKey(enemy.Id), Is.False);
            Assert.That(Feed(viewer).ContainsKey(xeno.Id), Is.False);
            Assert.That(Feed(viewer).ContainsKey(tower.Id), Is.True);

            // A new operator captures the array on reactivation; the old faction stays blind.
            var otherFaction = faction == "GOVFOR" ? "OPFOR" : "GOVFOR";
            SEntMan.EnsureComponent<UserIFFComponent>(enemy).Factions = [otherFaction];
            Server.System<SkillsSystem>().SetSkill(enemy, "RMCSkillEngineer", 2);
            SEntMan.EventBus.RaiseLocalEvent(tower, new InteractHandEvent(enemy, tower));
            maps.UpdateUserData((engineer, viewer), map);
            maps.UpdateUserData((enemy, other), map);
            Assert.That(sensor.Faction, Is.EqualTo(otherFaction));
            Assert.That(Feed(other)[engineer.Id].Image?.RsiState, Is.EqualTo("enemy_blip"));
            Assert.That(Feed(other).ContainsKey(xeno.Id), Is.True);
            Assert.That(Feed(viewer).ContainsKey(enemy.Id), Is.False);
            Assert.That(Feed(viewer).ContainsKey(xeno.Id), Is.False);

            Dictionary<int, TacticalMapBlip> Feed(TacticalMapUserComponent user) =>
                user.Govfor ? user.GovforBlips : user.OpforBlips;
        });
    }
#pragma warning restore RA0002
}
