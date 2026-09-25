using System.Linq;
using Content.IntegrationTests.Utility;
using Content.Server.CMU14.Ops.ThirdParty;
using Content.Server.CMU14.Threats;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Xenonids.Projectile;
using Content.Shared.CMU14.Dropship.Integrity;
using Content.Shared.CMU14.Threats;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Projectiles;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14;

[TestFixture]
public sealed class ReportedDropshipRegressionTest
{
    [TestCase("BulletRifle10x24mm", 1f)]
    [TestCase("XenoSpitProjectile", 4f)]
    public async Task LiveProjectilesCollideWithLexingtonHull(string prototype, float multiplier)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        EntityUid projectile = default;
        float expected = 0;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var integrity = entities.EnsureComponent<DropshipIntegrityComponent>(map.Grid);
            var hull = entities.SpawnEntity("CMNormandyWall1", map.GridCoords);
            entities.EnsureComponent<DropshipHullComponent>(hull);
            projectile = entities.SpawnEntity(prototype, map.GridCoords);
            var shot = entities.GetComponent<ProjectileComponent>(projectile);
            shot.OnlyCollideWhenShot = false;
            expected = integrity.Integrity - shot.Damage.GetTotal().Float() * multiplier *
                entities.System<DamageableSystem>().UniversalProjectileDamageModifier;
        });
        await pair.RunTicksSync(3);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Assert.That(entities.GetComponent<DropshipIntegrityComponent>(map.Grid).Integrity,
                Is.EqualTo(expected).Within(0.01f));
            Assert.That(entities.Deleted(projectile), Is.True, "The hull must consume a colliding shot.");
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public async Task HullImpactsDamageIntegrityOnceAndStopProjectiles(bool damageableHull, bool acid)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var integrity = entities.EnsureComponent<DropshipIntegrityComponent>(map.Grid);
            var hull = entities.SpawnEntity("CMNormandyWall1", map.GridCoords);
            entities.EnsureComponent<DropshipHullComponent>(hull);
            if (damageableHull)
            {
                entities.EnsureComponent<DamageableComponent>(hull);
                entities.EnsureComponent<InjurableComponent>(hull);
            }
            var projectile = entities.SpawnEntity(null, map.GridCoords);
            var physics = entities.AddComponent<PhysicsComponent>(projectile);
            var shot = entities.AddComponent<ProjectileComponent>(projectile);
            shot.Damage = new DamageSpecifier(pair.Server.ResolveDependency<IPrototypeManager>().Index<DamageTypePrototype>("Piercing"), FixedPoint2.New(10));
            if (acid)
                entities.AddComponent<XenoAcidProjectileComponent>(projectile);
            var before = integrity.Integrity;
            var expectedDamage = 10f * entities.System<DamageableSystem>().UniversalProjectileDamageModifier * (acid ? 4f : 1f);
            entities.System<SharedProjectileSystem>().ProjectileCollide((projectile, shot, physics), hull);
            Assert.That(integrity.Integrity, Is.EqualTo(before - expectedDamage).Within(0.01f));
            Assert.That(shot.ProjectileSpent, Is.True, "Intact hull must stop the impacting projectile.");
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("CMUUSArmyReinforcements", 8)]
    [TestCase("USArmyArmored", 6)]
    public async Task ArmyCallInSpawnsAboardShuttleWithoutAnAvailableLandingZone(string partyId, int crew)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true });
        var volunteers = await pair.Server.AddDummySessions(crew);
        await pair.RunUntilSynced();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var prototypes = pair.Server.ResolveDependency<IPrototypeManager>();
            var party = prototypes.Index<ThirdPartyPrototype>(partyId);
            Assert.That(party.EntryMethod, Is.EqualTo("shuttle"));
            var spawn = prototypes.Index(party.PartySpawn);
            entities.System<ThirdPartySystem>().SpawnThirdParty(party, spawn, false);
            var interest = entities.System<ForceInterestSystem>();
            var force = interest.GetForces(volunteers[0]).Single();
            Assert.That(force.TotalRoles, Is.EqualTo(crew));
            foreach (var volunteer in volunteers)
                interest.SetInterest(volunteer, force.Identifier, true);
            Assert.That(interest.GetForces(volunteers[0]).Single().InterestedPlayers, Is.EqualTo(crew));
        });
        await pair.RunTicksSync(60);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var bodies = entities.EntityQueryEnumerator<UnclaimedForceRoleComponent, TransformComponent>();
            var spawned = 0;
            EntityUid? shuttle = null;
            while (bodies.MoveNext(out _, out _, out var transform))
            {
                var grid = transform.GridUid;
                Assert.That(grid, Is.Not.Null, "The claimable Army body must arrive aboard a shuttle.");
                Assert.That(entities.HasComponent<DropshipComponent>(grid), Is.True);
                shuttle ??= grid;
                Assert.That(grid, Is.EqualTo(shuttle), "The whole force must deploy on the same shuttle.");
                spawned++;
            }
            Assert.That(spawned, Is.EqualTo(crew));
        });
        await pair.CleanReturnAsync();
    }
}
