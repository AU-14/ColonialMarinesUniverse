/*
#pragma warning disable RA0002 // Tests configure targets, cooldowns and permission fixtures directly.
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client.Guidebook;
using Content.Client.Guidebook.Components;
using Content.Client.Guidebook.Richtext;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.ShipWeapons;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Damage;
using Content.Shared._RMC14.Overwatch;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.Smoke;
using Content.Shared._RMC14.Slow;
using Content.Shared.Access.Components;
using Content.Shared.CCVar;
using Content.Shared.CMU14.ShipWeapons;
using Content.Shared.CMU14.GasMask;
using Content.Shared.CMU14.Medical.Anatomy.Organs;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Brain;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Lungs;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Guidebook;
using Content.Shared.Maps;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Content.Shared.Tag;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.ShipWeapons;

[TestFixture]
[TestOf(typeof(CMUShipWeaponsSystem))]
public sealed class CMUShipWeaponsTest : GameTest
{
    /// <summary>
    ///     Most of these tests cover arming and firing without physical batteries; the ones that need batteries turn
    ///     them on themselves.
    /// </summary>
    [SetUp]
    public async Task UseGunFreeShipWeapons()
    {
        await Server.WaitPost(() =>
            Server.ResolveDependency<IConfigurationManager>().SetCVar(CCVars.ShipWeaponsRequireGuns, false));
    }

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
            Assert.That(a.Weapons.Select(k => k.Id), Is.EquivalentTo(new[] { "CMUGatlingBarrage", "CMUWalkingBarrage" }));
            Assert.That(b.Weapons.Select(k => k.Id), Is.EquivalentTo(new[] { "CMUGasStrike", "CMUSmokeStrike" }));

            SEntMan.DeleteEntity(first);
            var replacement = Terminal(map.GridCoords);
            Send(replacement, actor, new CMUShipWeaponsArmBuiMsg("CMUPrecisionStrike", "CMUEMSStrike"));
            Assert.That(a.Weapons.Select(k => k.Id), Is.EquivalentTo(new[] { "CMUGatlingBarrage", "CMUWalkingBarrage" }));
            Assert.That(a.RemainingUses, Is.EqualTo(2));
            Assert.That(b.RemainingUses, Is.EqualTo(2));
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
                    SEntMan.SpawnEntity("CMUShipWeaponGun", new EntityCoordinates(grids[2], new Vector2(.5f)));
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
                        "the other network's battery cannot unlock this ship's weapons");
                    SEntMan.SpawnEntity("CMUShipWeaponGun", new EntityCoordinates(grids[1], new Vector2(.5f)));
                }
                Send(first, actor, new CMUShipWeaponsArmBuiMsg("CMU120mmHEBarrage", "CMU380mmHEBarrage"));
                Send(same, actor, new CMUShipWeaponsArmBuiMsg("CMUGasStrike", "CMUSmokeStrike"));
                Send(different, actor, new CMUShipWeaponsArmBuiMsg("CMUPrecisionStrike", "CMUWalkingBarrage"));
                Assert.That(SComp<CMUShipWeaponsComponent>(ship).Weapons.Select(k => k.Id),
                    Is.EquivalentTo(new[] { "CMU120mmHEBarrage", "CMU380mmHEBarrage" }));
                Assert.That(SComp<CMUShipWeaponsComponent>(differentShip).Weapons.Select(k => k.Id),
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
            Send(terminal, actor, new CMUShipWeaponsArmBuiMsg());
            Send(terminal, actor, new CMUShipWeaponsArmBuiMsg(
                "CMUGasStrike", "CMUSmokeStrike", "CMUEMSStrike", "CMUPrecisionStrike"));
            Assert.That(SEntMan.HasComponent<CMUShipWeaponsComponent>(map.Grid), Is.False);
            Send(terminal, actor, new CMUShipWeaponsArmBuiMsg("CMUGasStrike", "CMUSmokeStrike"));
            Assert.That(SComp<CMUShipWeaponsComponent>(map.Grid).Weapons, Has.Count.EqualTo(2));
        });
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public async Task CommandCanPermanentlySelectOneToThreeWeapons(int count)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var actor = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var terminal = Terminal(map.GridCoords);
            // The YAML default is two missions; this checks any loadout up to the terminal's own limit locks in.
            SComp<CMUShipWeaponsTerminalComponent>(terminal).MaxSelections = 3;
            ProtoId<CMUShipWeaponPrototype>[] weapons = ["CMUGasStrike", "CMUSmokeStrike", "CMUEMSStrike"];
            Send(terminal, actor, new CMUShipWeaponsArmBuiMsg(weapons.Take(count).ToArray()));
            var state = SComp<CMUShipWeaponsComponent>(map.Grid);
            Assert.That(state.Weapons, Is.EquivalentTo(weapons.Take(count)));
            Assert.That(state.RemainingUses, Is.EqualTo(2));
            Send(terminal, actor, new CMUShipWeaponsArmBuiMsg(weapons));
            Assert.That(state.Weapons, Has.Count.EqualTo(count), "smaller loadouts cannot be topped up later");
        });
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(4)]
    [TestCase(9)]
    public async Task TerminalSelectionLimitIsEnforcedAndSentToTheClient(int limit)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var actor = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var terminal = Terminal(map.GridCoords);
            var component = SComp<CMUShipWeaponsTerminalComponent>(terminal);
            Assert.That(component.MaxSelections, Is.EqualTo(2), "the YAML default is a two-mission loadout");
            component.MaxSelections = limit;
            var weapons = SProtoMan.EnumeratePrototypes<CMUShipWeaponPrototype>()
                .Select(p => new ProtoId<CMUShipWeaponPrototype>(p.ID)).Take(limit + 1).ToArray();
            Assert.That(weapons, Has.Length.EqualTo(limit + 1));
            Send(terminal, actor, new CMUShipWeaponsArmBuiMsg(weapons));
            Assert.That(SEntMan.HasComponent<CMUShipWeaponsComponent>(map.Grid), Is.False,
                "the server must reject messages exceeding this terminal's configured limit");

            Send(terminal, actor, new CMUShipWeaponsArmBuiMsg(weapons.Take(limit).ToArray()));
            var state = SComp<CMUShipWeaponsComponent>(map.Grid);
            Assert.That(state.Weapons, Has.Count.EqualTo(limit));
            Assert.That(state.RemainingUses, Is.EqualTo(2));
            var ui = SEntMan.System<SharedUserInterfaceSystem>();
            Assert.That(ui.TryGetUiState<CMUShipWeaponsBuiState>(terminal, CMUShipWeaponsUiKey.Key, out var choices), Is.True);
            Assert.That(choices!.MaxSelections, Is.EqualTo(limit));

            var replacement = Terminal(map.GridCoords);
            SComp<CMUShipWeaponsTerminalComponent>(replacement).MaxSelections = limit + 1;
            Send(replacement, actor, new CMUShipWeaponsArmBuiMsg(weapons));
            Assert.That(state.Weapons, Has.Count.EqualTo(limit),
                "a higher-limit terminal cannot expand a confirmed ship loadout");
        });
    }

    [TestCase(1)]
    [TestCase(5)]
    public async Task TerminalYamlSetsTheSharedPoolOnceWhenTheLoadoutLocks(int uses)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var actor = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var terminal = Terminal(map.GridCoords);
            var component = SComp<CMUShipWeaponsTerminalComponent>(terminal);
            Assert.That(component.Uses, Is.EqualTo(2), "the YAML default is two shared fire missions");
            component.Uses = uses;
            Send(terminal, actor, new CMUShipWeaponsArmBuiMsg("CMUGasStrike", "CMUSmokeStrike"));
            var state = SComp<CMUShipWeaponsComponent>(map.Grid);
            Assert.That(state.RemainingUses, Is.EqualTo(uses));
            Assert.That(state.MaxUses, Is.EqualTo(uses));
            var ui = SEntMan.System<SharedUserInterfaceSystem>();
            Assert.That(ui.TryGetUiState<CMUShipWeaponsBuiState>(terminal, CMUShipWeaponsUiKey.Key, out var choices), Is.True);
            Assert.That(choices!.Weapons, Has.All.Property(nameof(CMUShipWeaponStatus.Remaining)).EqualTo(uses));
            Assert.That(choices.Weapons, Has.All.Property(nameof(CMUShipWeaponStatus.Max)).EqualTo(uses));

            state.RemainingUses = 0;
            component.Uses = uses + 10;
            var replacement = Terminal(map.GridCoords);
            SComp<CMUShipWeaponsTerminalComponent>(replacement).Uses = uses + 10;
            Send(replacement, actor, new CMUShipWeaponsArmBuiMsg("CMUEMSStrike"));
            Assert.That(state.RemainingUses, Is.Zero, "editing or replacing terminals cannot refill a locked pool");
            Assert.That(state.MaxUses, Is.EqualTo(uses));
        });
    }

    [Test]
    public async Task SelectionTerminalsExposeAValidCmuShipWeaponsGuide()
    {
        await Client.WaitAssertion(() =>
        {
            ProtoId<GuideEntryPrototype> guide = "CMUGuideShipWeapons";
            var factory = Client.ResolveDependency<IComponentFactory>();
            foreach (EntProtoId terminal in new EntProtoId[]
                     { "CMUShipWeaponsTerminal", "CMUShipWeaponsTerminalGovfor", "CMUShipWeaponsTerminalOpfor" })
            {
                Assert.That(CProtoMan.Index(terminal).TryComp<GuideHelpComponent>(out var help, factory), Is.True);
                Assert.That(help!.Guides, Does.Contain(guide));
            }
            var entry = CProtoMan.Index(guide);
            using var reader = Client.ResolveDependency<IResourceManager>().ContentFileReadText(entry.Text);
            Assert.That(Client.ResolveDependency<DocumentParsingManager>().TryAddMarkup(new Document(), reader.ReadToEnd()),
                Is.True, "the inspect-linked guide parses in the actual client guidebook");
        });
    }

    [Test]
    public async Task LaunchSchedulesOneDirectionalWarningThreeSecondsLater()
    {
        var map = await Pair.CreateTestMap();
        EntityUid strike = default;
        TimeSpan warningAt = default;
        await Server.WaitAssertion(() =>
        {
            var planet = SEntMan.EnsureComponent<RMCPlanetComponent>(map.MapUid);
            planet.Offset = Vector2i.Zero;
            var areaGrid = SEntMan.EnsureComponent<AreaGridComponent>(map.Grid);
            SEntMan.System<AreaSystem>().ReplaceArea(areaGrid, Vector2i.Zero, "RMCAreaDesertDamInterior");
            SComp<AreaComponent>(areaGrid.AreaEntities["RMCAreaDesertDamInterior"]).OB = true;
            var actor = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var terminal = Terminal(map.GridCoords);
            var console = Console(map.GridCoords);
            Send(terminal, actor, new CMUShipWeaponsArmBuiMsg("CMUGatlingBarrage"));
            SComp<OverwatchConsoleComponent>(console).OrbitalCoordinates = Vector2i.Zero;
            Send(console, actor, new CMUShipWeaponSelectBuiMsg("CMUGatlingBarrage", CMUShipWeaponBearing.North), true);
            Send(console, actor, new OverwatchConsoleOrbitalLaunchBuiMsg(), true);
            strike = Strikes().Single();
            var firing = SComp<CMUShipWeaponStrikeComponent>(strike);
            warningAt = firing.DirectionalWarningAt;
            Assert.That(warningAt - SGameTiming.CurTime, Is.EqualTo(TimeSpan.FromSeconds(3)));
            Assert.That(firing.DirectionalWarned, Is.False);
        });
        await Pair.RunSeconds(2);
        await Server.WaitAssertion(() => Assert.That(SComp<CMUShipWeaponStrikeComponent>(strike).DirectionalWarned, Is.False));
        await Pair.RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            var firing = SComp<CMUShipWeaponStrikeComponent>(strike);
            Assert.That(firing.DirectionalWarned, Is.True);
            Assert.That(firing.Shot, Is.Zero, "directional warning precedes impact, not each barrage shell");
            Assert.That(firing.Warned, Is.False, "final approach remains a separate warning");
            SEntMan.System<CMUShipWeaponsSystem>().Update(0);
            Assert.That(firing.DirectionalWarningAt, Is.EqualTo(warningAt), "directional warning is not rescheduled");
            SEntMan.DeleteEntity(strike);
        });
    }

    [Test]
    public async Task OverwatchSpendsOneSharedPoolAcrossWeaponsAndConsolesAndRejectsInvalidLaunches()
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
            Assert.That(SComp<OverwatchConsoleComponent>(first).CMUSelectedShipWeapon?.Id, Is.EqualTo("CMUSmokeStrike"),
                "an armed ship always has a fire mission picked, and unarmed ones are rejected");
            foreach (var console in new[] { first, second })
                Send(console, actor, new CMUShipWeaponSelectBuiMsg("CMUSmokeStrike", CMUShipWeaponBearing.North), true);

            // No planet/area: launch must not consume a use.
            Send(first, actor, new OverwatchConsoleOrbitalLaunchBuiMsg(), true);
            Assert.That(state.RemainingUses, Is.EqualTo(2));
            Assert.That(state.MaxUses, Is.EqualTo(2));
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
            Assert.That(state.RemainingUses, Is.EqualTo(1));
            Assert.That(SComp<OverwatchConsoleComponent>(second).CMUShipWeapons, Is.EquivalentTo(new[]
                {
                    new CMUShipWeaponStatus("CMUSmokeStrike", 1, 2),
                    new CMUShipWeaponStatus("CMUEMSStrike", 1, 2),
                }),
                "every armed weapon reports the same shared pool");
            Send(second, actor, new OverwatchConsoleOrbitalLaunchBuiMsg(), true);
            Assert.That(state.RemainingUses, Is.EqualTo(1), "cooldown is shared across consoles");
            state.NextLaunch = TimeSpan.Zero;
            area.OB = false;
            Send(second, actor, new OverwatchConsoleOrbitalLaunchBuiMsg(), true);
            Assert.That(state.RemainingUses, Is.EqualTo(1), "protected targets do not consume ammunition");
            area.OB = true;
            Send(second, actor, new CMUShipWeaponSelectBuiMsg("CMUEMSStrike", CMUShipWeaponBearing.North), true);
            Send(second, actor, new OverwatchConsoleOrbitalLaunchBuiMsg(), true);
            Assert.That(state.RemainingUses, Is.Zero, "a different armed weapon spends from the same pool");
            state.NextLaunch = TimeSpan.Zero;
            Send(first, actor, new OverwatchConsoleOrbitalLaunchBuiMsg(), true);
            state.NextLaunch = TimeSpan.Zero;
            Send(second, actor, new OverwatchConsoleOrbitalLaunchBuiMsg(), true);
            Assert.That(state.RemainingUses, Is.Zero);
            Assert.That(Strikes(), Has.Count.EqualTo(2), "an empty pool rejects every armed weapon");
            Assert.That(SComp<OverwatchConsoleComponent>(second).CMUShipWeapons.Select(w => w.Remaining), Is.All.Zero);

            Send(terminal, actor, new CMUShipWeaponsArmBuiMsg("CMUGasStrike", "CMUPrecisionStrike"));
            Assert.That(state.Weapons.Select(w => w.Id), Is.EquivalentTo(new[] { "CMUSmokeStrike", "CMUEMSStrike" }),
                "exhaustion never unlocks the loadout");
            Assert.That(state.RemainingUses, Is.Zero, "re-arming cannot refill the pool");
            Send(first, actor, new CMUShipWeaponSelectBuiMsg(null, CMUShipWeaponBearing.North), true);
            Assert.That(SComp<OverwatchConsoleComponent>(first).CMUSelectedShipWeapon, Is.Not.Null,
                "the standard cannon can't be picked once the ship has fire missions");
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
            foreach (var profile in profiles)
            {
                Assert.That(profile.Shots, Is.GreaterThan(0), profile.ID);
                Assert.That(profile.Interval, Is.GreaterThan(TimeSpan.Zero), profile.ID);
                if (profile.Cloud != null)
                    Assert.That(profile.CloudRadius, Is.EqualTo(30), profile.ID);
            }

            Assert.That(Profile("CMUGatlingBarrage").Shots, Is.EqualTo(120));
            Assert.That(Profile("CMUGatlingBarrage").Spread, Is.EqualTo(16));
            Assert.That(Profile("CMUWalkingBarrage").Shots, Is.EqualTo(9));
            Assert.That(Profile("CMUWalkingBarrage").BlastRadius, Is.EqualTo(6));
            Assert.That(Profile("CMU120mmHEBarrage").Spread, Is.EqualTo(16));
            Assert.That(Profile("CMU120mmHEBarrage").BlastRadius, Is.EqualTo(12));
            Assert.That(Profile("CMU380mmHEBarrage").BlastRadius, Is.EqualTo(16));
            Assert.That(Profile("CMUAirburstStrike").Shots, Is.EqualTo(6));
            Assert.That(Profile("CMUAirburstStrike").Spread, Is.EqualTo(8));
            Assert.That(Profile("CMUAirburstStrike").BlastRadius, Is.EqualTo(12));
            var precision = Profile("CMUPrecisionStrike");
            var he120 = Profile("CMU120mmHEBarrage");
            Assert.That(precision.Shots, Is.EqualTo(1));
            Assert.That(precision.BlastRadius, Is.EqualTo(he120.BlastRadius));
            Assert.That(precision.Slope, Is.EqualTo(he120.Slope));
            Assert.That(precision.MaxIntensity, Is.EqualTo(he120.MaxIntensity));

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
            if (weapon == "CMUGasStrike")
            {
                Assert.That(SEntMan.HasComponent<RMCSlowdownComponent>(xeno), Is.True);
                Assert.That(SEntMan.System<StatusEffectsSystem>().TryGetStatusEffect(
                    xeno, CMUShipWeaponsSystem.GasHaze, out _), Is.True);
            }
        });
        if (weapon == "CMUGasStrike")
        {
            await Server.WaitPost(() =>
            {
                var clouds = SEntMan.EntityQueryEnumerator<CMUOrbitalGasComponent>();
                while (clouds.MoveNext(out var uid, out _))
                    SEntMan.QueueDeleteEntity(uid);
            });
            await Pair.RunSeconds(2);
            await Server.WaitAssertion(() =>
            {
                Assert.That(SEntMan.HasComponent<RMCSlowdownComponent>(xeno), Is.False);
                Assert.That(SEntMan.System<StatusEffectsSystem>().TryGetStatusEffect(
                    xeno, CMUShipWeaponsSystem.GasHaze, out _), Is.False,
                    "gas haze and slowdown expire after exposure ends");
            });
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task OrbitalGasDamagesLungsAndBrainEveryThreeSecondsIncludingCorpses(bool dead)
    {
        var map = await Pair.CreateTestMap();
        var organs = new Dictionary<EntityUid, float>();
        var clouds = new List<EntityUid>();
        EntityUid victim = default;
        await Server.WaitAssertion(() =>
        {
            var config = Server.ResolveDependency<IConfigurationManager>();
            config.SetCVar(CMUMedicalCCVars.Enabled, true);
            config.SetCVar(CMUMedicalCCVars.OrganEnabled, true);
            victim = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            if (dead)
                SEntMan.System<MobStateSystem>().ChangeMobState(victim, MobState.Dead);
            foreach (var organ in SEntMan.System<CMUMedicalBodyIndexSystem>().GetOrgans(victim))
            {
                if (SEntMan.HasComponent<LungsComponent>(organ.Owner) ||
                    SEntMan.HasComponent<CMUBrainComponent>(organ.Owner))
                    organs.Add(organ.Owner, SComp<OrganHealthComponent>(organ.Owner).Current.Float());
            }
            Assert.That(organs.Count, Is.GreaterThanOrEqualTo(2), "test must exercise real lungs and brain");
            for (var i = 0; i < 2; i++)
            {
                var gas = SEntMan.SpawnEntity("CMUShipWeaponGas", map.GridCoords);
                clouds.Add(gas);
                // Isolate direct organ damage from the cloud's separately tested poison damage.
                SComp<DamageOverTimeComponent>(gas).Damage = null;
            }
        });
        await Pair.RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            foreach (var (organ, initial) in organs)
                Assert.That(SComp<OrganHealthComponent>(organ).Current.Float(), Is.EqualTo(initial));
        });
        await Pair.RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            foreach (var (organ, initial) in organs)
                Assert.That(SComp<OrganHealthComponent>(organ).Current.Float(), Is.EqualTo(initial - 1),
                    "overlapping gas clouds must not multiply organ damage");
        });
        await Pair.RunSeconds(3);
        await Server.WaitAssertion(() =>
        {
            foreach (var (organ, initial) in organs)
                Assert.That(SComp<OrganHealthComponent>(organ).Current.Float(), Is.EqualTo(initial - 2));
            foreach (var cloud in clouds)
                SComp<DamageOverTimeComponent>(cloud).Damage =
                    new DamageSpecifier { DamageDict = { ["Poison"] = 12 } };
        });
        await Pair.RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SComp<DamageableComponent>(victim).Damage.DamageDict.GetValueOrDefault("Poison").Float(),
                Is.GreaterThan(0), "the cloud's existing poison also affects corpses");
        });
    }

    [TestCase("CMUGasMaskFilter", 5)]
    [TestCase("CMUGasMaskFilterHAZOPS", 15)]
    [TestCase("CMUGasMaskFilterWY", 15)]
    public async Task OrbitalGasFiltersProtectUntilTheirExposureBudgetExpires(string prototype, int seconds)
    {
        var map = await Pair.CreateTestMap();
        EntityUid victim = default, filter = default;
        await Server.WaitAssertion(() =>
        {
            victim = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var mask = SEntMan.SpawnEntity("CMUMaskGas", map.GridCoords);
            filter = SEntMan.SpawnEntity(prototype, map.GridCoords);
            Assert.That(SEntMan.System<ItemSlotsSystem>().TryInsert(mask, "filter", filter, null), Is.True);
            Assert.That(SEntMan.System<InventorySystem>().TryEquip(victim, mask, "mask", force: true), Is.True);
            SEntMan.SpawnEntity("CMUShipWeaponGas", map.GridCoords);
            SEntMan.SpawnEntity("CMUShipWeaponGas", map.GridCoords);
        });
        await Pair.RunSeconds(seconds - 0.5f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SComp<GasMaskFilterComponent>(filter).Integrity, Is.Zero,
                "the final protected tick consumes the filter");
            Assert.That(SComp<DamageableComponent>(victim).Damage.DamageDict.GetValueOrDefault("Poison").Float(),
                Is.Zero, "both standard and advanced filters must block all gas effects, including overlapping tiles");
            Assert.That(SEntMan.HasComponent<CMUOrbitalGasVictimComponent>(victim), Is.False);
        });
        await Pair.RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SComp<DamageableComponent>(victim).Damage.DamageDict.GetValueOrDefault("Poison").Float(),
                Is.GreaterThan(0), "gas penetrates after five or fifteen exposure seconds");
        });
    }

    [TestCase(5f, 50f)]
    [TestCase(7f, 18f)]
    public async Task EmsBurnsConfiguredDamageCapsAddedHumanDamageAndLeavesXenosUncapped(float damagePerSecond, float cap)
    {
        var map = await Pair.CreateTestMap();
        EntityUid human = default, xeno = default, field = default;
        float initialHeat = 0;
        await Server.WaitAssertion(() =>
        {
            var areas = SEntMan.EnsureComponent<AreaGridComponent>(map.Grid);
            SEntMan.System<AreaSystem>().ReplaceArea(areas, Vector2i.Zero, "RMCAreaDesertDamInterior");
            SComp<AreaComponent>(areas.AreaEntities["RMCAreaDesertDamInterior"]).OB = true;
            human = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            xeno = SEntMan.SpawnEntity("CMXenoDrone", map.GridCoords);
            SEntMan.System<DamageableSystem>().TryChangeDamage(human,
                new DamageSpecifier { DamageDict = { ["Heat"] = 30 } }, ignoreResistances: true,
                ignoreGlobalModifiers: true);
            initialHeat = SComp<DamageableComponent>(human).Damage.DamageDict.GetValueOrDefault("Heat").Float();
            Assert.That(initialHeat, Is.GreaterThan(0));
            field = SEntMan.SpawnEntity("CMUShipWeaponEmsField", map.GridCoords);
            var ems = SComp<CMUShipWeaponEmsFieldComponent>(field);
            Assert.That(ems.BurnDamagePerSecond, Is.EqualTo(5), "default damage comes from the EMS YAML");
            Assert.That(ems.HumanDamageCap, Is.EqualTo(50), "default cap comes from the EMS YAML");
            ems.BurnDamagePerSecond = damagePerSecond;
            ems.HumanDamageCap = cap;
            ems.EndsAt = SGameTiming.CurTime + TimeSpan.FromSeconds(15);
            SEntMan.System<CMUShipWeaponsSystem>().Update(0);
        });
        await Pair.RunSeconds(1.5f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SComp<DamageableComponent>(human).Damage.DamageDict.GetValueOrDefault("Heat").Float(),
                Is.EqualTo(initialHeat + damagePerSecond));
            Assert.That(SComp<DamageableComponent>(xeno).Damage.DamageDict.GetValueOrDefault("Heat").Float(),
                Is.EqualTo(damagePerSecond));
            for (var i = 0; i < 20; i++)
                SEntMan.System<CMUShipWeaponsSystem>().Update(0);
            Assert.That(SComp<DamageableComponent>(human).Damage.DamageDict.GetValueOrDefault("Heat").Float(),
                Is.EqualTo(initialHeat + damagePerSecond), "extra field updates cannot multiply burn damage");
        });
        await Pair.RunSeconds(13);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SComp<DamageableComponent>(human).Damage.DamageDict.GetValueOrDefault("Heat").Float(),
                Is.EqualTo(initialHeat + cap), "pre-existing burns do not reduce this strike's configured budget");
            Assert.That(SComp<DamageableComponent>(xeno).Damage.DamageDict.GetValueOrDefault("Heat").Float(),
                Is.EqualTo(14 * damagePerSecond), "xenos continue taking configured damage past the human cap");
            SEntMan.DeleteEntity(field);
        });
        await Pair.RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SComp<DamageableComponent>(xeno).Damage.DamageDict.GetValueOrDefault("Heat").Float(),
                Is.EqualTo(14 * damagePerSecond), "the lingering stun must not deal lingering burns");
            field = SEntMan.SpawnEntity("CMUShipWeaponEmsField", map.GridCoords);
            SComp<CMUShipWeaponEmsFieldComponent>(field).EndsAt = SGameTiming.CurTime + TimeSpan.FromSeconds(15);
            SEntMan.System<CMUShipWeaponsSystem>().Update(0);
        });
        await Pair.RunSeconds(1.5f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SComp<DamageableComponent>(human).Damage.DamageDict.GetValueOrDefault("Heat").Float(),
                Is.EqualTo(initialHeat + cap + 5), "a new strike uses the YAML defaults despite the existing stun");
            Assert.That(SComp<DamageableComponent>(xeno).Damage.DamageDict.GetValueOrDefault("Heat").Float(),
                Is.EqualTo(14 * damagePerSecond + 5));
        });
    }

    [Test]
    public async Task BatteriesFireEveryMissionAndTakeEightRepairStepsAfterEachShot()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var weapons = SProtoMan.EnumeratePrototypes<CMUShipWeaponPrototype>().Select(w => w.ID).ToList();
            foreach (var prototype in new[] { "CMUShipWeaponGun", "CMUShipWeaponGunFlipped" })
            {
                var gun = SEntMan.SpawnEntity(prototype, map.GridCoords);
                var component = SComp<CMUShipWeaponGunComponent>(gun);
                Assert.That(component.Weapons.Select(w => w.Id), Is.EquivalentTo(weapons), $"{prototype} fires every mission");
                Assert.That(component.Ready, Is.True, "a fresh battery is ready to fire");
                Assert.That(component.RepairSteps, Has.Count.EqualTo(8));
                SEntMan.DeleteEntity(gun);
            }
        });
    }

    [Test]
    public async Task RequiredBatteriesAreShipLocalAndBurnOutOnAcceptedLaunches()
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
                var foreignGun = SEntMan.SpawnEntity("CMUShipWeaponGun", new EntityCoordinates(other, new Vector2(.5f)));
                var actor = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
                var terminal = Terminal(map.GridCoords);
                var console = Console(map.GridCoords);
                Send(terminal, actor, new CMUShipWeaponsArmBuiMsg("CMUGasStrike", "CMUSmokeStrike"));
                Assert.That(SEntMan.HasComponent<CMUShipWeaponsComponent>(map.Grid), Is.False,
                    "an identical battery on another ship cannot unlock arming");
                var gun = SEntMan.SpawnEntity("CMUShipWeaponGun", map.GridCoords);
                var gunComp = SComp<CMUShipWeaponGunComponent>(gun);
                var transform = SEntMan.System<SharedTransformSystem>();
                transform.Unanchor(gun);
                Send(terminal, actor, new CMUShipWeaponsArmBuiMsg("CMUGasStrike", "CMUSmokeStrike"));
                Assert.That(SEntMan.HasComponent<CMUShipWeaponsComponent>(map.Grid), Is.False,
                    "an uninstalled battery cannot unlock arming");
                Assert.That(transform.AnchorEntity(gun), Is.True);
                Send(terminal, actor, new CMUShipWeaponsArmBuiMsg("CMUGasStrike", "CMUSmokeStrike"));
                var state = SComp<CMUShipWeaponsComponent>(map.Grid);
                Assert.That(state.RemainingUses, Is.EqualTo(2));
                var ui = SEntMan.System<SharedUserInterfaceSystem>();
                Assert.That(ui.TryGetUiState<CMUShipWeaponsBuiState>(terminal, CMUShipWeaponsUiKey.Key, out var choices), Is.True);
                Assert.That(choices!.AvailableWeapons.Select(w => w.Id),
                    Is.EquivalentTo(SProtoMan.EnumeratePrototypes<CMUShipWeaponPrototype>().Select(p => p.ID)),
                    "an installed battery can fire every mission");

                var planet = SEntMan.EnsureComponent<RMCPlanetComponent>(map.MapUid);
                planet.Offset = Vector2i.Zero;
                var areaGrid = SEntMan.EnsureComponent<AreaGridComponent>(map.Grid);
                SEntMan.System<AreaSystem>().ReplaceArea(areaGrid, Vector2i.Zero, "RMCAreaDesertDamInterior");
                var area = SComp<AreaComponent>(areaGrid.AreaEntities["RMCAreaDesertDamInterior"]);
                area.OB = true;
                SComp<OverwatchConsoleComponent>(console).OrbitalCoordinates = Vector2i.Zero;
                Send(console, actor, new CMUShipWeaponSelectBuiMsg("CMUSmokeStrike", CMUShipWeaponBearing.North), true);

                gunComp.Ready = false;
                Send(console, actor, new OverwatchConsoleOrbitalLaunchBuiMsg(), true);
                Assert.That(state.RemainingUses, Is.EqualTo(2), "a burnt-out battery cannot fire, and one on another ship doesn't count");
                Assert.That(SComp<CMUShipWeaponGunComponent>(foreignGun).Ready, Is.True);

                gunComp.Ready = true;
                area.OB = false;
                Send(console, actor, new OverwatchConsoleOrbitalLaunchBuiMsg(), true);
                Assert.That(gunComp.Ready, Is.True, "invalid targets never burn out a battery");
                area.OB = true;
                Send(console, actor, new OverwatchConsoleOrbitalLaunchBuiMsg(), true);
                Assert.That(state.RemainingUses, Is.EqualTo(1));
                Assert.That(gunComp.Ready, Is.False, "firing burns out the battery");
                Assert.That(gunComp.RepairStep, Is.Zero);

                gunComp.Ready = true;
                Send(console, actor, new OverwatchConsoleOrbitalLaunchBuiMsg(), true);
                Assert.That(gunComp.Ready, Is.True, "cooldown failures never burn out a battery");
                SEntMan.DeleteEntity(gun);
                state.NextLaunch = TimeSpan.Zero;
                Send(console, actor, new OverwatchConsoleOrbitalLaunchBuiMsg(), true);
                Assert.That(state.RemainingUses, Is.EqualTo(1), "removing the gun prevents firing");
                config.SetCVar(CCVars.ShipWeaponsRequireGuns, false);
                Send(terminal, actor, new CMUShipWeaponsArmBuiMsg("CMU120mmHEBarrage", "CMU380mmHEBarrage"));
                Assert.That(state.Weapons.Contains("CMUSmokeStrike"), Is.True, "CVar toggles cannot change locked choices");
                Send(console, actor, new OverwatchConsoleOrbitalLaunchBuiMsg(), true);
                Assert.That(state.RemainingUses, Is.Zero, "CVar off restores gun-free firing");
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
    public async Task TechPurchasesAddAFireMissionToTheirTeamsShip()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var terminal = SEntMan.SpawnEntity("CMUShipWeaponsTerminalGovfor", map.GridCoords);
            SEntMan.EventBus.RaiseEvent(EventSource.Local,
                new CMUTechShipWeaponMissionEvent("CMURailcannonStrike") { Team = "opfor" });
            Assert.That(SEntMan.HasComponent<CMUShipWeaponsComponent>(map.Grid), Is.False, "another team's purchase");
            SEntMan.EventBus.RaiseEvent(EventSource.Local,
                new CMUTechShipWeaponMissionEvent("CMURailcannonStrike") { Team = "govfor" });
            var state = SComp<CMUShipWeaponsComponent>(map.Grid);
            Assert.That(state.Weapons.Select(w => w.Id), Does.Contain("CMURailcannonStrike"));
            Assert.That(state.RemainingUses, Is.EqualTo(1));
            Assert.That(state.Locked, Is.False, "a purchase doesn't lock the terminal's own picks");
            SEntMan.DeleteEntity(terminal);
        });
    }

    [Test]
    public async Task RailcannonHitsEveryNetworkLevelButNotOtherNetworksOrProtectedTiles()
    {
        var cleanup = new List<EntityUid>();
        var victims = new List<EntityUid>();
        var outside = new List<EntityUid>();
        EntityCoordinates strikeCoordinates = default;
        try
        {
            await Server.WaitAssertion(() =>
            {
                var maps = SEntMan.System<SharedMapSystem>();
                var zLevels = SEntMan.System<CMUZLevelsSystem>();
                var network = zLevels.CreateZNetwork();
                var unrelated = zLevels.CreateZNetwork();
                cleanup.Add(network);
                cleanup.Add(unrelated);
                var members = new Dictionary<EntityUid, int>();
                var tileId = Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId;
                for (var i = 0; i < 4; i++)
                {
                    var map = maps.CreateMap(out var mapId);
                    cleanup.Add(map);
                    var grid = maps.CreateGridEntity(mapId);
                    var areaGrid = SEntMan.EnsureComponent<AreaGridComponent>(grid);
                    for (var x = 0; x <= 3; x++)
                    {
                        var tile = new Vector2i(x, 0);
                        maps.SetTile(grid, tile, new Tile(tileId));
                        SEntMan.System<AreaSystem>().ReplaceArea(areaGrid, tile, "RMCAreaDesertDamInterior");
                    }
                    SComp<AreaComponent>(areaGrid.AreaEntities["RMCAreaDesertDamInterior"]).OB = true;
                    var center = new EntityCoordinates(grid, new Vector2(.5f));
                    var victim = SEntMan.SpawnEntity(null, center);
                    SEntMan.EnsureComponent<DamageableComponent>(victim);
                    SEntMan.EnsureComponent<InjurableComponent>(victim);
                    var edge = SEntMan.SpawnEntity(null, new EntityCoordinates(grid, new Vector2(3.5f, .5f)));
                    SEntMan.EnsureComponent<DamageableComponent>(edge);
                    SEntMan.EnsureComponent<InjurableComponent>(edge);
                    victims.Add(victim);
                    outside.Add(edge);
                    if (i < 3)
                        members.Add(map, i - 1);
                    else
                        Assert.That(zLevels.TryAddMapsIntoZNetwork(unrelated, new() { [map] = 0 }), Is.True);
                    if (i == 1)
                        strikeCoordinates = center;
                }
                Assert.That(zLevels.TryAddMapsIntoZNetwork(network, members), Is.True);
            });
            await Pair.RunTicksSync(2);
            await Server.WaitAssertion(() =>
            {
                var strike = SEntMan.SpawnEntity("CMUShipWeaponStrike", strikeCoordinates);
                SEntMan.EnsureComponent<CMUShipWeaponStrikeComponent>(strike).Weapon = "CMURailcannonStrike";
                SEntMan.System<CMUShipWeaponsSystem>().Update(0);
                for (var i = 0; i < victims.Count; i++)
                {
                    Assert.That(SEntMan.System<DamageableSystem>()
                        .GetTotalDamage((victims[i], SComp<DamageableComponent>(victims[i]))).Float(),
                        Is.EqualTo(i < 3 ? 4000 : 0), $"level/network {i}");
                    Assert.That(SEntMan.System<DamageableSystem>()
                        .GetTotalDamage((outside[i], SComp<DamageableComponent>(outside[i]))).Float(),
                        Is.Zero, $"no 5x5 spill on level {i}");
                }

                // A second mission must retain each level's area protection.
                var protectedGrid = SComp<TransformComponent>(victims[0]).GridUid!.Value;
                var protectedAreas = SComp<AreaGridComponent>(protectedGrid);
                SComp<AreaComponent>(protectedAreas.AreaEntities["RMCAreaDesertDamInterior"]).OB = false;
                strike = SEntMan.SpawnEntity("CMUShipWeaponStrike", strikeCoordinates);
                SEntMan.EnsureComponent<CMUShipWeaponStrikeComponent>(strike).Weapon = "CMURailcannonStrike";
                SEntMan.System<CMUShipWeaponsSystem>().Update(0);
                Assert.That(SEntMan.System<DamageableSystem>()
                    .GetTotalDamage((victims[0], SComp<DamageableComponent>(victims[0]))).Float(), Is.EqualTo(4000));
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                foreach (var uid in cleanup)
                {
                    if (!SEntMan.Deleted(uid))
                        SEntMan.DeleteEntity(uid);
                }
            });
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
        float initialBurnOnExit = 0;
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
            deadline = ems!.EndsAt + TimeSpan.FromSeconds(35);
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
            initialBurnOnExit = SComp<DamageableComponent>(initial).Damage.DamageDict.GetValueOrDefault("Heat").Float();
            Assert.That(initialBurnOnExit, Is.GreaterThan(0));
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
            Assert.That(SComp<DamageableComponent>(initial).Damage.DamageDict.GetValueOrDefault("Heat").Float(),
                Is.EqualTo(initialBurnOnExit), "leaving the field stops burns without removing its stun");
            Assert.That(SComp<DamageableComponent>(late).Damage.DamageDict.GetValueOrDefault("Heat").Float(),
                Is.GreaterThan(0), "late entrants also take burns");
            Assert.That(SComp<DamageableComponent>(outside).Damage.DamageDict.GetValueOrDefault("Heat").Float(),
                Is.Zero);
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
*/
