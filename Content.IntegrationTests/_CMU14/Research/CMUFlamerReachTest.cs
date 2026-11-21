#pragma warning disable RA0002 // Flamer chains and projectile physics are read directly to check the fired shape.

using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Weapons.Ranged.Flamer;
using Content.Shared.CMU14.Weapons;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.UnitTesting.Pool;
using ServerGunSystem = Content.Server.Weapons.Ranged.Systems.GunSystem;

namespace Content.IntegrationTests._CMU14.Research;

/// <summary>
/// FlameRebalance audit: FLOW-01/COUPLING-01 (flamer reach and cone width are per-tank, no longer tied to blue fire)
/// and FLOW-02 (the DRGN fans shards around a centred main jet). Fires the real weapons.
/// </summary>
[TestFixture]
public sealed class CMUFlamerReachTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    /// <summary>
    /// M240 tanks project their whole reach again (UT 5, B-gel 7) in the standard 3-wide cone. The civilian LC9 keeps
    /// 60% (UT 3). A specialist M240-T B tank and the vehicle flamer get the 5-wide cone without allowing blue fire
    /// being what decides it.
    /// </summary>
    [Test]
    public async Task FlamerReachAndConeComeFromTheTank()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            LayFloor(map);
            var flamer = SEntMan.System<SharedRMCFlamerSystem>();
            var containers = SEntMan.System<SharedContainerSystem>();
            var origin = map.GridCoords;
            var target = origin.Offset(new Vector2(-12, 0));

            (int Rows, int Width) Fire(string tankId)
            {
                DeleteChains();
                var tank = SEntMan.SpawnEntity(tankId, origin);
                var gun = SEntMan.SpawnEntity("RMCWeaponFlamerUnloaded", origin);
                var provider = SComp<RMCFlamerAmmoProviderComponent>(gun);
                Assert.That(containers.Insert(tank, containers.GetContainer(gun, provider.ContainerId)), Is.True,
                    $"{tankId} should load into the M240");
                flamer.ShootFlamer((gun, provider), (gun, SComp<GunComponent>(gun)), null, origin, target);
                return Shape(tankId);
            }

            var m240 = Fire("RMCTankFlamer");
            var m240BGel = Fire("RMCTankFlamerBGel");
            var lc9 = Fire("RMCTankFlamerColony");
            var specialistB = Fire("RMCTankFlamerLargeB");

            DeleteChains();
            var vehicle = SEntMan.SpawnEntity("VehicleTankFlamer", origin);
            var vehicleTank = SComp<RMCFlamerTankComponent>(vehicle);
            var vehicleProvider = SComp<RMCFlamerAmmoProviderComponent>(vehicle);
            flamer.ShootFlamer((vehicle, vehicleProvider), (vehicle, SComp<GunComponent>(vehicle)), null, origin, target);
            var vehicleShape = Shape("VehicleTankFlamer");

            Assert.Multiple(() =>
            {
                Assert.That(m240, Is.EqualTo((5, 3)), "an M240 with UT reaches 5 rows, at most 3 wide");
                Assert.That(m240BGel, Is.EqualTo((7, 3)), "an M240 with B-gel reaches 7 rows, at most 3 wide");
                Assert.That(lc9.Rows, Is.EqualTo(3), "the LC9 colony tank projects 60% of UT's reach");
                Assert.That(specialistB, Is.EqualTo((6, 5)), "the M240-T B tank reaches 6 rows and widens to 5");
                Assert.That(vehicleShape.Rows, Is.EqualTo(7), "the vehicle flamer keeps its full UT reach of 7");
                Assert.That(vehicleTank.AllowBlueFire, Is.False, "the vehicle flamer no longer needs blue fire for its reach");
                Assert.That(vehicleTank.ConeMaxWidth, Is.EqualTo(5));
            });

            DeleteChains();
        });
    }

    /// <summary>
    /// One DRGN cartridge fires its main jet plus four shards. The main jet (whose impact makes the burst) flies down
    /// the middle of the fan, the shards fan out symmetrically either side, and each shard hits softer than the jet.
    /// </summary>
    [Test]
    public async Task DragonFlamerFansShardsAroundACentredJet()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            LayFloor(map);
            var origin = map.GridCoords;
            var gun = SEntMan.SpawnEntity("VehicleTankDragonFlamer", origin);
            Assert.That(SEntMan.HasComponent<CMUCenteredProjectileSpreadComponent>(gun), Is.True);

            var cartridge = SEntMan.SpawnEntity("VehicleCartridgeDragonFlame", origin);
            var ammo = new List<(EntityUid? Entity, IShootable Shootable)> { (cartridge, SComp<CartridgeAmmoComponent>(cartridge)) };
            var shot = SEntMan.System<ServerGunSystem>()
                .Shoot((gun, SComp<GunComponent>(gun)), ammo, origin, origin.Offset(new Vector2(-6, 0)), out _);

            Assert.That(shot, Is.Not.Null);
            Assert.That(shot!, Has.Count.EqualTo(5), "one cartridge fires the jet and four shards");
            Assert.That(SEntMan.GetComponent<MetaDataComponent>(shot[0]).EntityPrototype?.ID, Is.EqualTo("VehicleProjectileDragonFlame"));
            Assert.That(shot.Skip(1).Select(p => SEntMan.GetComponent<MetaDataComponent>(p).EntityPrototype?.ID),
                Is.All.EqualTo("VehicleProjectileDragonFlameShard"));

            var headings = shot.Select(p => Heading(SComp<PhysicsComponent>(p).LinearVelocity)).ToList();
            var main = headings[0];
            var shards = headings.Skip(1).Select(h => Wrap(h - main)).OrderBy(h => h).ToList();
            Assert.Multiple(() =>
            {
                Assert.That(shards.Sum(), Is.EqualTo(0).Within(1), "the shards fan out evenly around the main jet");
                Assert.That(shards[0], Is.LessThan(-5), "shards fly to one side");
                Assert.That(shards[^1], Is.GreaterThan(5), "and to the other");
                Assert.That(Damage("VehicleProjectileDragonFlameShard"), Is.LessThan(Damage("VehicleProjectileDragonFlame")),
                    "a shard hits softer than the main jet");
            });
        });
    }

    private void LayFloor(TestMapData map)
    {
        var maps = SEntMan.System<SharedMapSystem>();
        var grid = SEntMan.GetComponent<MapGridComponent>(map.Grid);
        var floor = new Tile(Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);
        for (var x = -14; x <= 2; x++)
        {
            for (var y = -4; y <= 4; y++)
                maps.SetTile(map.Grid, grid, new Vector2i(x, y), floor);
        }
    }

    private (int Rows, int Width) Shape(string source)
    {
        var chains = Chains();
        Assert.That(chains, Has.Count.EqualTo(1), $"{source} should fire one flamer chain");
        var rows = chains[0].Comp.Tiles.GroupBy(t => t.At).ToList();
        return (rows.Count, rows.Max(r => r.Count()));
    }

    private float Damage(string id)
    {
        Assert.That(SProtoMan.Index<EntityPrototype>(id).TryGetComponent<ProjectileComponent>(out var projectile, SEntMan.ComponentFactory),
            Is.True);
        return projectile!.Damage.GetTotal().Float();
    }

    private static double Heading(Vector2 velocity) => Math.Atan2(velocity.Y, velocity.X) * 180 / Math.PI;

    private static double Wrap(double degrees)
    {
        while (degrees > 180)
            degrees -= 360;
        while (degrees < -180)
            degrees += 360;
        return degrees;
    }

    private List<Entity<RMCFlamerChainComponent>> Chains()
    {
        var chains = new List<Entity<RMCFlamerChainComponent>>();
        var query = SEntMan.EntityQueryEnumerator<RMCFlamerChainComponent>();
        while (query.MoveNext(out var uid, out var chain))
            chains.Add((uid, chain));
        return chains;
    }

    private void DeleteChains()
    {
        foreach (var chain in Chains())
            SEntMan.DeleteEntity(chain.Owner);
    }
}
