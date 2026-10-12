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
    public void FiveImpactSpreadHasFiveDistinctWidelySeparatedLocations()
    {
        var offsets = Enumerable.Range(0, 5)
            .Select(i => SharedDropshipWeaponSystem.GetVolleyImpactOffset(i, 5, 6, Angle.FromDegrees(27)))
            .ToArray();
        Assert.That(offsets.Distinct().Count(), Is.EqualTo(5));
        foreach (var offset in offsets)
            Assert.That(offset.Length(), Is.EqualTo(6).Within(0.001));
        for (var i = 0; i < offsets.Length; i++)
        {
            for (var j = i + 1; j < offsets.Length; j++)
                Assert.That(Vector2.Distance(offsets[i], offsets[j]), Is.GreaterThan(7));
        }
        Assert.That(SharedDropshipWeaponSystem.GetVolleyImpactOffset(0, 1, 6, Angle.Zero),
            Is.EqualTo(Vector2.Zero));
    }

    [Test]
    public async Task GauVariantsAreDistinctPrintablePayloadsAndAllBoforsShellsExplode()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var map = entities.System<SharedMapSystem>().CreateMap();
            var coords = new EntityCoordinates(map, Vector2.Zero);
            var printables = entities.System<DropshipFabricatorSystem>().Printables;
            var types = new HashSet<string>();
            foreach (var id in new[]
                     {
                         "CMUDropshipAmmoGAUFragmentation",
                         "CMUDropshipAmmoGAUArmorPiercing",
                         "CMUDropshipAmmoGAUIncendiary",
                     })
            {
                var uid = entities.SpawnEntity(id, coords);
                var ammo = entities.GetComponent<DropshipAmmoComponent>(uid);
                Assert.That(ammo.Weapon.Id, Is.EqualTo("RMCDropshipAttachmentGau21Cannon"));
                Assert.That(ammo.Rounds / ammo.RoundsPerShot, Is.EqualTo(10));
                Assert.That(types.Add(ammo.AmmoType!), Is.True, "Different payloads must not be combined by refilling.");
                Assert.That(ammo.AmmoSpriteType, Is.Not.Null);
                Assert.That(printables.Any(p => p.Id == id), Is.True);
                if (id.EndsWith("Fragmentation"))
                {
                    Assert.That(ammo.Explosion, Is.Not.Null);
                    Assert.That(ammo.BulletSpread, Is.GreaterThan(3));
                    Assert.That(ammo.ArmorPiercing, Is.LessThan(10));
                }
                else if (id.EndsWith("ArmorPiercing"))
                {
                    Assert.That(ammo.Explosion, Is.Null);
                    Assert.That(ammo.ArmorPiercing, Is.EqualTo(80));
                    Assert.That(ammo.ZLevelPenetration, Is.EqualTo(3));
                    Assert.That(ammo.BulletSpread, Is.EqualTo(1));
                }
                else
                    Assert.That(ammo.Fire, Is.Not.Null);
            }

            foreach (var suffix in new[] { "HE", "AP", "Airburst", "HEAT", "Incendiary" })
            {
                var uid = entities.SpawnEntity("CMUDropshipAmmoBOFORSMag" + suffix, coords);
                var ammo = entities.GetComponent<DropshipAmmoComponent>(uid);
                Assert.That(ammo.Explosion, Is.Not.Null, suffix);
                Assert.That(ammo.Explosion!.Total, Is.GreaterThan(0), suffix);
                Assert.That(ammo.Damage, Is.Null, "40mm shells must use explosion damage, not GAU-style tile hits.");
                Assert.That(ammo.SoundImpact, Is.Null, "The explosion system supplies the detonation sound.");
                Assert.That(ammo.SoundGround, Is.Null, "BOFORS must not replay a GAU flyby.");
                Assert.That(ammo.Rounds, Is.EqualTo(20));
                Assert.That(ammo.RoundsPerShot, Is.EqualTo(5));
            }
            entities.DeleteEntity(map);
        });
        await pair.CleanReturnAsync();
    }

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
            victim = entities.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
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
                Target = map.GridCoords,
                MarkerAt = TimeSpan.Zero,
                MarkerDuration = TimeSpan.Zero,
                ShotsLeft = 1,
                ShotsPerVolley = 1,
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
    public async Task NewAmmoUsesValidDepletionArtwork()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var ammunition = new List<NetEntity>();
        EntityUid map = default;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            map = entities.System<SharedMapSystem>().CreateMap();
            foreach (var id in new[]
                     {
                         "CMUDropshipAmmoGAUFragmentation",
                         "CMUDropshipAmmoGAUArmorPiercing",
                         "CMUDropshipAmmoGAUIncendiary",
                         "RMCDropshipAttachmentAmmoLaserCannonRapid",
                     })
            {
                ammunition.Add(entities.GetNetEntity(entities.SpawnEntity(id,
                    new EntityCoordinates(map, Vector2.Zero))));
            }
        });
        for (var shotsLeft = 10; shotsLeft >= 0; shotsLeft--)
        {
            await pair.Server.WaitAssertion(() =>
            {
                var entities = pair.Server.EntMan;
                foreach (var net in ammunition)
                {
                    var uid = entities.GetEntity(net);
                    var ammo = entities.GetComponent<DropshipAmmoComponent>(uid);
                    ammo.Rounds = ammo.RoundsPerShot * shotsLeft;
                    entities.Dirty(uid, ammo);
                    entities.System<SharedAppearanceSystem>().SetData(uid, DropshipAmmoVisuals.Fill, ammo.Rounds);
                }
            });
            await pair.RunUntilSynced();
            await pair.Client.WaitAssertion(() =>
            {
                var entities = pair.Client.EntMan;
                entities.System<AppearanceSystem>().FrameUpdate(0);
                foreach (var net in ammunition)
                {
                    var uid = entities.GetEntity(net);
                    var ammo = entities.GetComponent<DropshipAmmoComponent>(uid);
                    var layer = entities.GetComponent<SpriteComponent>(uid)[DropshipAmmoVisuals.Fill];
                    Assert.That(layer.RsiState.ToString(), Is.EqualTo(ammo.AmmoSpriteType + "_" + shotsLeft));
                    Assert.That(layer.ActualRsi!.TryGetState(layer.RsiState, out _), Is.True);
                }
            });
        }
        await pair.Server.WaitAssertion(() => pair.Server.EntMan.DeleteEntity(map));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RapidBatteryFiresTenFiveLaserSpreadsThenRunsOut()
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
            for (var i = 0; i < 10; i++)
            {
                Assert.That(weapons.TryFireWeapon(weapon, target, DropshipWeaponStrikeType.Direct), Is.True);
                Assert.That(ammo.Rounds, Is.EqualTo(100 - (i + 1) * 10));
            }
            Assert.That(weapons.TryFireWeapon(weapon, target, DropshipWeaponStrikeType.Direct), Is.False);
            var flights = entities.QueryEntities<AmmoInFlightComponent>().ToArray();
            Assert.That(flights, Has.Length.EqualTo(10));
            foreach (var flight in flights)
            {
                Assert.That(flight.Comp.ImpactsPerVolley, Is.EqualTo(5));
                Assert.That(flight.Comp.ShotsPerVolley, Is.EqualTo(flight.Comp.ShotsLeft));
                flight.Comp.MarkerAt = TimeSpan.Zero;
                flight.Comp.MarkerDuration = TimeSpan.Zero;
            }
            weapons.Update(0);
            var pulses = entities.QueryEntities<MetaDataComponent>()
                .Where(e => e.Comp.EntityPrototype?.ID == "CMUDropshipLaserPulse").ToArray();
            Assert.That(pulses, Has.Length.EqualTo(50), "Ten shots must produce fifty actual laser impact effects.");
            var transform = entities.System<SharedTransformSystem>();
            foreach (var pulse in pulses)
            {
                var position = transform.GetMapCoordinates(pulse).Position;
                Assert.That(Vector2.Distance(position, new Vector2(30, 30)), Is.InRange(5f, 7f),
                    "Actual dispersed hits must remain widely spread after tile snapping.");
            }
            foreach (var flight in flights)
                Assert.That(flight.Comp.ShotsLeft, Is.EqualTo(0), "A five-laser shot must complete in one volley.");
            config.SetCVar(RMCCVars.RMCDropshipCASDebug, false);
            entities.DeleteEntity(map);
        });
        await pair.CleanReturnAsync();
    }
}
