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
using Content.Shared.Atmos.Components;
using Content.Shared.Interaction;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;

#pragma warning disable RA0002 // Arrange the mounted ammo pipeline and inspect its consumed payload.

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class MohawkAmmoTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Dirty = true };

    [TestCase("CMUMohawkM90Ammo", "CMUMohawkM90HEAP", 165, 0, 275, false)]
    [TestCase("CMUMohawkM90AmmoScatter", "CMUMohawkM90Scatter", 60, 10, 350, false)]
    [TestCase("CMUMohawkM90AmmoIncendiary", "CMUMohawkM90Incendiary", 130, 2, 600, true)]
    [TestCase("CMUMohawkM90AmmoDevastator", "CMUMohawkM90Devastator", 300, 12, 2000, false)]
    public async Task PowerloaderLoadsVariantAndGunConsumesItsProjectile(
        string crateId, string projectileId, int damage, int spread, int cost, bool burns)
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
            Assert.That(payload.Rounds, Is.EqualTo(400));
            Assert.That(payload.RoundsPerShot, Is.EqualTo(40));
            Assert.That(payload.DirectFireSpreadDegrees, Is.EqualTo(spread));
            Assert.That(payload.Damage!.GetTotal().Int(), Is.EqualTo(damage));
            Assert.That(SEntMan.GetComponent<DropshipFabricatorPrintableComponent>(crate).Cost, Is.EqualTo(cost));
            Assert.That(Server.System<SharedSentryTargetingSystem>().TryApplyDefaultFaction(point, "GOVFOR"), Is.True);
            var chin = SEntMan.GetComponent<MohawkChinGunComponent>(point);
            var transform = Server.System<SharedTransformSystem>();
            var maximumScatter = 0f;
            chin.DefensiveFire = true;
            for (var sample = 0; sample < 100; sample++)
            {
                var attempt = new AttemptShootEvent(weapon, null, map.GridCoords,
                    map.GridCoords.Offset(new Vector2(1.5f, 20)));
                SEntMan.EventBus.RaiseLocalEvent(weapon, ref attempt);
                Assert.That(attempt.Cancelled, Is.False);
                var direction = transform.ToMapCoordinates(attempt.ToCoordinates!.Value).Position -
                                transform.ToMapCoordinates(attempt.FromCoordinates).Position;
                var degrees = MathF.Abs(MathHelper.RadiansToDegrees(MathF.Atan2(direction.X, direction.Y)));
                maximumScatter = MathF.Max(maximumScatter, degrees);
                Assert.That(degrees, Is.LessThanOrEqualTo(spread + 0.001f));
            }
            chin.DefensiveFire = false;
            if (spread > 0)
                Assert.That(maximumScatter, Is.GreaterThan(spread * 0.5f),
                    "The ammo must actually scatter firing trajectories, not merely advertise a spread field.");
            var take = new TakeAmmoEvent(1, new(), map.GridCoords, null);
            SEntMan.EventBus.RaiseLocalEvent(weapon, take);
            Assert.That(take.Ammo, Has.Count.EqualTo(1));
            Assert.That(payload.Rounds, Is.EqualTo(399));
            var projectile = take.Ammo.Single().Entity!.Value;
            Assert.That(SEntMan.GetComponent<MetaDataComponent>(projectile).EntityPrototype!.ID,
                Is.EqualTo(projectileId));
            Assert.That(SEntMan.GetComponent<ProjectileComponent>(projectile).Damage.GetTotal().Int(),
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
}
