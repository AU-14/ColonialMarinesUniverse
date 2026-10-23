using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;

namespace Content.IntegrationTests.CMU14.BugReports;

// BUG-1062: welders stopped repairing barricades, nailguns still worked
[TestFixture]
public sealed class BarricadeWeldRepairTest : InteractionTest
{
    [Test]
    public async Task WelderRepairsDamagedMetalBarricade()
    {
        await SpawnTarget("CMBarricadeMetal");
        var target = STarget!.Value;
        await Server.WaitPost(() =>
        {
            SEntMan.System<SkillsSystem>().SetSkill(SPlayer, "RMCSkillConstruction", 2);
            SEntMan.System<DamageableSystem>().TryChangeDamage(target,
                new DamageSpecifier { DamageDict = { ["Blunt"] = 100 } }, true);
        });
        await RunTicks(5);

        FixedPoint2 before = default;
        await Server.WaitPost(() =>
            before = SEntMan.System<DamageableSystem>().GetTotalDamage(target));
        Assert.That(before, Is.GreaterThan(FixedPoint2.Zero));

        await InteractUsing("CMWelder");

        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.System<DamageableSystem>().GetTotalDamage(target), Is.LessThan(before)));
    }
}
