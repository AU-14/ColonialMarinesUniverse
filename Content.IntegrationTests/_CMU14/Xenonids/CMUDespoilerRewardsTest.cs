using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Xenonids.Despoiler;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared.Damage;
using Content.Shared.Weapons.Melee.Events;

namespace Content.IntegrationTests.CMU14.Xenonids;

[TestFixture]
public sealed class CMUDespoilerRewardsTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    [TestCase("CMU14XenoNeomorph")]
    [TestCase("CMXenoDrone")]
    public async Task SlashingEnemyHiveGrantsHypertensionButSlashingOwnHiveDoesNot(string targetPrototype)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var despoiler = SEntMan.SpawnEntity("RMCXenoDespoiler", map.GridCoords);
            var target = SEntMan.SpawnEntity(targetPrototype, map.GridCoords);
            var ownHive = SEntMan.SpawnEntity("CMUAlphaHive", map.GridCoords);
            var enemyHive = SEntMan.SpawnEntity("CMUPathogenHive", map.GridCoords);
            var hives = Server.System<SharedXenoHiveSystem>();
            hives.SetHive(despoiler, ownHive);
            hives.SetHive(target, ownHive);
            var hypertension = SEntMan.GetComponent<XenoDespoilerHypertensionComponent>(despoiler);

            var friendlyHit = new MeleeHitEvent([target], despoiler, despoiler, new DamageSpecifier(), null);
            SEntMan.EventBus.RaiseLocalEvent(despoiler, friendlyHit);
            Assert.That(hypertension.Points + hypertension.Stacks * hypertension.PointsPerStack, Is.Zero,
                "friendly attacks must not grant hypertension");

            hives.SetHive(target, enemyHive);
            var hostileHit = new MeleeHitEvent([target], despoiler, despoiler, new DamageSpecifier(), null);
            SEntMan.EventBus.RaiseLocalEvent(despoiler, hostileHit);
            Assert.That(hypertension.Points + hypertension.Stacks * hypertension.PointsPerStack,
                Is.EqualTo(hypertension.PointsPerSlash),
                "a slash against a living enemy hive must grant hypertension");
        });
    }
}
