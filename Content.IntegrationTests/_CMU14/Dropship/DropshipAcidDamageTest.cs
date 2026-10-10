using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Dropship.Integrity;
using Content.Shared.CMU14.Dropship.Integrity;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Projectiles;
using Robust.Shared.Physics.Components;

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class DropshipAcidDamageTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Dirty = true };

    [TestCase(false)]
    [TestCase(true)]
    public async Task DirectSpitDamageReachesTheHullExactlyOnce(bool damageableWall)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var integrity = SEntMan.EnsureComponent<DropshipIntegrityComponent>(map.Grid.Owner);
            var wall = SEntMan.SpawnEntity("CMWallMetal", map.GridCoords);
            SEntMan.EnsureComponent<DropshipHullComponent>(wall);
            if (!damageableWall)
                SEntMan.RemoveComponent<DamageableComponent>(wall);
            var spit = SEntMan.SpawnEntity("XenoSpitProjectile", map.GridCoords);
            Server.System<SharedProjectileSystem>().ProjectileCollide(
                (spit, SEntMan.GetComponent<ProjectileComponent>(spit), SEntMan.GetComponent<PhysicsComponent>(spit)), wall);
            var expected = damageableWall
                ? Server.System<DamageableSystem>().GetTotalDamage(wall).Float()
                : 80f;
            Assert.That(expected, Is.GreaterThan(0));
            Assert.That(integrity.MaxIntegrity - integrity.Integrity, Is.EqualTo(expected).Within(.01),
                "Intact walls need the hit notification; damageable hull pieces must only forward their ordinary damage delta.");
            Assert.That(SEntMan.HasComponent<DropshipAcidCoatingComponent>(map.Grid.Owner), Is.False);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task AcidGlobCorrodesTheSharedHullOverTimeWithoutStacking(bool damageableWall)
    {
        var map = await Pair.CreateTestMap();
        DropshipIntegrityComponent integrity = null!;
        await Server.WaitAssertion(() =>
        {
            integrity = SEntMan.EnsureComponent<DropshipIntegrityComponent>(map.Grid.Owner);
            var wall = SEntMan.SpawnEntity("CMWallMetal", map.GridCoords);
            SEntMan.EnsureComponent<DropshipHullComponent>(wall);
            Assert.That(Server.Transform(wall).Anchored, Is.True);
            Assert.That(Server.Transform(wall).GridUid, Is.EqualTo(map.Grid.Owner));
            if (!damageableWall)
                SEntMan.RemoveComponent<DamageableComponent>(wall);
            for (var i = 0; i < 2; i++)
            {
                var glob = SEntMan.SpawnEntity("XenoBombardAcidProjectile", map.GridCoords);
                Assert.That(SEntMan.HasComponent<DropshipAcidProjectileComponent>(glob), Is.True);
                Server.System<SharedProjectileSystem>().ProjectileCollide(
                    (glob, SEntMan.GetComponent<ProjectileComponent>(glob), SEntMan.GetComponent<PhysicsComponent>(glob)), wall);
                Assert.That(Server.Transform(wall).Anchored, Is.True, "Impact must leave the hull anchored.");
                Assert.That(SEntMan.HasComponent<DropshipHullComponent>(wall), Is.True);
                Assert.That(SEntMan.HasComponent<DropshipIntegrityComponent>(map.Grid.Owner), Is.True);
                Assert.That(SEntMan.GetComponent<ProjectileComponent>(glob).ProjectileSpent, Is.True);
            }
            Assert.That(SEntMan.HasComponent<DropshipAcidCoatingComponent>(map.Grid.Owner), Is.True);
            Assert.That(integrity.Integrity, Is.EqualTo(integrity.MaxIntegrity), "The glob deposits acid instead of applying all damage instantly.");
        });
        await Pair.RunSeconds(2.1f);
        await Server.WaitAssertion(() =>
            Assert.That(integrity.MaxIntegrity - integrity.Integrity, Is.EqualTo(40).Within(.01),
                "Two hits refresh a single coating instead of multiplying damage per hull piece."));
        await Pair.RunSeconds(11);
        await Server.WaitAssertion(() =>
        {
            Assert.That(integrity.MaxIntegrity - integrity.Integrity, Is.EqualTo(240).Within(.01));
            Assert.That(SEntMan.HasComponent<DropshipAcidCoatingComponent>(map.Grid.Owner), Is.False);
        });
    }
}
