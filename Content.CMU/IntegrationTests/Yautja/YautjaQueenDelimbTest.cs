using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Xenonids.Melee;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.CMU14.Xenomorphs.Melee;
using Content.Shared.Weapons.Melee;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaQueenDelimbTest : GameTest
{
    [TestCase("CMUMobYautja")]
    [TestCase("CMUMobYautjaYoungblood")]
    public async Task YautjaShrugOffMostQueenSeverRolls(string proto)
    {
        await Server.WaitAssertion(() =>
        {
            var factory = Server.ResolveDependency<IComponentFactory>();
            Assert.That(SProtoMan.Index<EntityPrototype>(proto).TryGetComponent(out XenoMeleeSeverResistanceComponent? resistance, factory),
                Is.True);
            Assert.That(resistance!.ChanceMultiplier, Is.LessThan(1f));
        });
    }

    // queen forced to always roll a sever, so whether a limb drops is down to the target's resistance alone
    [TestCase(1f, true)]
    [TestCase(0f, false)]
    public async Task QueenSeverRollIsScaledByTheTargetsResistance(float multiplier, bool expectSever)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var queen = SSpawnAtPosition("CMXenoQueen", map.GridCoords);
            var victim = SSpawnAtPosition("CMMobHuman", map.GridCoords);
            SEntMan.GetComponent<XenoMeleeSeverComponent>(queen).Chance = 1f;
            SEntMan.EnsureComponent<XenoMeleeSeverResistanceComponent>(victim).ChanceMultiplier = multiplier;

            var limbsBefore = CountLimbs(victim);
            var melee = Server.System<SharedMeleeWeaponSystem>();
            Assert.That(melee.TryGetWeapon(queen, out var weapon, out var comp), Is.True);
            Assert.That(melee.AttemptLightAttack(queen, weapon, comp, victim, requireCombatMode: false), Is.True);

            Assert.That(CountLimbs(victim), expectSever ? Is.LessThan(limbsBefore) : Is.EqualTo(limbsBefore));
        });
    }

    private int CountLimbs(EntityUid body)
    {
        return Server.System<SharedBodySystem>().GetBodyChildren(body)
            .Count(part => part.Component.PartType is BodyPartType.Arm or BodyPartType.Leg);
    }
}
