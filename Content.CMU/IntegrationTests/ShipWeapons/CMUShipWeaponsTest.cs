#pragma warning disable RA0002 // Tests configure targets, cooldowns and permission fixtures directly.
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.ShipWeapons;
using Content.Server.CMU14.ZLevels.Core;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Damage;
using Content.Shared._RMC14.OrbitalCannon;
using Content.Shared._RMC14.Overwatch;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Smoke;
using Content.Shared.Access.Components;
using Content.Shared.CMU14.ShipWeapons;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Maps;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.ShipWeapons;

[TestFixture]
[TestOf(typeof(CMUShipWeaponsSystem))]
public sealed class CMUShipWeaponsTest : GameTest
{
    private void Send(EntityUid terminal, EntityUid actor, BoundUserInterfaceMessage message, bool overwatch = false)
    {
        message.Actor = actor;
        message.UiKey = overwatch ? OverwatchConsoleUI.Key : CMUShipWeaponsUiKey.Key;
        SEntMan.EventBus.RaiseLocalEvent(terminal, (object) message);
    }

    private EntityUid Terminal(EntityCoordinates coordinates)
    {
        var terminal = SEntMan.SpawnEntity("CMUShipWeaponsTerminal", coordinates);
        SEntMan.RemoveComponent<AccessReaderComponent>(terminal);
        return terminal;
    }

    private EntityUid Console(EntityCoordinates coordinates)
    {
        var console = SEntMan.SpawnEntity("RMCOverwatchConsoleGovfor", coordinates);
        SEntMan.RemoveComponent<AccessReaderComponent>(console);
        return console;
    }

    private List<EntityUid> Strikes()
    {
        var result = new List<EntityUid>();
        var query = SEntMan.EntityQueryEnumerator<CMUShipWeaponStrikeComponent>();
        while (query.MoveNext(out var uid, out _))
            result.Add(uid);
        return result;
    }

    private CMUShipWeaponPrototype Profile(ProtoId<CMUShipWeaponPrototype> id) => SProtoMan.Index(id);

    [Test]
    public async Task DuplicateShipsHaveIndependentLockedLoadoutsAndReplacementTerminalsCannotResetThem()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            var other = maps.CreateGridEntity(map.MapId);
            maps.SetTile(other, Vector2i.Zero, map.Tile.Tile);
            SEntMan.System<SharedTransformSystem>().SetCoordinates(other,
                new EntityCoordinates(map.MapUid, new Vector2(100, 0)));
            var actor = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var first = Terminal(map.GridCoords);
            var second = Terminal(new EntityCoordinates(other, Vector2.Zero));
            var system = SEntMan.System<CMUShipWeaponsSystem>();
            Assert.That(system.TryGetShip(first, out var firstShip), Is.True);
            Assert.That(system.TryGetShip(second, out var secondShip), Is.True);
            Assert.That(firstShip, Is.Not.EqualTo(secondShip));

            Send(first, actor, new CMUShipWeaponsArmBuiMsg("CMUGatlingBarrage", "CMUWalkingBarrage"));
            Send(second, actor, new CMUShipWeaponsArmBuiMsg("CMUGasStrike", "CMUSmokeStrike"));
            var a = SComp<CMUShipWeaponsComponent>(firstShip);
            var b = SComp<CMUShipWeaponsComponent>(secondShip);
            Assert.That(a.Weapons.Keys.Select(k => k.Id), Is.EquivalentTo(new[] { "CMUGatlingBarrage", "CMUWalkingBarrage" }));
            Assert.That(b.Weapons.Keys.Select(k => k.Id), Is.EquivalentTo(new[] { "CMUGasStrike", "CMUSmokeStrike" }));

