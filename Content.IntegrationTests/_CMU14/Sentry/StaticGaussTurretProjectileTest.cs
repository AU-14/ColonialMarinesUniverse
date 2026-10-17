using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._CMU14.Sentry;

[TestFixture]
public sealed class StaticGaussTurretProjectileTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [TestCase("RMCTurretAlmayer", "BulletRifle10x24mm")]
    [TestCase("RMCTurretAlmayer", "BulletRifle10x24mmAP")]
    [TestCase("AU14TurretAlmayerWeYu", "BulletRifle10x24mm")]
    [TestCase("AU14TurretAlmayerWeYu", "BulletRifle10x24mmAP")]
    public async Task RifleBulletsCollideDamageAndDestroyStaticTurret(string turretPrototype, string bulletPrototype)
    {
        var map = await Pair.CreateTestMap();
        EntityUid turret = default;
        await Server.WaitAssertion(() => turret = SEntMan.SpawnEntity(turretPrototype, map.GridCoords));

        async Task Shoot()
        {
            await Server.WaitAssertion(() =>
            {
                var bullet = SEntMan.SpawnEntity(bulletPrototype, map.GridCoords.Offset(new Vector2(-3, 0)));
                Server.System<SharedGunSystem>().ShootProjectile(bullet, Vector2.UnitX, Vector2.Zero, null, speed: 20);
            });
            await Pair.RunSeconds(0.5f);
        }

        // Run the real projectile through physics: directly raising a hit event would
        // miss the original bug, where bullets never collided with the turret at all.
        await Shoot();
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.EntityExists(turret), Is.True, "One rifle round must not destroy a full-health turret.");
            var health = SEntMan.GetComponent<DamageableComponent>(turret);
            Assert.That(Server.System<DamageableSystem>().GetPositiveDamage((turret, health)).DamageDict, Is.Not.Empty,
                "A bullet crossing the turret must deal damage instead of passing through its collision shape.");
        });

        var destroyed = false;
        for (var shot = 0; shot < 10 && !destroyed; shot++)
        {
            await Shoot();
            await Server.WaitAssertion(() => destroyed = !SEntMan.EntityExists(turret));
        }

        Assert.That(destroyed, Is.True, "Repeated rifle hits must trigger the turret's existing destruction threshold.");
    }
}
