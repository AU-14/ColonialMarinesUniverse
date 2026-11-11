using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Xenonids.CriticalGrace;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaCritGraceTest : GameTest
{
    // cmss13 preds drop the tick they cross crit like a marine, no xeno-style grace
    [TestCase("CMUMobYautja")]
    public async Task YautjaDropStraightIntoCrit(string proto)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var mob = SSpawnAtPosition(proto, map.GridCoords);
            Assert.That(SEntMan.HasComponent<CriticalGraceTimeComponent>(mob), Is.False);
            DamageTo(mob, CritThreshold(mob) + 5);
            Assert.That(Server.System<MobStateSystem>().IsCritical(mob), Is.True);
        });
    }

    // cmss13 yaut_bracers.dm blocks the final countdown once you're past crit

    private float CritThreshold(EntityUid mob)
    {
        Assert.That(Server.System<MobThresholdSystem>().TryGetThresholdForState(mob, MobState.Critical, out var crit), Is.True);
        return crit!.Value.Float();
    }

    // sets total damage to an exact amount so the warning sees both rising and healing changes
    private void DamageTo(EntityUid mob, float total)
    {
        var damageable = Server.System<DamageableSystem>();
        var current = damageable.GetTotalDamage(mob).Float();
        var damage = new DamageSpecifier();
        damage.DamageDict["Blunt"] = total - current;
        damageable.TryChangeDamage(mob, damage, ignoreResistances: true);
    }
}
