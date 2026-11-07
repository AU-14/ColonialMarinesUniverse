using System.Linq;
using System.Numerics;
using System.Reflection;
using Content.Client._RMC14.Dropship.Weapon;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Dropship.AttachmentPoint;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.PowerLoader;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using TestPair = Content.IntegrationTests.Pair.TestPair;

#pragma warning disable RA0002 // Arrange mounted payloads and inspect captured flights.

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class DropshipWeaponFeaturesTest
{

    [TestCase(DropshipLaserPattern.Ring)]
    [TestCase(DropshipLaserPattern.Line)]
    public async Task LaserPatternSettingControlsActualThreeImpactPositions(DropshipLaserPattern pattern)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var (map, ship, point) = CreateMount(pair);
            var coords = new EntityCoordinates(ship, Vector2.Zero);
            var weapon = Mount(entities, point, "RMCDropshipAttachmentLaserCannon", false, coords);
            var battery = Mount(entities, point, "RMCDropshipAttachmentAmmoLaserCannonRapid", true, coords);
            var console = entities.SpawnEntity("CMUMohawkMidwayGunneryea0f8d621b", coords);
            var terminal = entities.GetComponent<DropshipTerminalWeaponsComponent>(console);
            terminal.ScreenOne.Weapon = entities.GetNetEntity(weapon);
            var actor = entities.SpawnEntity("CMMobHuman", coords);
            entities.System<SkillsSystem>().SetSkill(actor, "RMCSkillPilot", 2);
            entities.EventBus.RaiseLocalEvent(console, new DropshipWeaponSettingsMessage(true, pattern, DropshipAirburstHeight.High)
                { Actor = actor, UiKey = DropshipTerminalWeaponsUi.Key });
            var component = entities.GetComponent<DropshipWeaponComponent>(weapon);
            Assert.That(component.LaserPattern, Is.EqualTo(pattern));
            Assert.That(component.AirburstHeight, Is.EqualTo(DropshipAirburstHeight.Medium),
                "Laser settings must not change unrelated airburst settings.");
            var config = pair.Server.ResolveDependency<IConfigurationManager>();
            config.SetCVar(RMCCVars.RMCDropshipCASDebug, true);
            var system = entities.System<SharedDropshipWeaponSystem>();
            Assert.That(system.TryFireWeapon(weapon, new EntityCoordinates(map, 30, 30), DropshipWeaponStrikeType.Direct), Is.True);
            Assert.That(entities.GetComponent<DropshipAmmoComponent>(battery).Rounds, Is.EqualTo(80));
            var flight = entities.QueryEntities<AmmoInFlightComponent>().Single();
            Assert.That(flight.Comp.LaserPattern, Is.EqualTo(pattern));
            flight.Comp.MarkerAt = TimeSpan.Zero;
            flight.Comp.MarkerDuration = TimeSpan.Zero;
            system.Update(0);
            var transform = entities.System<SharedTransformSystem>();
            var positions = entities.QueryEntities<MetaDataComponent>()
                .Where(e => e.Comp.EntityPrototype?.ID == "CMUDropshipLaserPulse")
                .Select(e => transform.GetMapCoordinates(e.Owner).Position).ToArray();
            Assert.That(positions, Has.Length.EqualTo(3));
            Assert.That(positions.Distinct().Count(), Is.EqualTo(3));
            var expected = Enumerable.Range(0, 3).Select(i => new Vector2(30, 30) +
                SharedDropshipWeaponSystem.GetLaserImpactOffset(i, 3, 6, Angle.Zero, pattern)).ToArray();
            foreach (var location in expected)
                Assert.That(positions.Any(p => Vector2.Distance(p, location) < 1), Is.True,
                    $"Actual tile-snapped impact must match shared preview location {location}.");
            if (pattern == DropshipLaserPattern.Line)
                Assert.That(positions.Select(p => p.Y).Distinct().Count(), Is.EqualTo(1));
            config.SetCVar(RMCCVars.RMCDropshipCASDebug, false);
            entities.DeleteEntity(map);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AirburstHeightChangesRealCoverageWithoutChangingExplosiveEnergy()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var maps = entities.System<SharedMapSystem>();
            for (var x = -10; x <= 10; x++)
            {
                for (var y = -10; y <= 10; y++)
                    maps.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);
            }
            var magazine = entities.SpawnEntity("CMUDropshipAmmoBOFORSMagAirburst", map.GridCoords);
            var ammo = entities.GetComponent<DropshipAmmoComponent>(magazine);
            var tiles = new List<int>();
            var peaks = new List<float>();
            var method = typeof(ExplosionSystem).GetMethod("GetExplosionTiles", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var epicenter = entities.System<SharedTransformSystem>().ToMapCoordinates(map.GridCoords);
            foreach (var height in new[] { DropshipAirburstHeight.Low, DropshipAirburstHeight.Medium, DropshipAirburstHeight.High })
            {
                var payload = SharedDropshipWeaponSystem.GetAirburstExplosion(ammo, height)!;
                Assert.That(payload.Total, Is.EqualTo(40));
                var result = ((int Area, List<float> Intensities, ExplosionSpaceTileFlood? Space,
                    Dictionary<EntityUid, ExplosionGridTileFlood> Grids, Matrix3x2 Matrix))
                    method.Invoke(entities.System<ExplosionSystem>(),
                        new object[] { epicenter, "RMC", payload.Total, payload.Slope, payload.Max })!;
                tiles.Add(result.Grids[map.Grid].TileLists.Sum(row => row.Value.Count));
                peaks.Add(result.Intensities.Max());
                Assert.That(SharedDropshipWeaponSystem.EstimateBlastRadius(payload),
                    Is.EqualTo(0.5f + entities.System<ExplosionSystem>().IntensityToRadius(payload.Total, payload.Slope, payload.Max)));
            }
            Assert.That(tiles[1], Is.GreaterThan(tiles[0]), "Medium bursts must cover more real tiles than low bursts.");
            Assert.That(tiles[2], Is.GreaterThan(tiles[1]), "High bursts must cover more real tiles than medium bursts.");
            Assert.That(peaks[0], Is.GreaterThan(peaks[1]));
            Assert.That(peaks[1], Is.GreaterThan(peaks[2]));
            Assert.That(ammo.Explosion!.Slope, Is.EqualTo(8), "Settings must not mutate shared ammunition payloads.");
            entities.DeleteEntity(map.MapUid);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(DropshipAirburstHeight.Low)]
    public async Task AirburstConsoleSettingIsCapturedAtRelease(DropshipAirburstHeight height)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var (map, ship, point) = CreateMount(pair);
            var coordinates = new EntityCoordinates(ship, Vector2.Zero);
            var weapon = Mount(entities, point, "CMUDropshipAttachmentBOFORS", false, coordinates);
            var magazine = Mount(entities, point, "CMUDropshipAmmoBOFORSMagAirburst", true, coordinates);
            var console = entities.SpawnEntity("CMUMohawkMidwayGunneryea0f8d621b", coordinates);
            var terminal = entities.GetComponent<DropshipTerminalWeaponsComponent>(console);
            terminal.ScreenTwo.Weapon = entities.GetNetEntity(weapon);
            var actor = entities.SpawnEntity("CMMobHuman", coordinates);
            entities.System<SkillsSystem>().SetSkill(actor, "RMCSkillPilot", 1);
            entities.EventBus.RaiseLocalEvent(console, new DropshipWeaponSettingsMessage(false, DropshipLaserPattern.Line,
                DropshipAirburstHeight.Low) { Actor = actor, UiKey = DropshipTerminalWeaponsUi.Key });
            Assert.That(entities.GetComponent<DropshipWeaponComponent>(weapon).AirburstHeight,
                Is.EqualTo(DropshipAirburstHeight.Medium), "BOFORS configuration requires its existing pilot skill level.");
            entities.System<SkillsSystem>().SetSkill(actor, "RMCSkillPilot", 2);
            entities.EventBus.RaiseLocalEvent(console, new DropshipWeaponSettingsMessage(false, DropshipLaserPattern.Line, height)
                { Actor = actor, UiKey = DropshipTerminalWeaponsUi.Key });
            var component = entities.GetComponent<DropshipWeaponComponent>(weapon);
            Assert.That(component.AirburstHeight, Is.EqualTo(height));
            Assert.That(component.LaserPattern, Is.EqualTo(DropshipLaserPattern.Ring));
            var ammo = entities.GetComponent<DropshipAmmoComponent>(magazine);
            var expected = SharedDropshipWeaponSystem.GetAirburstExplosion(ammo, height)!;
            var config = pair.Server.ResolveDependency<IConfigurationManager>();
            config.SetCVar(RMCCVars.RMCDropshipCASDebug, true);
            Assert.That(entities.System<SharedDropshipWeaponSystem>().TryFireWeapon(weapon,
                new EntityCoordinates(map, 30, 30), DropshipWeaponStrikeType.Direct), Is.True);
            Assert.That(ammo.Rounds, Is.EqualTo(15));
            var flight = entities.QueryEntities<AmmoInFlightComponent>().Single();
            Assert.That(flight.Comp.Explosion!.Slope, Is.EqualTo(expected.Slope));
            Assert.That(flight.Comp.Explosion.Max, Is.EqualTo(expected.Max));
            entities.EventBus.RaiseLocalEvent(console, new DropshipWeaponSettingsMessage(false,
                DropshipLaserPattern.Ring, DropshipAirburstHeight.High)
                { Actor = actor, UiKey = DropshipTerminalWeaponsUi.Key });
            Assert.That(flight.Comp.Explosion.Slope, Is.EqualTo(expected.Slope), "Settings only affect future shots.");
            Assert.That(ammo.Explosion!.Slope, Is.EqualTo(8));
            var outsiderMap = entities.System<SharedMapSystem>().CreateMap();
            var outsider = entities.SpawnEntity("CMUMohawkMidwayGunneryea0f8d621b", new EntityCoordinates(outsiderMap, Vector2.Zero));
            entities.GetComponent<DropshipTerminalWeaponsComponent>(outsider).ScreenOne.Weapon = entities.GetNetEntity(weapon);
            entities.EventBus.RaiseLocalEvent(outsider, new DropshipWeaponSettingsMessage(true,
                DropshipLaserPattern.Ring, DropshipAirburstHeight.Low)
                { Actor = actor, UiKey = DropshipTerminalWeaponsUi.Key });
            Assert.That(component.AirburstHeight, Is.EqualTo(DropshipAirburstHeight.High),
                "A console cannot configure a weapon on another ship.");
            config.SetCVar(RMCCVars.RMCDropshipCASDebug, false);
            entities.DeleteEntity(map);
            entities.DeleteEntity(outsiderMap);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MixedBeltAlternatesEveryRoundAndUsesPartialCrates()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var (map, ship, point) = CreateMount(pair);
            var coords = new EntityCoordinates(ship, Vector2.Zero);
            var weapon = Mount(entities, point, "RMCDropshipAttachmentGau21Cannon", false, coords);
            var belt = entities.SpawnEntity("CMUDropshipMixedGauBelt", coords);
            var loader = entities.SpawnEntity("RMCMechPowerLoader", coords);
            var crates = new[]
            {
                entities.SpawnEntity("CMUDropshipAmmoGAUFragmentation", coords),
                entities.SpawnEntity("CMUDropshipAmmoGAUArmorPiercing", coords),
                entities.SpawnEntity("CMUDropshipAmmoGAUIncendiary", coords),
            };
            foreach (var crate in crates)
            {
                var args = new PowerLoaderInteractEvent(loader, belt, crate, []);
                entities.EventBus.RaiseLocalEvent(crate, ref args);
                Assert.That(args.Handled, Is.True);
            }
            var containers = entities.System<SharedContainerSystem>();
            var mount = entities.GetComponent<DropshipWeaponPointComponent>(point);
            Assert.That(containers.Insert(belt, containers.GetContainer(point, mount.AmmoContainerSlotId)), Is.True);
            var system = entities.System<SharedDropshipWeaponSystem>();
            Assert.That(entities.GetComponent<DropshipAmmoComponent>(belt).Rounds, Is.EqualTo(800));
            var config = pair.Server.ResolveDependency<IConfigurationManager>();
            config.SetCVar(RMCCVars.RMCDropshipCASDebug, true);
            for (var shot = 0; shot < 3; shot++)
            {
                Assert.That(system.TryGetWeaponAmmo(weapon, out var next), Is.True);
                Assert.That(next.Owner, Is.EqualTo(crates[0]));
                Assert.That(system.GetAvailableBursts(next), Is.EqualTo(20 - shot));
                Assert.That(system.TryFireWeapon(weapon, new EntityCoordinates(map, 20, 20), DropshipWeaponStrikeType.Direct), Is.True);
                Assert.That(next.Comp.Rounds, Is.EqualTo(400 - 20 * (shot + 1)));
                if (shot == 0)
                {
                    var sequence = entities.QueryEntities<AmmoInFlightComponent>().OrderBy(f => f.Owner.Id).ToArray();
                    Assert.That(sequence, Has.Length.EqualTo(40));
                    Assert.That(sequence.All(f => f.Comp.ShotsLeft == 1), Is.True);
                    for (var round = 0; round < sequence.Length; round++)
                    {
                        Assert.That(sequence[round].Comp.Explosion != null, Is.EqualTo(round % 2 == 0));
                        Assert.That(sequence[round].Comp.ArmorPiercing == 60, Is.EqualTo(round % 2 == 1));
                        Assert.That(sequence[round].Comp.Fire, Is.Null, "The third crate must not enter the full belt.");
                    }
                }
            }
            var flights = entities.QueryEntities<AmmoInFlightComponent>().ToArray();
            Assert.That(flights, Has.Length.EqualTo(120));
            Assert.That(flights.Count(f => f.Comp.Explosion != null), Is.EqualTo(60));
            Assert.That(flights.Count(f => f.Comp.ArmorPiercing == 60), Is.EqualTo(60));
            Assert.That(flights.Count(f => f.Comp.Fire != null), Is.Zero);
            Assert.That(entities.GetComponent<DropshipAmmoComponent>(belt).Rounds, Is.EqualTo(680));
            Assert.That(system.GetWeaponRounds(weapon), Is.EqualTo(680));
            entities.GetComponent<DropshipAmmoComponent>(crates[0]).Rounds = 10;
            Assert.That(system.TryGetWeaponAmmo(weapon, out var available), Is.True);
            Assert.That(available.Owner, Is.EqualTo(crates[0]), "Partial crates participate round-by-round.");
            Assert.That(system.TryFireWeapon(weapon, new EntityCoordinates(map, 20, 20), DropshipWeaponStrikeType.FireMission), Is.True);
            Assert.That(entities.GetComponent<DropshipAmmoComponent>(crates[1]).Rounds, Is.EqualTo(310));
            Assert.That(entities.GetComponent<DropshipAmmoComponent>(crates[0]).Rounds, Is.Zero);
            Assert.That(system.TryGetWeaponAmmo(weapon, out available), Is.True);
            Assert.That(available.Owner, Is.EqualTo(crates[1]), "The exhausted first crate is skipped.");
            Assert.That(entities.GetComponent<DropshipAmmoComponent>(crates[2]).Rounds, Is.EqualTo(400),
                "A third crate must remain outside the full two-slot carrier.");
            var incorrect = entities.SpawnEntity("RMCDropshipAttachmentAmmoLaserCannon", coords);
            Assert.That(containers.Remove(crates[0], containers.GetContainer(belt, "belt1")), Is.True);
            Assert.That(containers.Insert(incorrect, containers.GetContainer(belt, "belt1")), Is.False);
            Assert.That(containers.Insert(belt, containers.GetContainer(belt, "belt1")), Is.False);
            config.SetCVar(RMCCVars.RMCDropshipCASDebug, false);
            entities.DeleteEntity(map);
        });
        await pair.CleanReturnAsync();
    }

    private static (EntityUid Map, EntityUid Ship, EntityUid Point) CreateMount(TestPair pair)
    {
        var entities = pair.Server.EntMan;
        var maps = entities.System<SharedMapSystem>();
        var map = maps.CreateMap(out var mapId);
        var grid = maps.CreateGridEntity(mapId);
        var definitions = pair.Server.ResolveDependency<ITileDefinitionManager>();
        maps.SetTile(grid, Vector2i.Zero, new Tile(definitions["FloorSteel"].TileId));
        var dropship = entities.AddComponent<DropshipComponent>(grid.Owner);
        var point = entities.SpawnEntity("CMAlamoWall61", new EntityCoordinates(grid.Owner, Vector2.Zero));
        dropship.AttachmentPoints.Add(point);
        var mount = entities.GetComponent<DropshipWeaponPointComponent>(point);
        var containers = entities.System<SharedContainerSystem>();
        containers.EnsureContainer<ContainerSlot>(point, mount.WeaponContainerSlotId);
        containers.EnsureContainer<ContainerSlot>(point, mount.AmmoContainerSlotId);
        return (map, grid.Owner, point);
    }

    private static EntityUid Mount(IEntityManager entities, EntityUid point, string prototype, bool ammo, EntityCoordinates coords)
    {
        var uid = entities.SpawnEntity(prototype, coords);
        var mount = entities.GetComponent<DropshipWeaponPointComponent>(point);
        var container = entities.System<SharedContainerSystem>();
        Assert.That(container.Insert(uid, container.GetContainer(point,
            ammo ? mount.AmmoContainerSlotId : mount.WeaponContainerSlotId)), Is.True);
        return uid;
    }
}
