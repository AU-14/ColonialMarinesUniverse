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

}
