using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Xenonids.CriticalGrace;
using Content.Shared.ActionBlocker;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Standing;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaCritGraceTest : GameTest
{
    // a pred pushed past crit keeps going on its feet for a while, then drops like anyone else
    [TestCase("CMUMobYautja")]
    [TestCase("CMUMobYautjaYoungblood")]
    public async Task YautjaStayUpForAWhileAfterCrossingCrit(string proto)
    {
        var map = await Pair.CreateTestMap();
        EntityUid mob = default;
        var grace = TimeSpan.Zero;

        await Server.WaitAssertion(() =>
        {
            mob = SSpawnAtPosition(proto, map.GridCoords);
            grace = SEntMan.GetComponent<CriticalGraceTimeComponent>(mob).GraceDuration;
            PushPastCrit(mob);

            Assert.That(Server.System<MobStateSystem>().IsAlive(mob), Is.True, "dropped straight into crit");
            Assert.That(Server.System<StandingStateSystem>().IsDown(mob), Is.False);
            Assert.That(Server.System<ActionBlockerSystem>().CanMove(mob), Is.True);
        });

        await Pair.RunTicksSync(Pair.SecondsToTicks((float) grace.TotalSeconds + 1));

        await Server.WaitAssertion(() =>
            Assert.That(Server.System<MobStateSystem>().IsCritical(mob), Is.True, "the grace never ran out"));
    }

    [Test]
    public async Task HumansStillDropStraightIntoCrit()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var human = SSpawnAtPosition("CMMobHuman", map.GridCoords);
            PushPastCrit(human);
            Assert.That(Server.System<MobStateSystem>().IsCritical(human), Is.True);
        });
    }

    private void PushPastCrit(EntityUid mob)
    {
        Assert.That(Server.System<MobThresholdSystem>().TryGetThresholdForState(mob, MobState.Critical, out var crit), Is.True);
        var damage = new DamageSpecifier();
        damage.DamageDict["Blunt"] = crit!.Value + 5;
        Server.System<DamageableSystem>().TryChangeDamage(mob, damage, ignoreResistances: true);
    }
}
