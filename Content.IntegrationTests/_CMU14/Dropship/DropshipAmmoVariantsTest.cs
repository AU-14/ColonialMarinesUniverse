using System.Linq;
using System.Numerics;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Dropship.AttachmentPoint;
using Content.Shared._RMC14.Dropship.Fabricator;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared.Damage.Systems;
using Robust.Client.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

#pragma warning disable RA0002 // Arrange impact timers and inspect ammunition payloads.

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class DropshipAmmoVariantsTest
{

    [Test]
    public async Task BoforsApBlastDamagesAnAdjacentTile()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        EntityUid victim = default;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var maps = entities.System<SharedMapSystem>();
            for (var x = -5; x <= 5; x++)
            {
                for (var y = -5; y <= 5; y++)
                    maps.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);
            }
            victim = entities.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1.5f, 0.5f)));
            var victimCoordinates = entities.System<SharedTransformSystem>().GetMapCoordinates(victim);
            var epicenter = entities.System<SharedTransformSystem>()
                .ToMapCoordinates(map.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            Assert.That(victimCoordinates.MapId, Is.EqualTo(epicenter.MapId));
            Assert.That(maps.CoordinatesToTile(map.Grid, map.Grid.Comp, victimCoordinates), Is.EqualTo(new Vector2i(1, 0)));
            Assert.That(maps.CoordinatesToTile(map.Grid, map.Grid.Comp, epicenter), Is.EqualTo(new Vector2i(0, 0)));
            Assert.That(Vector2.Distance(victimCoordinates.Position, epicenter.Position), Is.EqualTo(1f).Within(0.001f));
        });
        await pair.RunTicksSync(2);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var config = pair.Server.ResolveDependency<IConfigurationManager>();
            config.SetCVar(RMCCVars.RMCDropshipCASDebug, true);
            var magazine = entities.SpawnEntity("CMUDropshipAmmoBOFORSMagAP", map.GridCoords);
            var ammo = entities.GetComponent<DropshipAmmoComponent>(magazine);
            var flight = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            entities.AddComponent(flight, new AmmoInFlightComponent
            {
                Target = map.GridCoords.Offset(new Vector2(0.5f, 0.5f)),
                MarkerAt = TimeSpan.Zero,
                MarkerDuration = TimeSpan.Zero,
                ShotsLeft = 1,
                ShotsPerVolley = 1,
                BulletSpread = 0,
                Explosion = ammo.Explosion,
                Damage = ammo.Damage,
            });
            entities.System<SharedDropshipWeaponSystem>().Update(0);
        });
        await pair.RunSeconds(2);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Assert.That(entities.System<DamageableSystem>().GetAllDamage(victim).GetTotal().Float(),
                Is.GreaterThan(0), "A genuine AP detonation must damage beyond the GAU's 0.49-tile impact radius.");
            pair.Server.ResolveDependency<IConfigurationManager>().SetCVar(RMCCVars.RMCDropshipCASDebug, false);
            entities.DeleteEntity(map.MapUid);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RapidBatteryFiresFiveThreeLaserSpreadsThenRunsOut()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var maps = entities.System<SharedMapSystem>();
            var map = maps.CreateMap(out var mapId);
            var grid = maps.CreateGridEntity(mapId);
            var ship = grid.Owner;
            var tileDefinitions = pair.Server.ResolveDependency<ITileDefinitionManager>();
            maps.SetTile(grid, Vector2i.Zero, new Tile(tileDefinitions["FloorSteel"].TileId));
            entities.AddComponent<DropshipComponent>(ship);
            var coords = new EntityCoordinates(ship, Vector2.Zero);
            var point = entities.SpawnEntity("CMAlamoWall61", coords);
            var mount = entities.GetComponent<DropshipWeaponPointComponent>(point);
            var containers = entities.System<SharedContainerSystem>();
            var weapon = entities.SpawnEntity("RMCDropshipAttachmentLaserCannon", coords);
            var battery = entities.SpawnEntity("RMCDropshipAttachmentAmmoLaserCannonRapid", coords);
            Assert.That(containers.Insert(weapon,
                containers.EnsureContainer<ContainerSlot>(point, mount.WeaponContainerSlotId)), Is.True);
            Assert.That(containers.Insert(battery,
                containers.EnsureContainer<ContainerSlot>(point, mount.AmmoContainerSlotId)), Is.True);
            var ammo = entities.GetComponent<DropshipAmmoComponent>(battery);
            Assert.That(ammo.Rounds, Is.EqualTo(100));
            var config = pair.Server.ResolveDependency<IConfigurationManager>();
            config.SetCVar(RMCCVars.RMCDropshipCASDebug, true);
            var weapons = entities.System<SharedDropshipWeaponSystem>();
            var target = new EntityCoordinates(map, 30, 30);
            for (var i = 0; i < 5; i++)
            {
                Assert.That(weapons.TryFireWeapon(weapon, target, DropshipWeaponStrikeType.Direct), Is.True);
                Assert.That(ammo.Rounds, Is.EqualTo(100 - (i + 1) * 20));
            }
            Assert.That(weapons.TryFireWeapon(weapon, target, DropshipWeaponStrikeType.Direct), Is.False);
            var flights = entities.QueryEntities<AmmoInFlightComponent>().ToArray();
            Assert.That(flights, Has.Length.EqualTo(5));
            foreach (var flight in flights)
            {
                Assert.That(flight.Comp.ImpactsPerVolley, Is.EqualTo(3));
                Assert.That(flight.Comp.ShotsPerVolley, Is.EqualTo(flight.Comp.ShotsLeft));
                flight.Comp.MarkerAt = TimeSpan.Zero;
                flight.Comp.MarkerDuration = TimeSpan.Zero;
            }
            weapons.Update(0);
            var pulses = entities.QueryEntities<MetaDataComponent>()
                .Where(e => e.Comp.EntityPrototype?.ID == "CMUDropshipLaserPulse").ToArray();
            Assert.That(pulses, Has.Length.EqualTo(15), "Five shots must produce fifteen actual laser impact effects.");
            var transform = entities.System<SharedTransformSystem>();
            foreach (var pulse in pulses)
            {
                var position = transform.GetMapCoordinates(pulse).Position;
                Assert.That(Vector2.Distance(position, new Vector2(30, 30)), Is.InRange(5f, 7f),
                    "Actual dispersed hits must remain widely spread after tile snapping.");
            }
            foreach (var flight in flights)
                Assert.That(flight.Comp.ShotsLeft, Is.EqualTo(0), "A three-laser shot must complete in one volley.");
            config.SetCVar(RMCCVars.RMCDropshipCASDebug, false);
            entities.DeleteEntity(map);
        });
        await pair.CleanReturnAsync();
    }
}
