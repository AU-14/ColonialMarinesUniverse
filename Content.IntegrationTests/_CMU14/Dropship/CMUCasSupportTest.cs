using System.Linq;
using System.Numerics;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Dropship.AttachmentPoint;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.Marines;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Dropship.Cas;
using Content.Shared.CMU14.Dropship.DirectFire;
using Content.Shared.Damage.Components;
using Content.Shared.Mobs.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

#pragma warning disable RA0002 // Arrange factions, heat and ammo at the system boundary.

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class CMUCasSupportTest
{
    [Test]
    public async Task RocketFalloutZonesAreBoundedAndThermobaricDamagesResin()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var maps = entities.System<SharedMapSystem>();
            maps.CreateMap(out var mapId);
            var grid = maps.CreateGridEntity(mapId);
            var tiles = pair.Server.ResolveDependency<ITileDefinitionManager>();
            for (var x = -6; x <= 6; x++)
            {
                for (var y = -6; y <= 6; y++)
                    maps.SetTile(grid, new Vector2i(x, y), new Tile(tiles["FloorSteel"].TileId));
            }

            var fallout = entities.System<CMUCasFalloutSystem>();
            var center = new EntityCoordinates(grid.Owner, new Vector2(0.5f, 0.5f));
            var centerMap = new MapCoordinates(new Vector2(0.5f, 0.5f), mapId);
            var far = new MapCoordinates(new Vector2(5.5f, 0.5f), mapId);
            Assert.That(fallout.IsWeedKilled(centerMap), Is.False);
            Assert.That(fallout.IsObscured(centerMap), Is.False);

            entities.SpawnEntity("CMUCasFalloutWeedKiller", center);
            Assert.That(fallout.IsWeedKilled(centerMap), Is.True, "Weedkiller must treat its impact area.");
            Assert.That(fallout.IsWeedKilled(far), Is.False, "Weedkiller treatment must stay within three tiles.");
            Assert.That(fallout.IsObscured(centerMap), Is.False, "Weedkiller must not obscure optics.");

            entities.SpawnEntity("CMUCasFalloutObscurant", center);
            Assert.That(fallout.IsObscured(centerMap), Is.True, "WP smoke must obscure its impact area.");
            Assert.That(fallout.IsObscured(far), Is.False);

            var wall = entities.SpawnEntity("WallXenoResin", new EntityCoordinates(grid.Owner, new Vector2(1.5f, 0.5f)));
            var distant = entities.SpawnEntity("WallXenoResin", new EntityCoordinates(grid.Owner, new Vector2(5.5f, 5.5f)));
            entities.SpawnEntity("CMUCasFalloutResin", center);
            Assert.That(entities.GetComponent<DamageableComponent>(wall).TotalDamage.Float(), Is.GreaterThan(0),
                "Thermobaric overpressure must damage nearby resin.");
            Assert.That(entities.GetComponent<DamageableComponent>(distant).TotalDamage.Float(), Is.EqualTo(0),
                "Thermobaric overpressure must not reach distant resin.");

            var flare = entities.SpawnEntity("CMUCasFalloutFlare", center);
            Assert.That(entities.HasComponent<DropshipTargetComponent>(flare), Is.True,
                "Flare rockets must leave a targetable CAS mark.");
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SpecialRocketsCarryTheirFallout()
    {
        await using var pair = await PoolManager.GetServerClient();
        var prototypes = pair.Server.ProtoMan;
        var factory = pair.Server.ResolveDependency<IComponentFactory>();
        await pair.Server.WaitAssertion(() =>
        {
            void Expect(string rocket, string effect)
            {
                Assert.That(prototypes.Index<EntityPrototype>(rocket)
                        .TryGetComponent(out DropshipAmmoComponent? ammo, factory), Is.True, rocket);
                Assert.That(ammo!.ImpactEffects.Select(id => id.Id), Does.Contain(effect), rocket);
            }

            Expect("RMCDropshipAttachmentAmmoRocketMiniMikeWeedKiller", "CMUCasFalloutWeedKiller");
            Expect("RMCDropshipAttachmentAmmoRocketMiniMikeSmoke", "CMUCasFalloutObscurant");
            Expect("RMCDropshipAttachmentAmmoRocketMiniMikeFlare", "CMUCasFalloutFlare");
            Expect("RMCDropshipAttachmentAmmoRocketMiniMikeThermobaric", "CMUCasFalloutResin");
            Expect("AU14DropshipAttachmentAmmoRocketThermobaricTwo", "CMUCasFalloutResin");
            Expect("RMCDropshipAttachmentAmmoRocketMiniMikeHEAT", "CMUCasFalloutArmor");
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HeapIsGroundCompatibleAndOverheatsFasterThanSquall()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var prototypes = pair.Server.ProtoMan;
        var factory = pair.Server.ResolveDependency<IComponentFactory>();
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(prototypes.Index<EntityPrototype>("CMUMohawkM90Ammo")
                .TryGetComponent(out DropshipAmmoComponent? heap, factory), Is.True);
            Assert.That(prototypes.Index<EntityPrototype>("CMUMohawkM90AmmoKinetic")
                .TryGetComponent(out DropshipAmmoComponent? squall, factory), Is.True);
            Assert.That(heap!.GroundCompatible, Is.True, "HEAP must fire on the ground.");
            Assert.That(heap.DirectFireHeat, Is.GreaterThan(squall!.DirectFireHeat * 5),
                "HEAP must heat the barrels much faster than Squall.");

            var entities = pair.Server.EntMan;
            var maps = entities.System<SharedMapSystem>();
            maps.CreateMap(out var mapId);
            var grid = maps.CreateGridEntity(mapId);
            var tiles = pair.Server.ResolveDependency<ITileDefinitionManager>();
            maps.SetTile(grid, Vector2i.Zero, new Tile(tiles["FloorSteel"].TileId));
            var point = entities.SpawnEntity("CMUMohawkM90Point", new EntityCoordinates(grid.Owner, Vector2.Zero));
            var chin = entities.GetComponent<MohawkChinGunComponent>(point);
            var turret = entities.System<MohawkChinGunSystem>();

            var shots = 0;
            while (!chin.Overheated && shots < 1000)
            {
                turret.AddHeat((point, chin), heap.DirectFireHeat);
                shots++;
            }

            Assert.That(chin.Overheated, Is.True);
            Assert.That(shots, Is.LessThanOrEqualTo(20), "Sustained HEAP fire must overheat quickly.");

            chin.Heat = 0;
            chin.Overheated = false;
            for (var i = 0; i < shots; i++)
                turret.AddHeat((point, chin), squall.DirectFireHeat);
            Assert.That(chin.Overheated, Is.False, "The same number of Squall rounds must not overheat the guns.");

            chin.Heat = chin.MaxHeat;
            chin.Overheated = true;
            turret.Update((chin.MaxHeat - chin.OverheatRecoveredAt) / chin.CoolPerSecond + 1);
            Assert.That(chin.Overheated, Is.False, "Cooling below the recovery threshold must clear the lockout.");
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task JamsFaultTheHardpointAndAdminToolsRecoverIt()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var config = pair.Server.ResolveDependency<IConfigurationManager>();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var maps = entities.System<SharedMapSystem>();
            maps.CreateMap(out var mapId);
            var grid = maps.CreateGridEntity(mapId);
            var tiles = pair.Server.ResolveDependency<ITileDefinitionManager>();
            maps.SetTile(grid, Vector2i.Zero, new Tile(tiles["FloorSteel"].TileId));
            var coordinates = new EntityCoordinates(grid.Owner, Vector2.Zero);
            var point = entities.SpawnEntity("CMUMohawkM90Point", coordinates);
            var boot = entities.System<DropshipFireControlBootSystem>();
            var maxChance = config.GetCVar(CCVars.DropshipFaultMaxChance);

            Assert.That(boot.TryJam(point), Is.False, "Tests run with jams disabled.");
            config.SetCVar(CCVars.DropshipJamChance, 1f);
            config.SetCVar(CCVars.DropshipFaultMaxChance, 1f);
            try
            {
                Assert.That(boot.TryJam(point), Is.True);
                var fault = entities.GetComponent<DropshipHardpointFaultComponent>(point);
                Assert.That(fault.Faulted, Is.True);
                Assert.That(fault.Reason, Is.Not.Null);
                Assert.That(boot.TryJam(point), Is.False, "A jammed hardpoint must not jam twice.");
                boot.ClearFault(point);
                Assert.That(fault.Faulted, Is.False);
                Assert.That(fault.Reason, Is.Null);
            }
            finally
            {
                config.SetCVar(CCVars.DropshipJamChance, 0f);
                config.SetCVar(CCVars.DropshipFaultMaxChance, maxChance);
            }

            var mount = entities.GetComponent<DropshipWeaponPointComponent>(point);
            var containers = entities.System<SharedContainerSystem>();
            var ammo = entities.SpawnEntity("CMUMohawkM90AmmoKinetic", coordinates);
            Assert.That(containers.Insert(ammo, containers.GetContainer(point, mount.AmmoContainerSlotId)), Is.True);
            var payload = entities.GetComponent<DropshipAmmoComponent>(ammo);
            payload.Rounds = 0;
            Assert.That(entities.System<SharedDropshipWeaponSystem>().AdminRefillAmmo(point), Is.GreaterThan(0));
            Assert.That(payload.Rounds, Is.EqualTo(payload.MaxRounds));

            var chin = entities.GetComponent<MohawkChinGunComponent>(point);
            chin.Calibrated = false;
            chin.Heat = chin.MaxHeat;
            chin.Overheated = true;
            entities.System<MohawkChinGunSystem>().AdminRestore((point, chin));
            Assert.That(chin.Calibrated, Is.True);
            Assert.That(chin.Overheated, Is.False);
            Assert.That(chin.Heat, Is.Zero);
        });
        await pair.CleanReturnAsync();
    }

}
