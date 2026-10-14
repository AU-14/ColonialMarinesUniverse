using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Dropship.AttachmentPoint;
using Content.Shared._RMC14.Dropship.Fabricator;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.PowerLoader;
using Content.Shared._RMC14.Sentry;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.CMU14.Dropship.DirectFire;
using Content.Shared.CMU14;
using Content.Shared.CMU14.Weapons.Ranged;
using Content.Shared.Atmos.Components;
using Content.Shared.Interaction;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Throwing;
using Content.Shared._RMC14.PowerLoader.Events;
using Content.Shared.DoAfter;
using Robust.Shared.Physics.Components;
using Robust.Shared.Containers;
using System.Reflection;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.Explosion.Components;

#pragma warning disable RA0002 // Arrange the mounted ammo pipeline and inspect its consumed payload.

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class MohawkAmmoTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Dirty = true };

    [Test]
    public async Task TempestReallyCoversMoreTilesGroundsideAndAirborne()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            for (var x = -10; x <= 10; x++)
            {
                for (var y = -10; y <= 10; y++)
                    maps.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);
            }
            var crate = SEntMan.SpawnEntity("CMUMohawkM90AmmoDevastator", map.GridCoords);
            var ammo = SEntMan.GetComponent<DropshipAmmoComponent>(crate);
            var ground = SEntMan.SpawnEntity("CMUMohawkM90DevastatorImpact", map.GridCoords);
            var explosive = SEntMan.GetComponent<ExplosiveComponent>(ground);
            var method = typeof(ExplosionSystem).GetMethod("GetExplosionTiles", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var center = Server.System<SharedTransformSystem>().ToMapCoordinates(map.GridCoords);
            int Tiles(float total, float slope, float max)
            {
                var result = ((int Area, List<float> Intensities, ExplosionSpaceTileFlood? Space,
                    Dictionary<EntityUid, ExplosionGridTileFlood> Grids, System.Numerics.Matrix3x2 Matrix))
                    method.Invoke(Server.System<ExplosionSystem>(), new object[] { center, "RMC", total, slope, max })!;
                return result.Grids[map.Grid].TileLists.Sum(row => row.Value.Count);
            }
            var oldCoverage = Tiles(60, 6, 12);
            var newCoverage = Tiles(ammo.Explosion!.Total, ammo.Explosion.Slope, ammo.Explosion.Max);
            Assert.That(newCoverage, Is.GreaterThanOrEqualTo(oldCoverage * 1.5),
                "Tempest's wider AoE must measurably increase actual explosion tile coverage.");
            Assert.That(Tiles(explosive.TotalIntensity, explosive.IntensitySlope, explosive.MaxIntensity), Is.EqualTo(newCoverage));
            Assert.That(ammo.FriendlyImpactRadius, Is.GreaterThanOrEqualTo(
                SharedDropshipWeaponSystem.EstimateBlastRadius(ammo.Explosion)));
        });
    }

    [Test]
    public async Task DualFeedLoadsAlternatesFallsBackAndUnloadsBothCrates()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            SEntMan.EnsureComponent<DropshipComponent>(map.Grid).AttachmentPoints.Clear();
            SEntMan.EnsureComponent<ShipFactionComponent>(map.Grid).Faction = "GOVFOR";
            var point = SEntMan.SpawnEntity("CMUMohawkM90Point", map.GridCoords);
            SEntMan.GetComponent<DropshipComponent>(map.Grid).AttachmentPoints.Add(point);
            var mount = SEntMan.GetComponent<DropshipWeaponPointComponent>(point);
            var containers = Server.System<SharedContainerSystem>();
            var weapon = containers.GetContainer(point, mount.WeaponContainerSlotId).ContainedEntities.Single();
            var firstSlot = containers.GetContainer(point, mount.AmmoContainerSlotId);
            var secondSlot = containers.GetContainer(point, mount.SecondAmmoContainerSlotId!);
            var loader = SEntMan.SpawnEntity("RMCMechPowerLoader", map.GridCoords);
            EntityUid Load(string prototype)
            {
                var crate = SEntMan.SpawnEntity(prototype, map.GridCoords);
                SEntMan.GetComponent<PowerLoaderAttachableComponent>(crate).AttachDelay = TimeSpan.Zero;
                var interact = new BeforeRangedInteractEvent(loader, crate, point, map.GridCoords, true);
                SEntMan.EventBus.RaiseLocalEvent(crate, interact);
                return crate;
            }
            var first = Load("CMUMohawkM90Ammo");
            var second = Load("CMUMohawkM90AmmoIncendiary");
            Assert.That(firstSlot.Contains(first), Is.True);
            Assert.That(secondSlot.Contains(second), Is.True);
            var extra = Load("CMUMohawkM90AmmoScatter");
            Assert.That(firstSlot.Contains(extra) || secondSlot.Contains(extra), Is.False,
                "A full dual-feed mount must reject a third box.");
            var count = new GetAmmoCountEvent();
            SEntMan.EventBus.RaiseLocalEvent(weapon, ref count);
            Assert.That(count.Count, Is.EqualTo(800));
            Assert.That(count.Capacity, Is.EqualTo(800));
            Assert.That(Server.System<SharedSentryTargetingSystem>().TryApplyDefaultFaction(point, "GOVFOR"), Is.True);
            var chin = SEntMan.GetComponent<MohawkChinGunComponent>(point);
            chin.Calibrated = true;
            chin.IffConfigured = true;
            EntityUid Consume()
            {
                var take = new TakeAmmoEvent(1, new(), map.GridCoords, null);
                SEntMan.EventBus.RaiseLocalEvent(weapon, take);
                Assert.That(take.Ammo, Has.Count.EqualTo(1));
                return take.Ammo.Single().Entity!.Value;
            }
            string Prototype(EntityUid entity) => SEntMan.GetComponent<MetaDataComponent>(entity).EntityPrototype!.ID;
            chin.AlternateFeeds = true;
            Assert.That(Prototype(Consume()), Is.EqualTo("CMUMohawkM90HEAP"));
            Assert.That(chin.ActiveFeed, Is.EqualTo(1));
            Assert.That(Prototype(Consume()), Is.EqualTo("CMUMohawkM90Incendiary"));
            Assert.That(chin.ActiveFeed, Is.EqualTo(0));
            chin.AlternateFeeds = false;
            chin.ActiveFeed = 1;
            Assert.That(Prototype(Consume()), Is.EqualTo("CMUMohawkM90Incendiary"));
            Assert.That(chin.ActiveFeed, Is.EqualTo(1));
            SEntMan.GetComponent<DropshipAmmoComponent>(second).Rounds = 0;
            Assert.That(Prototype(Consume()), Is.EqualTo("CMUMohawkM90HEAP"));
            Assert.That(chin.ActiveFeed, Is.EqualTo(0));

            for (var i = 0; i < 10; i++)
                Consume();
            var casings = SEntMan.QueryEntities<CMUSpentCasingComponent>().ToArray();
            Assert.That(casings, Has.Length.EqualTo(14));
            foreach (var casing in casings)
            {
                Assert.That(SEntMan.HasComponent<ThrownItemComponent>(casing), Is.True);
                Assert.That(SEntMan.GetComponent<PhysicsComponent>(casing).LinearVelocity.Length(), Is.InRange(2f, 5f));
            }
            Assert.That(casings.Select(casing => SEntMan.GetComponent<PhysicsComponent>(casing).LinearVelocity).Distinct().Count(),
                Is.GreaterThan(1), "Ejection direction and speed must vary between shells.");

            void Unload(EntityUid crate, string slot)
            {
                var detach = new DropshipDetachDoAfterEvent(SEntMan.GetNetEntity(point), SEntMan.GetNetEntity(crate), slot);
                detach.DoAfter = new DoAfter(0, new DoAfterArgs(SEntMan, loader, TimeSpan.Zero, detach, point, point), TimeSpan.Zero);
                SEntMan.EventBus.RaiseLocalEvent(point, detach);
                Assert.That(containers.GetContainer(point, slot).Contains(crate), Is.False);
                Assert.That(containers.GetContainer(point, mount.WeaponContainerSlotId).Contains(weapon), Is.True);
            }
            Unload(first, mount.AmmoContainerSlotId);
            Unload(second, mount.SecondAmmoContainerSlotId!);
        });
    }

    [TestCase("CMUMohawkM90Ammo", "CMUMohawkM90HEAP", 165, 0, 3000, false)]
    [TestCase("CMUMohawkM90AmmoScatter", "CMUMohawkM90Scatter", 60, 10, 3000, false)]
    [TestCase("CMUMohawkM90AmmoIncendiary", "CMUMohawkM90Incendiary", 130, 2, 3000, true)]
    [TestCase("CMUMohawkM90AmmoDevastator", "CMUMohawkM90Devastator", 300, 12, 6000, false)]
    [TestCase("CMUMohawkM90AmmoKinetic", "CMUMohawkM90Kinetic", 41, 0, 750, false)]
    public async Task PowerloaderLoadsVariantAndGunConsumesItsProjectile(
        string crateId, string projectileId, double damage, int spread, int cost, bool burns)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            SEntMan.EnsureComponent<DropshipComponent>(map.Grid).AttachmentPoints.Clear();
            SEntMan.EnsureComponent<ShipFactionComponent>(map.Grid).Faction = "GOVFOR";
            var point = SEntMan.SpawnEntity("CMUMohawkM90Point", map.GridCoords);
            SEntMan.GetComponent<DropshipComponent>(map.Grid).AttachmentPoints.Add(point);
            var mount = SEntMan.GetComponent<DropshipWeaponPointComponent>(point);
            var containers = Server.System<SharedContainerSystem>();
            var weapon = containers.GetContainer(point, mount.WeaponContainerSlotId).ContainedEntities.Single();
            var crate = SEntMan.SpawnEntity(crateId, map.GridCoords);
            var loader = SEntMan.SpawnEntity("RMCMechPowerLoader", map.GridCoords);
            SEntMan.GetComponent<PowerLoaderAttachableComponent>(crate).AttachDelay = TimeSpan.Zero;
            var interact = new BeforeRangedInteractEvent(loader, crate, point, map.GridCoords, true);
            SEntMan.EventBus.RaiseLocalEvent(crate, interact);
            Assert.That(interact.Handled, Is.True);
            Assert.That(containers.GetContainer(point, mount.AmmoContainerSlotId).Contains(crate), Is.True);
            var payload = SEntMan.GetComponent<DropshipAmmoComponent>(crate);
            Assert.That(payload.Weapon.Id, Is.EqualTo("CMUMohawkM90"));
            var capacity = payload.GroundCompatible ? 1200 : 400;
            if (payload.GroundCompatible)
                Assert.That(payload.DirectFireRate, Is.EqualTo(15));
            Assert.That(payload.Rounds, Is.EqualTo(capacity));
            Assert.That(payload.MaxRounds, Is.EqualTo(capacity));
            Assert.That(payload.RoundsPerShot, Is.EqualTo(1));
            Assert.That(payload.DirectFireSpreadDegrees, Is.EqualTo(spread));
            Assert.That(payload.Damage!.GetTotal().Float(), Is.EqualTo(damage));
            Assert.That(SEntMan.GetComponent<DropshipFabricatorPrintableComponent>(crate).Cost, Is.EqualTo(cost));
            Assert.That(Server.System<SharedSentryTargetingSystem>().TryApplyDefaultFaction(point, "GOVFOR"), Is.True);
            var chin = SEntMan.GetComponent<MohawkChinGunComponent>(point);
            chin.Calibrated = true;
            chin.IffConfigured = true;
            var transform = Server.System<SharedTransformSystem>();
            var maximumScatter = 0f;
            chin.DefensiveFire = true;
            for (var sample = 0; sample < 100; sample++)
            {
                var attempt = new AttemptShootEvent(weapon, null, map.GridCoords,
                    map.GridCoords.Offset(new Vector2(1.5f, 20)));
                SEntMan.EventBus.RaiseLocalEvent(weapon, ref attempt);
                Assert.That(attempt.Cancelled, Is.EqualTo(!payload.GroundCompatible),
                    "Only Squall kinetic ammunition can be fired groundside.");
                if (!payload.GroundCompatible)
                    continue;
                var direction = transform.ToMapCoordinates(attempt.ToCoordinates!.Value).Position -
                                transform.ToMapCoordinates(attempt.FromCoordinates).Position;
                var degrees = MathF.Abs(MathHelper.RadiansToDegrees(MathF.Atan2(direction.X, direction.Y)));
                maximumScatter = MathF.Max(maximumScatter, degrees);
                Assert.That(degrees, Is.LessThanOrEqualTo(spread + 0.001f));
            }
            chin.DefensiveFire = false;
            if (payload.GroundCompatible && spread > 0)
                Assert.That(maximumScatter, Is.GreaterThan(spread * 0.5f),
                    "The ammo must actually scatter firing trajectories, not merely advertise a spread field.");
            var take = new TakeAmmoEvent(1, new(), map.GridCoords, null);
            SEntMan.EventBus.RaiseLocalEvent(weapon, take);
            Assert.That(take.Ammo, Has.Count.EqualTo(1));
            Assert.That(payload.Rounds, Is.EqualTo(capacity - 1));
            var projectile = take.Ammo.Single().Entity!.Value;
            Assert.That(SEntMan.GetComponent<MetaDataComponent>(projectile).EntityPrototype!.ID,
                Is.EqualTo(projectileId));
            Assert.That(SEntMan.GetComponent<ProjectileComponent>(projectile).Damage.GetTotal().Float(),
                Is.EqualTo(damage));
            Assert.That(SEntMan.HasComponent<IgniteOnProjectileHitComponent>(projectile), Is.EqualTo(burns));
            Assert.That(SEntMan.GetComponent<ProjectileIFFComponent>(projectile).Enabled, Is.True);
            if (burns)
            {
                var victim = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(15, 15)));
                var hit = new ProjectileHitEvent(SEntMan.GetComponent<ProjectileComponent>(projectile).Damage, victim);
                SEntMan.EventBus.RaiseLocalEvent(projectile, ref hit);
                Assert.That(SEntMan.GetComponent<FlammableComponent>(victim).OnFire, Is.True,
                    "An Ember projectile hit must ignite a susceptible mob.");
            }

        });
    }

    [Test]
    public async Task M90CasingsExpireAfterTenSeconds()
    {
        var map = await Pair.CreateTestMap();
        EntityUid casing = default;
        await Server.WaitAssertion(() =>
        {
            casing = SEntMan.SpawnEntity("CMUMohawkM90Casing", map.GridCoords);
            Assert.That(SEntMan.GetComponent<Robust.Shared.Spawners.TimedDespawnComponent>(casing).Lifetime,
                Is.EqualTo(10));
        });
        await Pair.RunSeconds(9);
        await Server.WaitAssertion(() => Assert.That(SEntMan.Deleted(casing), Is.False));
        await Pair.RunSeconds(2);
        await Server.WaitAssertion(() => Assert.That(SEntMan.Deleted(casing), Is.True));
    }
}