            SEntMan.DeleteEntity(first);
            var replacement = Terminal(map.GridCoords);
            Send(replacement, actor, new CMUShipWeaponsArmBuiMsg("CMUPrecisionStrike", "CMUEMSStrike"));
            Assert.That(a.Weapons.Keys.Select(k => k.Id), Is.EquivalentTo(new[] { "CMUGatlingBarrage", "CMUWalkingBarrage" }));
            Assert.That(a.Weapons.Values, Is.All.EqualTo(3));
            Assert.That(b.Weapons.Values, Is.All.EqualTo(3));
        });
    }

    [Test]
    public async Task DecksShareTheirShipButIdenticalMultiDeckShipsDoNotShareLoadouts()
    {
        var mapsToDelete = new List<EntityUid>();
        try
        {
            await Server.WaitAssertion(() =>
            {
                var maps = SEntMan.System<SharedMapSystem>();
                var zLevels = SEntMan.System<CMUZLevelsSystem>();
                var system = SEntMan.System<CMUShipWeaponsSystem>();
                var a = zLevels.CreateZNetwork();
                var b = zLevels.CreateZNetwork();
                mapsToDelete.Add(a);
                mapsToDelete.Add(b);
                var lower = maps.CreateMap(out var lowerId);
                var upper = maps.CreateMap(out var upperId);
                var other = maps.CreateMap(out var otherId);
                mapsToDelete.AddRange([lower, upper, other]);
                Assert.That(zLevels.TryAddMapsIntoZNetwork(a, new() { [lower] = 0, [upper] = 1 }), Is.True);
                Assert.That(zLevels.TryAddMapsIntoZNetwork(b, new() { [other] = 0 }), Is.True);
                var first = Terminal(SEntMan.System<SharedTransformSystem>().ToCoordinates(new MapCoordinates(Vector2.Zero, lowerId)));
                var same = Terminal(SEntMan.System<SharedTransformSystem>().ToCoordinates(new MapCoordinates(Vector2.Zero, upperId)));
                var different = Terminal(SEntMan.System<SharedTransformSystem>().ToCoordinates(new MapCoordinates(Vector2.Zero, otherId)));
                var actor = SEntMan.SpawnEntity("CMMobHuman", new MapCoordinates(Vector2.Zero, lowerId));
                Assert.That(system.TryGetShip(first, out var ship), Is.True);
                Assert.That(system.TryGetShip(same, out var sameShip), Is.True);
                Assert.That(system.TryGetShip(different, out var differentShip), Is.True);
                Assert.That(ship, Is.EqualTo(a.Owner));
                Assert.That(sameShip, Is.EqualTo(ship));
                Assert.That(differentShip, Is.EqualTo(b.Owner));

                Send(first, actor, new CMUShipWeaponsArmBuiMsg("CMU120mmHEBarrage", "CMU380mmHEBarrage"));
                Send(same, actor, new CMUShipWeaponsArmBuiMsg("CMUGasStrike", "CMUSmokeStrike"));
                Send(different, actor, new CMUShipWeaponsArmBuiMsg("CMUPrecisionStrike", "CMUAirburstStrike"));
                Assert.That(SComp<CMUShipWeaponsComponent>(ship).Weapons.Keys.Select(k => k.Id),
                    Is.EquivalentTo(new[] { "CMU120mmHEBarrage", "CMU380mmHEBarrage" }));
                Assert.That(SComp<CMUShipWeaponsComponent>(differentShip).Weapons.Keys.Select(k => k.Id),
                    Is.EquivalentTo(new[] { "CMUPrecisionStrike", "CMUAirburstStrike" }));
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                foreach (var uid in mapsToDelete)
                {
                    if (!SEntMan.Deleted(uid))
                        SEntMan.DeleteEntity(uid);
                }
            });
        }
    }

    [Test]
    public async Task InvalidAndUnauthorizedArmingCannotLockTheShip()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var actor = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var terminal = SEntMan.SpawnEntity("CMUShipWeaponsTerminalGovfor", map.GridCoords);
            Send(terminal, actor, new CMUShipWeaponsArmBuiMsg("CMUGasStrike", "CMUSmokeStrike"));
            Assert.That(SEntMan.HasComponent<CMUShipWeaponsComponent>(map.Grid), Is.False);
            SEntMan.RemoveComponent<AccessReaderComponent>(terminal);
            Send(terminal, actor, new CMUShipWeaponsArmBuiMsg("CMUGasStrike", "CMUGasStrike"));
            Send(terminal, actor, new CMUShipWeaponsArmBuiMsg("NotAShipWeapon", "CMUSmokeStrike"));
            Assert.That(SEntMan.HasComponent<CMUShipWeaponsComponent>(map.Grid), Is.False);
            Send(terminal, actor, new CMUShipWeaponsArmBuiMsg("CMUGasStrike", "CMUSmokeStrike"));
            Assert.That(SComp<CMUShipWeaponsComponent>(map.Grid).Weapons, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public async Task OverwatchConsumesExactlyThreeUsesAcrossConsolesAndRejectsInvalidLaunches()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var actor = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var terminal = Terminal(map.GridCoords);
            var first = Console(map.GridCoords);
            var second = Console(map.GridCoords);
            Send(terminal, actor, new CMUShipWeaponsArmBuiMsg("CMUSmokeStrike", "CMUEMSStrike"));
            var state = SComp<CMUShipWeaponsComponent>(map.Grid);
            Send(first, actor, new CMUShipWeaponSelectBuiMsg("CMUGasStrike", CMUShipWeaponBearing.North), true);
            Assert.That(SComp<OverwatchConsoleComponent>(first).CMUSelectedShipWeapon, Is.Null);
            foreach (var console in new[] { first, second })
                Send(console, actor, new CMUShipWeaponSelectBuiMsg("CMUSmokeStrike", CMUShipWeaponBearing.North), true);

            // No planet/area: launch must not consume a use.
            Send(first, actor, new OverwatchConsoleOrbitalLaunchBuiMsg(), true);
            Assert.That(state.Weapons["CMUSmokeStrike"], Is.EqualTo(3));
            Assert.That(Strikes(), Is.Empty);

            var planet = SEntMan.EnsureComponent<RMCPlanetComponent>(map.MapUid);
            planet.Offset = Vector2i.Zero;
            var areaGrid = SEntMan.EnsureComponent<AreaGridComponent>(map.Grid);
            SEntMan.System<AreaSystem>().ReplaceArea(areaGrid, Vector2i.Zero, "RMCAreaDesertDamInterior");
            var area = SComp<AreaComponent>(areaGrid.AreaEntities["RMCAreaDesertDamInterior"]);
            area.OB = true;
            SComp<OverwatchConsoleComponent>(first).OrbitalCoordinates = Vector2i.Zero;
            SComp<OverwatchConsoleComponent>(second).OrbitalCoordinates = Vector2i.Zero;

            Send(first, actor, new OverwatchConsoleOrbitalLaunchBuiMsg(), true);
            Assert.That(state.Weapons["CMUSmokeStrike"], Is.EqualTo(2));
            Send(second, actor, new OverwatchConsoleOrbitalLaunchBuiMsg(), true);
            Assert.That(state.Weapons["CMUSmokeStrike"], Is.EqualTo(2), "cooldown is shared across consoles");
            state.NextLaunch = TimeSpan.Zero;
            area.OB = false;
            Send(second, actor, new OverwatchConsoleOrbitalLaunchBuiMsg(), true);
            Assert.That(state.Weapons["CMUSmokeStrike"], Is.EqualTo(2), "protected targets do not consume ammunition");
            area.OB = true;
            Send(second, actor, new OverwatchConsoleOrbitalLaunchBuiMsg(), true);
            state.NextLaunch = TimeSpan.Zero;
            Send(first, actor, new OverwatchConsoleOrbitalLaunchBuiMsg(), true);
            state.NextLaunch = TimeSpan.Zero;
            Send(second, actor, new OverwatchConsoleOrbitalLaunchBuiMsg(), true);
            Assert.That(state.Weapons["CMUSmokeStrike"], Is.Zero);
            Assert.That(state.Weapons["CMUEMSStrike"], Is.EqualTo(3), "each armed weapon has its own three uses");
            Assert.That(Strikes(), Has.Count.EqualTo(3));
            Assert.That(SComp<OverwatchConsoleComponent>(second).CMUShipWeapons.Single(w => w.Weapon.Id == "CMUSmokeStrike").Remaining, Is.Zero);

            Send(terminal, actor, new CMUShipWeaponsArmBuiMsg("CMUGasStrike", "CMUPrecisionStrike"));
            Assert.That(state.Weapons["CMUSmokeStrike"], Is.Zero, "exhaustion never unlocks the loadout");
            Send(first, actor, new CMUShipWeaponSelectBuiMsg(null, CMUShipWeaponBearing.North), true);
            Assert.That(SComp<OverwatchConsoleComponent>(first).CMUSelectedShipWeapon, Is.Null,
                "the standard cannon remains independently selectable");
            foreach (var strike in Strikes())
                SEntMan.DeleteEntity(strike);
        });
    }

    [Test]
    public async Task AllNinePatternsFitInsideStandardObAndWalkingAdvancesInTheChosenDirection()
    {
        await Server.WaitAssertion(() =>
        {
            var profiles = SProtoMan.EnumeratePrototypes<CMUShipWeaponPrototype>().ToArray();
            Assert.That(profiles, Has.Length.EqualTo(9));
            EntProtoId standardId = "RMCOrbitalCannonExplosionExplosive";
            var standard = SProtoMan.Index(standardId)
                .Components["OrbitalCannonExplosion"].Component as OrbitalCannonExplosionComponent;
            Assert.That(standard, Is.Not.Null);
            var explosion = SEntMan.System<ExplosionSystem>();
            var standardRadius = standard!.Steps.Where(s => s.Type != null)
                .Max(s => explosion.IntensityToRadius(s.Total, s.Slope, s.Max));
            foreach (var profile in profiles)
            {
                Assert.That(profile.Shots, Is.GreaterThan(0), profile.ID);
                Assert.That(profile.Interval, Is.GreaterThan(TimeSpan.Zero), profile.ID);
                Assert.That(profile.FootprintRadius, Is.LessThanOrEqualTo(12), profile.ID);
                Assert.That(profile.FootprintRadius, Is.LessThan(standardRadius), profile.ID);
            }

            var walking = Profile("CMUWalkingBarrage");
            foreach (var bearing in Enum.GetValues<CMUShipWeaponBearing>())
            {
                var start = CMUShipWeaponsSystem.WalkingOffset(walking, 0, bearing);
                var end = CMUShipWeaponsSystem.WalkingOffset(walking, walking.Shots - 1, bearing);
                Assert.That(Vector2.Distance(start, end), Is.EqualTo(walking.WalkingDistance).Within(0.001));
                Assert.That(start + end, Is.EqualTo(Vector2.Zero));
                for (var shot = 0; shot < walking.Shots; shot++)
                {
                    var point = CMUShipWeaponsSystem.WalkingOffset(walking, shot, bearing);
                    Assert.That(point.Length() + walking.BlastRadius, Is.LessThanOrEqualTo(walking.FootprintRadius));
                }
            }

            Assert.That(Profile("CMUGatlingBarrage").Interval, Is.LessThan(Profile("CMU120mmHEBarrage").Interval));
            Assert.That(Profile("CMU380mmHEBarrage").BlastRadius, Is.GreaterThan(Profile("CMU120mmHEBarrage").BlastRadius));
            Assert.That(Profile("CMUPrecisionStrike").Spread, Is.Zero);
            Assert.That(Profile("CMUAirburstStrike").Airburst, Is.True);
        });
    }

    [TestCase("CMUSmokeStrike", "CMUShipWeaponSmoke")]
    [TestCase("CMUGasStrike", "CMUShipWeaponGas")]
    [TestCase("CMUEMSStrike", "CMUShipWeaponEmsCloud")]
    public async Task SupportStrikesCreateBoundedNonSpreadingClouds(string weapon, string cloud)
    {
        var map = await Pair.CreateTestMap();
        EntityUid victim = default;
        await Server.WaitAssertion(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            var areaGrid = SEntMan.EnsureComponent<AreaGridComponent>(map.Grid);
            var areas = SEntMan.System<AreaSystem>();
            var tile = Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId;
            for (var x = -8; x <= 8; x++)
            for (var y = -8; y <= 8; y++)
            {
                maps.SetTile(map.Grid, new Vector2i(x, y), new Tile(tile));
                areas.ReplaceArea(areaGrid, new Vector2i(x, y), "RMCAreaDesertDamInterior");
            }
            SComp<AreaComponent>(areaGrid.AreaEntities["RMCAreaDesertDamInterior"]).OB = true;
            victim = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, new Vector2(.5f)));
            var profile = SProtoMan.Index<CMUShipWeaponPrototype>(weapon);
            var strike = SEntMan.SpawnEntity("CMUShipWeaponStrike", new EntityCoordinates(map.Grid, new Vector2(.5f)));
            var firing = SEntMan.EnsureComponent<CMUShipWeaponStrikeComponent>(strike);
            firing.Weapon = weapon;
            SEntMan.System<CMUShipWeaponsSystem>().Update(0);
            var clouds = new List<EntityUid>();
            var entities = SEntMan.EntityQueryEnumerator<MetaDataComponent>();
            while (entities.MoveNext(out var uid, out var metadata))
            {
                if (metadata.EntityPrototype?.ID == cloud)
                    clouds.Add(uid);
            }
            var expected = 0;
            for (var x = -profile.CloudRadius; x <= profile.CloudRadius; x++)
            for (var y = -profile.CloudRadius; y <= profile.CloudRadius; y++)
            {
                if (x * x + y * y <= profile.CloudRadius * profile.CloudRadius)
                    expected++;
            }
            Assert.That(clouds, Has.Count.EqualTo(expected));
            foreach (var entity in clouds)
            {
                Assert.That(SEntMan.HasComponent<EvenSmokeComponent>(entity), Is.False,
                    "clouds must not spread beyond the strike footprint");
                Assert.That(SEntMan.HasComponent<DamageOverTimeComponent>(entity), Is.EqualTo(weapon == "CMUGasStrike"));
            }
        });

        await Pair.RunTicksSync(120);
        await Server.WaitAssertion(() =>
        {
            var poison = SComp<DamageableComponent>(victim).Damage.DamageDict.GetValueOrDefault("Poison").Float();
            Assert.That(poison > 0, Is.EqualTo(weapon == "CMUGasStrike"), "only the toxic cloud inflicts poison damage");
            Assert.That(SEntMan.HasComponent<RMCSuperSlowdownComponent>(victim), Is.EqualTo(weapon == "CMUEMSStrike"),
                "EMS actually immobilizes occupants without using a damaging explosion");
        });
    }

    [Test]
    public async Task PrecisionStrikeActuallyDamagesItsTargetAndCompletes()
    {
        var map = await Pair.CreateTestMap();
        EntityUid victim = default, strike = default;
        await Server.WaitAssertion(() =>
        {
            var areaGrid = SEntMan.EnsureComponent<AreaGridComponent>(map.Grid);
            SEntMan.System<AreaSystem>().ReplaceArea(areaGrid, Vector2i.Zero, "RMCAreaDesertDamInterior");
            SComp<AreaComponent>(areaGrid.AreaEntities["RMCAreaDesertDamInterior"]).OB = true;
            var coordinates = new EntityCoordinates(map.Grid, new Vector2(.5f));
            victim = SEntMan.SpawnEntity("CMMobHuman", coordinates);
            strike = SEntMan.SpawnEntity("CMUShipWeaponStrike", coordinates);
            var firing = SEntMan.EnsureComponent<CMUShipWeaponStrikeComponent>(strike);
            firing.Weapon = "CMUPrecisionStrike";
            SEntMan.System<CMUShipWeaponsSystem>().Update(0);
        });

        await Pair.RunTicksSync(30);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(strike), Is.True, "the one-shot strike cleans itself up");
            Assert.That(SEntMan.Deleted(victim) ||
                SEntMan.System<DamageableSystem>().GetTotalDamage((victim, SComp<DamageableComponent>(victim))) > 0,
                Is.True, "the deterministic precision impact must damage a target at its coordinates");
        });
    }
}
