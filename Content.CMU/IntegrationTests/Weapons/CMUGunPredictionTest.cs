using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.Weapons.Safety;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.CMU14.Weapons;

public sealed class CMUGunPredictionTest : GameTest
{
    [Test]
    public async Task JudgeBuckshotRejectsPredictedLaunchHitsOnItsMovingShooter()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var shooter = SSpawnAtPosition("CMMobHuman", map.GridCoords);
            var aim = map.GridCoords.Offset(new Vector2(2, 0));
            var target = SSpawnAtPosition("CMMobHuman", aim);
            var judge = SSpawnAtPosition("CMUWeaponRevolverLACNJudge", map.GridCoords);
            var shell = SSpawnAtPosition("CMShellShotgunBuckshot1", map.GridCoords);
            var guns = Server.System<SharedGunSystem>();
            Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(shooter, judge), Is.True);
            Assert.That(guns.TryAmmoInsert((judge, SComp<BallisticAmmoProviderComponent>(judge)), shell, shooter, judge, 0), Is.True);
            Server.System<CMUGunSafetySystem>().SetSafety(judge, false, playSound: false);
            Server.System<SharedPhysicsSystem>().SetLinearVelocity(shooter, new Vector2(0, 3));

            var pellets = guns.AttemptShoot((judge, SComp<GunComponent>(judge)), shooter, aim);
            Assert.That(pellets, Has.Count.EqualTo(4));

            var projectiles = Server.System<SharedProjectileSystem>();
            var damage = Server.System<DamageableSystem>();
            foreach (var pellet in pellets!)
            {
                var projectile = SComp<ProjectileComponent>(pellet);
                var physics = SComp<PhysicsComponent>(pellet);
                Assert.That(projectile.Shooter, Is.EqualTo(shooter));
                projectiles.ProjectileCollide((pellet, projectile, physics), shooter, predicted: true);
                Assert.That(projectile.ProjectileSpent, Is.False);
            }

            Assert.That(damage.GetTotalDamage(shooter).Float(), Is.Zero);
            var firstPellet = pellets[0];
            projectiles.ProjectileCollide((firstPellet, SComp<ProjectileComponent>(firstPellet), SComp<PhysicsComponent>(firstPellet)),
                target, predicted: true);
            Assert.That(damage.GetTotalDamage(target).Float(), Is.GreaterThan(0));

            var secondPellet = pellets[1];
            projectiles.ProjectileCollide((secondPellet, SComp<ProjectileComponent>(secondPellet), SComp<PhysicsComponent>(secondPellet)),
                shooter, context: DamageImpactContext.PointBlank);
            Assert.That(damage.GetTotalDamage(shooter).Float(), Is.GreaterThan(0),
                "An authorized point-blank self-shot can still hit.");
        });
    }
}
