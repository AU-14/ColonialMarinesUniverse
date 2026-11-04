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
    [TestCase("CMUMobYautjaYoungblood")]
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

    [TestCase("CMUMobYautja")]
    [TestCase("CMUMobYautjaYoungblood")]
    public async Task YautjaGetWarnedOnceBeforeCrit(string proto)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var mob = SSpawnAtPosition(proto, map.GridCoords);
            var warning = SEntMan.GetComponent<YautjaVitalsWarningComponent>(mob);
            var crit = CritThreshold(mob);

            DamageTo(mob, crit * 0.5f);
            Assert.That(warning.Warned, Is.False, "warned way too early");

            DamageTo(mob, crit * 0.85f);
            Assert.That(warning.Warned, Is.True, "no warning right before crit");
            Assert.That(Server.System<MobStateSystem>().IsAlive(mob), Is.True);

            // healing back up re-arms it for the next fight
            DamageTo(mob, crit * 0.5f);
            Assert.That(warning.Warned, Is.False);
        });
    }

    // cmss13 yaut_bracers.dm blocks the final countdown once you're past crit
    [Test]
    public async Task CritPredCannotArmSelfDestruct()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var pred = SSpawnAtPosition("CMUMobYautja", map.GridCoords);
            // the hunter spawns already wearing one
            Assert.That(Server.System<InventorySystem>().TryGetSlotEntity(pred, "gloves", out var worn), Is.True);
            var bracer = worn!.Value;
            var bracerComp = SEntMan.GetComponent<YautjaBracerComponent>(bracer);
            Assert.That(bracerComp.User, Is.EqualTo(pred));

            DamageTo(pred, CritThreshold(pred) + 5);
            Assert.That(Server.System<MobStateSystem>().IsCritical(pred), Is.True);

            Assert.That(Server.System<YautjaSelfDestructSystem>().TryArmSelfDestruct((bracer, bracerComp), pred), Is.False);
            Assert.That(bracerComp.SelfDestructArmed, Is.False);
        });
    }

    [Test]
    public async Task HumansStillDropStraightIntoCrit()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var human = SSpawnAtPosition("CMMobHuman", map.GridCoords);
            DamageTo(human, CritThreshold(human) + 5);
            Assert.That(Server.System<MobStateSystem>().IsCritical(human), Is.True);
        });
    }

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
