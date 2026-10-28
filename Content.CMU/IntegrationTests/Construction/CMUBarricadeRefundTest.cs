using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Stacks;

namespace Content.IntegrationTests.CMU14.Construction;

public sealed class CMUBarricadeRefundTest : InteractionTest
{
    [TestCase(4, false, 2, 2)]
    [TestCase(4, true, 2, 1)]
    [TestCase(0, false, 4, 4)]
    public async Task BuildAndDeconstructRefundsOnlyInvestedMaterials(int skill, bool damaged, int cost, int refund)
    {
        await Server.WaitPost(() => Server.System<SkillsSystem>().SetSkill(SPlayer, "RMCSkillConstruction", skill));
        await StartConstruction("AU14BarricadeMetalRecipe");
        await InteractUsing("CMSheetMetal", 10);
        AssertPrototype("AU14BarricadeMetal");
        await Server.WaitAssertion(() =>
        {
            var held = HandSys.GetActiveItem((SPlayer, Hands));
            Assert.That(SEntMan.GetComponent<StackComponent>(held!.Value).Count, Is.EqualTo(10 - cost));
            if (damaged)
            {
                var damage = new DamageSpecifier(SProtoMan.Index<DamageTypePrototype>("Blunt"), FixedPoint2.New(1));
                Server.System<DamageableSystem>().SetDamage((STarget!.Value,
                    SEntMan.GetComponent<DamageableComponent>(STarget.Value)), damage);
            }
            Server.System<SharedTransformSystem>().Unanchor(STarget!.Value);
            SConstruction.SetPathfindingTarget(STarget.Value, "start");
        });
        await InteractUsing(Screw);
        await InteractUsing(Pry);
        await FindEntity(("CMSheetMetal1", refund));
        await Server.WaitAssertion(() => Assert.That(
            SEntMan.EntityQuery<StackComponent>().Where(s => s.StackTypeId == "CMSteel").Sum(s => s.Count),
            Is.EqualTo(refund), "Deconstruction must not create the undiscounted material amount."));
    }
}
