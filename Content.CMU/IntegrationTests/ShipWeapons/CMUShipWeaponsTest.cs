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
using Content.Shared._RMC14.Smoke;
using Content.Shared.Access.Components;
using Content.Shared.CCVar;
using Content.Shared.CMU14.ShipWeapons;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Maps;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Content.Shared.Tag;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
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

    [TestCase(false)]
    [TestCase(true)]
    public async Task DecksShareTheirShipButIdenticalMultiDeckShipsDoNotShareLoadouts(bool requireGuns)
    {
        var mapsToDelete = new List<EntityUid>();
        var config = Server.ResolveDependency<IConfigurationManager>();
        try
        {
            await Server.WaitAssertion(() =>
            {
                config.SetCVar(CCVars.ShipWeaponsRequireGuns, requireGuns);
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
                var grids = new List<EntityUid>();
                var tileId = Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId;
                foreach (var mapId in new[] { lowerId, upperId, otherId })
                {
                    var grid = maps.CreateGridEntity(mapId);
                    maps.SetTile(grid, Vector2i.Zero, new Tile(tileId));
                    grids.Add(grid);
                }
                var first = Terminal(new EntityCoordinates(grids[0], new Vector2(.5f)));
                var same = Terminal(new EntityCoordinates(grids[1], new Vector2(.5f)));
                var different = Terminal(new EntityCoordinates(grids[2], new Vector2(.5f)));
                if (requireGuns)
                {
                    SEntMan.SpawnEntity("CMUShipWeaponGunHeavy", new EntityCoordinates(grids[1], new Vector2(.5f)));
                    SEntMan.SpawnEntity("CMUShipWeaponGunRapid", new EntityCoordinates(grids[2], new Vector2(.5f)));
                }
                var actor = SEntMan.SpawnEntity("CMMobHuman", new MapCoordinates(Vector2.Zero, lowerId));
                Assert.That(system.TryGetShip(first, out var ship), Is.True);
                Assert.That(system.TryGetShip(same, out var sameShip), Is.True);
                Assert.That(system.TryGetShip(different, out var differentShip), Is.True);
                Assert.That(ship, Is.EqualTo(a.Owner));
                Assert.That(sameShip, Is.EqualTo(ship));
                Assert.That(differentShip, Is.EqualTo(b.Owner));

                if (requireGuns)
                {
                    Send(first, actor, new CMUShipWeaponsArmBuiMsg("CMUPrecisionStrike", "CMUWalkingBarrage"));
                    Assert.That(SEntMan.HasComponent<CMUShipWeaponsComponent>(ship), Is.False,
                        "the other network's rapid battery cannot unlock this ship's weapons");
                }
                Send(first, actor, new CMUShipWeaponsArmBuiMsg("CMU120mmHEBarrage", "CMU380mmHEBarrage"));
                Send(same, actor, new CMUShipWeaponsArmBuiMsg("CMUGasStrike", "CMUSmokeStrike"));
                Send(different, actor, new CMUShipWeaponsArmBuiMsg("CMUPrecisionStrike", "CMUWalkingBarrage"));
                Assert.That(SComp<CMUShipWeaponsComponent>(ship).Weapons.Keys.Select(k => k.Id),
                    Is.EquivalentTo(new[] { "CMU120mmHEBarrage", "CMU380mmHEBarrage" }));
                Assert.That(SComp<CMUShipWeaponsComponent>(differentShip).Weapons.Keys.Select(k => k.Id),
                    Is.EquivalentTo(new[] { "CMUPrecisionStrike", "CMUWalkingBarrage" }));
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                config.SetCVar(CCVars.ShipWeaponsRequireGuns, false);
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
    public async Task TenProfilesRespectTheirRequestedFootprintsAndWalkingDirection()
    {
        await Server.WaitAssertion(() =>
        {
            var profiles = SProtoMan.EnumeratePrototypes<CMUShipWeaponPrototype>().ToArray();
            Assert.That(profiles, Has.Length.EqualTo(10));
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
                if (profile.Cloud != null)
                    Assert.That(profile.CloudRadius, Is.EqualTo(30), profile.ID);
                else
                {
                    Assert.That(profile.FootprintRadius, Is.LessThanOrEqualTo(12), profile.ID);
                    Assert.That(profile.FootprintRadius, Is.LessThan(standardRadius), profile.ID);
                }
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
        EntityUid victim = default, xeno = default;
        await Server.WaitAssertion(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            var areaGrid = SEntMan.EnsureComponent<AreaGridComponent>(map.Grid);
            var areas = SEntMan.System<AreaSystem>();
            var tile = Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId;
            for (var x = -31; x <= 31; x++)
            for (var y = -31; y <= 31; y++)
            {
                maps.SetTile(map.Grid, new Vector2i(x, y), new Tile(tile));
                areas.ReplaceArea(areaGrid, new Vector2i(x, y), "RMCAreaDesertDamInterior");
            }
            SComp<AreaComponent>(areaGrid.AreaEntities["RMCAreaDesertDamInterior"]).OB = true;
            victim = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, new Vector2(.5f)));
            xeno = SEntMan.SpawnEntity("CMXenoDrone", new EntityCoordinates(map.Grid, new Vector2(.5f)));
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
            Assert.That(SComp<DamageableComponent>(xeno).Damage.DamageDict.GetValueOrDefault("Poison").Float(), Is.Zero,
                "orbital gas never damages xenos");
            Assert.That(SEntMan.HasComponent<StunnedComponent>(victim), Is.EqualTo(weapon == "CMUEMSStrike"),
                "EMS actually immobilizes occupants without using a damaging explosion");
            Assert.That(SEntMan.HasComponent<StunnedComponent>(xeno), Is.EqualTo(weapon == "CMUEMSStrike"));
        });
    }

    [Test]
    public async Task BatteriesAcceptExactlyTheirConfiguredMissionRounds()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var slots = SEntMan.System<ItemSlotsSystem>();
            var ammo = new[]
            {
                "CMUShipWeaponRound120mm", "CMUShipWeaponRound380mm", "CMUShipWeaponRoundAirburst",
                "CMUShipWeaponRoundGas", "CMUShipWeaponRoundEMS", "CMUShipWeaponRoundSmoke",
                "CMUShipWeaponRoundGatling", "CMUShipWeaponRoundWalking", "CMUShipWeaponRoundPrecision",
                "CMUShipWeaponRoundRailcannon",
            };
            var supported = new List<ProtoId<CMUShipWeaponPrototype>>();
            foreach (var prototype in new[]
            {
                "CMUShipWeaponGunHeavy", "CMUShipWeaponGunSupport",
                "CMUShipWeaponGunRapid", "CMUShipWeaponGunRailcannon",
            })
            {
                var gun = SEntMan.SpawnEntity(prototype, map.GridCoords);
                var component = SComp<CMUShipWeaponGunComponent>(gun);
                supported.AddRange(component.Weapons);
                foreach (var roundPrototype in ammo)
                {
                    var round = SEntMan.SpawnEntity(roundPrototype, map.GridCoords);
                    var expected = component.Weapons.Contains(SComp<CMUShipWeaponAmmoComponent>(round).Weapon);
                    Assert.That(slots.TryInsert(gun, "mission-round", round, null), Is.EqualTo(expected),
                        $"{prototype} and {roundPrototype}");
                    if (expected)
                        Assert.That(slots.TryEject(gun, "mission-round", null, out _), Is.True);
                    SEntMan.DeleteEntity(round);
                }
                SEntMan.DeleteEntity(gun);
            }
            Assert.That(supported.Select(w => w.Id),
                Is.EquivalentTo(SProtoMan.EnumeratePrototypes<CMUShipWeaponPrototype>().Select(p => p.ID)),
                "every mission has exactly one default battery group");
        });
    }

    [Test]
    public async Task RequiredBatteriesAreShipLocalAndOnlyConsumeMatchingRoundsOnAcceptedLaunches()
    {
        var map = await Pair.CreateTestMap();
        var config = Server.ResolveDependency<IConfigurationManager>();
        try
        {
            await Server.WaitAssertion(() =>
            {
                config.SetCVar(CCVars.ShipWeaponsRequireGuns, true);
                var maps = SEntMan.System<SharedMapSystem>();
                var other = maps.CreateGridEntity(map.MapId);
                maps.SetTile(other, Vector2i.Zero, map.Tile.Tile);
                SEntMan.System<SharedTransformSystem>().SetCoordinates(other,
                    new EntityCoordinates(map.MapUid, new Vector2(100, 0)));
                var foreignGun = SEntMan.SpawnEntity("CMUShipWeaponGunSupport", new EntityCoordinates(other, new Vector2(.5f)));
                var actor = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
                var terminal = Terminal(map.GridCoords);
                var console = Console(map.GridCoords);
                Send(terminal, actor, new CMUShipWeaponsArmBuiMsg("CMUGasStrike", "CMUSmokeStrike"));
                Assert.That(SEntMan.HasComponent<CMUShipWeaponsComponent>(map.Grid), Is.False,
                    "an identical battery on another ship cannot unlock arming");
                var gun = SEntMan.SpawnEntity("CMUShipWeaponGunSupport", map.GridCoords);
                var transform = SEntMan.System<SharedTransformSystem>();
                transform.Unanchor(gun);
                Send(terminal, actor, new CMUShipWeaponsArmBuiMsg("CMUGasStrike", "CMUSmokeStrike"));
                Assert.That(SEntMan.HasComponent<CMUShipWeaponsComponent>(map.Grid), Is.False,
                    "an uninstalled battery cannot unlock arming");
                Assert.That(transform.AnchorEntity(gun), Is.True);
                Send(terminal, actor, new CMUShipWeaponsArmBuiMsg("CMU120mmHEBarrage", "CMUSmokeStrike"));
                Assert.That(SEntMan.HasComponent<CMUShipWeaponsComponent>(map.Grid), Is.False);
                Send(terminal, actor, new CMUShipWeaponsArmBuiMsg("CMUGasStrike", "CMUSmokeStrike"));
                var state = SComp<CMUShipWeaponsComponent>(map.Grid);
                Assert.That(state.Weapons.Values, Is.All.EqualTo(3), "arming does not require loaded rounds");
                var ui = SEntMan.System<SharedUserInterfaceSystem>();
                Assert.That(ui.TryGetUiState<CMUShipWeaponsBuiState>(terminal, CMUShipWeaponsUiKey.Key, out var choices), Is.True);
                Assert.That(choices!.AvailableWeapons.Select(w => w.Id),
                    Is.EquivalentTo(new[] { "CMUGasStrike", "CMUEMSStrike", "CMUSmokeStrike" }));

                var slots = SEntMan.System<ItemSlotsSystem>();
                var foreignRound = SEntMan.SpawnEntity("CMUShipWeaponRoundSmoke", new EntityCoordinates(other, new Vector2(.5f)));
                Assert.That(slots.TryInsert(foreignGun, "mission-round", foreignRound, null), Is.True);
                var planet = SEntMan.EnsureComponent<RMCPlanetComponent>(map.MapUid);
                planet.Offset = Vector2i.Zero;
                var areaGrid = SEntMan.EnsureComponent<AreaGridComponent>(map.Grid);
                SEntMan.System<AreaSystem>().ReplaceArea(areaGrid, Vector2i.Zero, "RMCAreaDesertDamInterior");
                var area = SComp<AreaComponent>(areaGrid.AreaEntities["RMCAreaDesertDamInterior"]);
                area.OB = true;
                SComp<OverwatchConsoleComponent>(console).OrbitalCoordinates = Vector2i.Zero;
                Send(console, actor, new CMUShipWeaponSelectBuiMsg("CMUSmokeStrike", CMUShipWeaponBearing.North), true);
                Send(console, actor, new OverwatchConsoleOrbitalLaunchBuiMsg(), true);
                Assert.That(state.Weapons["CMUSmokeStrike"], Is.EqualTo(3));
                Assert.That(SEntMan.Deleted(foreignRound), Is.False, "cannot fire a loaded battery on the other ship");

                var wrongRound = SEntMan.SpawnEntity("CMUShipWeaponRound120mm", map.GridCoords);
                Assert.That(slots.TryInsert(gun, "mission-round", wrongRound, actor), Is.False);
                var gasRound = SEntMan.SpawnEntity("CMUShipWeaponRoundGas", map.GridCoords);
                Assert.That(SEntMan.System<SharedHandsSystem>().TryPickupAnyHand(actor, gasRound), Is.True);
                Assert.That(slots.TryGetSlot(gun, "mission-round", out var slot), Is.True);
                Assert.That(slots.TryInsertFromHand(gun, slot!, actor), Is.True, "players can manually load mission rounds");
                Send(console, actor, new OverwatchConsoleOrbitalLaunchBuiMsg(), true);
                Assert.That(state.Weapons["CMUSmokeStrike"], Is.EqualTo(3), "loaded gas cannot fire smoke");
                Assert.That(SEntMan.Deleted(gasRound), Is.False);
                Assert.That(slots.TryEject(gun, "mission-round", null, out var ejected), Is.True);
                Assert.That(ejected, Is.EqualTo(gasRound));
                var smokeRound = SEntMan.SpawnEntity("CMUShipWeaponRoundSmoke", map.GridCoords);
                Assert.That(slots.TryInsert(gun, "mission-round", smokeRound, actor), Is.True);
                area.OB = false;
                Send(console, actor, new OverwatchConsoleOrbitalLaunchBuiMsg(), true);
                Assert.That(SEntMan.Deleted(smokeRound), Is.False, "invalid targets never consume physical rounds");
                area.OB = true;
                Send(console, actor, new OverwatchConsoleOrbitalLaunchBuiMsg(), true);
                Assert.That(SEntMan.Deleted(smokeRound), Is.True);
                Assert.That(state.Weapons["CMUSmokeStrike"], Is.EqualTo(2));
                Assert.That(SEntMan.System<SharedContainerSystem>().GetContainer(gun, "mission-round").ContainedEntities, Is.Empty);
                var reload = SEntMan.SpawnEntity("CMUShipWeaponRoundSmoke", map.GridCoords);
                Assert.That(slots.TryInsert(gun, "mission-round", reload, actor), Is.True);
                Send(console, actor, new OverwatchConsoleOrbitalLaunchBuiMsg(), true);
                Assert.That(SEntMan.Deleted(reload), Is.False, "cooldown failures never consume loaded rounds");
                SEntMan.DeleteEntity(gun);
                state.NextLaunch = TimeSpan.Zero;
                Send(console, actor, new OverwatchConsoleOrbitalLaunchBuiMsg(), true);
                Assert.That(state.Weapons["CMUSmokeStrike"], Is.EqualTo(2), "removing the gun prevents firing");
                config.SetCVar(CCVars.ShipWeaponsRequireGuns, false);
                Send(terminal, actor, new CMUShipWeaponsArmBuiMsg("CMU120mmHEBarrage", "CMU380mmHEBarrage"));
                Assert.That(state.Weapons.ContainsKey("CMUSmokeStrike"), Is.True, "CVar toggles cannot change locked choices");
                Send(console, actor, new OverwatchConsoleOrbitalLaunchBuiMsg(), true);
                Assert.That(state.Weapons["CMUSmokeStrike"], Is.EqualTo(1), "CVar off restores gun-free firing");
                foreach (var strike in Strikes())
                    SEntMan.DeleteEntity(strike);
            });
        }
        finally
        {
            await Server.WaitPost(() => config.SetCVar(CCVars.ShipWeaponsRequireGuns, false));
        }
    }

    [Test]
    public async Task RailcannonDamagesExactlyFiveByFiveTilesWithoutRadialSpill()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            var areaGrid = SEntMan.EnsureComponent<AreaGridComponent>(map.Grid);
            var victims = new Dictionary<Vector2i, EntityUid>();
            var structures = new Dictionary<Vector2i, EntityUid>();
            for (var x = -3; x <= 3; x++)
            for (var y = -3; y <= 3; y++)
            {
                var tile = new Vector2i(x, y);
                maps.SetTile(map.Grid, tile, map.Tile.Tile);
                SEntMan.System<AreaSystem>().ReplaceArea(areaGrid, tile, "RMCAreaDesertDamInterior");
                victims[tile] = SEntMan.SpawnEntity("CMMobHuman",
                    new EntityCoordinates(map.Grid, new Vector2(x + .5f, y + .5f)));
                structures[tile] = SEntMan.SpawnEntity(null,
                    new EntityCoordinates(map.Grid, new Vector2(x + .5f, y + .5f)));
                SEntMan.EnsureComponent<DamageableComponent>(structures[tile]);
                SEntMan.EnsureComponent<InjurableComponent>(structures[tile]);
            }
            SComp<AreaComponent>(areaGrid.AreaEntities["RMCAreaDesertDamInterior"]).OB = true;
            var strike = SEntMan.SpawnEntity("CMUShipWeaponStrike", new EntityCoordinates(map.Grid, new Vector2(.5f)));
            SEntMan.EnsureComponent<CMUShipWeaponStrikeComponent>(strike).Weapon = "CMURailcannonStrike";
            SEntMan.System<CMUShipWeaponsSystem>().Update(0);
            foreach (var (tile, victim) in victims)
            {
                var structuralDamage = SEntMan.System<DamageableSystem>()
                    .GetTotalDamage((structures[tile], SComp<DamageableComponent>(structures[tile]))).Float();
                if (Math.Abs(tile.X) <= 2 && Math.Abs(tile.Y) <= 2)
                {
                    Assert.That(SEntMan.Deleted(victim) || SEntMan.IsQueuedForDeletion(victim) ||
                        SComp<MobStateComponent>(victim).CurrentState == MobState.Dead,
                        Is.True, $"lethal whole-body trauma at {tile}");
                    Assert.That(structuralDamage, Is.EqualTo(4000), $"full structural damage at {tile}");
                }
                else
                {
                    Assert.That(SEntMan.System<DamageableSystem>()
                        .GetTotalDamage((victim, SComp<DamageableComponent>(victim))).Float(),
                        Is.Zero, $"no damage outside 5x5 at {tile}");
                    Assert.That(structuralDamage, Is.Zero, $"no structural damage outside 5x5 at {tile}");
                }
            }
        });
    }

    [Test]
    public async Task EmsStunsInitialAndLateEntrantsUntilTwentyFiveSecondsAfterItsFieldEnds()
    {
        var map = await Pair.CreateTestMap();
        EntityUid initial = default, late = default, outside = default, field = default;
        TimeSpan deadline = default;
        await Server.WaitAssertion(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            var areaGrid = SEntMan.EnsureComponent<AreaGridComponent>(map.Grid);
            foreach (var x in new[] { 0, 30, 31 })
            {
                maps.SetTile(map.Grid, new Vector2i(x, 0), map.Tile.Tile);
                SEntMan.System<AreaSystem>().ReplaceArea(areaGrid, new Vector2i(x, 0), "RMCAreaDesertDamInterior");
            }
            SComp<AreaComponent>(areaGrid.AreaEntities["RMCAreaDesertDamInterior"]).OB = true;
            var center = new EntityCoordinates(map.Grid, new Vector2(.5f));
            initial = SEntMan.SpawnEntity("CMMobHuman", center);
            ProtoId<TagPrototype> immune = "StunImmune";
            SEntMan.System<TagSystem>().AddTag(initial, immune);
            late = SEntMan.SpawnEntity("CMXenoDrone", new EntityCoordinates(map.Grid, new Vector2(31.5f, .5f)));
            outside = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, new Vector2(31.5f, .5f)));
            var strike = SEntMan.SpawnEntity("CMUShipWeaponStrike", center);
            SEntMan.EnsureComponent<CMUShipWeaponStrikeComponent>(strike).Weapon = "CMUEMSStrike";
            SEntMan.System<CMUShipWeaponsSystem>().Update(0);
            var fields = SEntMan.EntityQueryEnumerator<CMUShipWeaponEmsFieldComponent>();
            Assert.That(fields.MoveNext(out field, out var ems), Is.True);
            deadline = ems!.EndsAt + TimeSpan.FromSeconds(25);
            Assert.That(ems.EndsAt - SGameTiming.CurTime, Is.EqualTo(TimeSpan.FromSeconds(15)));
        });
        await Pair.RunSeconds(10);
        await Server.WaitAssertion(() =>
        {
            var status = SEntMan.System<StatusEffectsSystem>();
            Assert.That(status.TryGetTime(initial, CMUShipWeaponsSystem.EmsStun, out var initialTime), Is.True);
            Assert.That(initialTime.EndEffectTime, Is.EqualTo(deadline), "ordinary stun immunity is bypassed");
            Assert.That(SEntMan.HasComponent<StunnedComponent>(initial), Is.True);
            Assert.That(SEntMan.HasComponent<StunnedComponent>(late), Is.False);
            SEntMan.System<SharedTransformSystem>().SetCoordinates(initial,
                new EntityCoordinates(map.Grid, new Vector2(31.5f, .5f)));
            SEntMan.System<SharedTransformSystem>().SetCoordinates(late,
                new EntityCoordinates(map.Grid, new Vector2(30.5f, .5f)));
            Assert.That(status.TryGetTime(late, CMUShipWeaponsSystem.EmsStun, out var lateTime), Is.True,
                "crossing the radius boundary applies EMS immediately, without waiting for a pulse");
            Assert.That(lateTime.EndEffectTime, Is.EqualTo(deadline));
        });
        await Pair.RunSeconds(6);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(field), Is.True, "field ends after 15 seconds");
            var status = SEntMan.System<StatusEffectsSystem>();
            foreach (var victim in new[] { initial, late })
            {
                Assert.That(status.TryGetTime(victim, CMUShipWeaponsSystem.EmsStun, out var time), Is.True);
                Assert.That(time.EndEffectTime, Is.EqualTo(deadline), "entering late or leaving early does not alter expiry");
                Assert.That(SEntMan.HasComponent<StunnedComponent>(victim), Is.True);
            }
            Assert.That(SEntMan.HasComponent<StunnedComponent>(outside), Is.False);
        });
        await Pair.RunSeconds(23);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<StunnedComponent>(initial), Is.True);
            Assert.That(SEntMan.HasComponent<StunnedComponent>(late), Is.True);
        });
        await Pair.RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<StunnedComponent>(initial), Is.False);
            Assert.That(SEntMan.HasComponent<StunnedComponent>(late), Is.False);
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
