using System.Linq;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.CMU14.ConstructionRobot;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Stacks;
using Content.Shared.Verbs;
using Robust.Shared.Localization;

namespace Content.IntegrationTests.CMU14.Construction;

public sealed class CMUConstructionRobotTest : InteractionTest
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task RobotCommitsMaterialsOnlyIfItSurvivesConstruction(bool destroyDuringBuild)
    {
        var robot = await Spawn("CMUConstructionRobot", PlayerCoords);
        await Server.WaitPost(() =>
        {
            var verbs = Server.System<SharedVerbSystem>();
            var claim = verbs.GetLocalVerbs(ToServer(robot), SPlayer, typeof(Verb))
                .Single(verb => verb.Text == Loc.GetString("cmu-construction-robot-verb-take"));
            verbs.ExecuteVerb(claim, SPlayer, ToServer(robot));
        });
        var metal = await Spawn("CMSheetMetal", TargetCoords);
        await Server.WaitPost(() => Stack.SetCount((ToServer(metal), null), 10));
        await StartConstruction("AU14BarricadeMetalRecipe");

        if (destroyDuringBuild)
        {
            for (var i = 0; i < 12 && !Comp<CMUConstructionRobotComponent>(robot).IsBuilding; i++)
                await RunSeconds(0.25f);

            await Server.WaitAssertion(() =>
            {
                Assert.That(Comp<CMUConstructionRobotComponent>(robot).IsBuilding, Is.True);
                var damage = new DamageSpecifier(SProtoMan.Index<DamageTypePrototype>("Blunt"), FixedPoint2.New(500));
                Server.System<DamageableSystem>().SetDamage((ToServer(robot), Comp<DamageableComponent>(robot)), damage);
            });
            await RunSeconds(4);
            await Server.WaitAssertion(() =>
            {
                Assert.That(SEntMan.HasComponent<CMUConstructionRobotOwnerComponent>(SPlayer), Is.False);
                Assert.That(SEntMan.EntityQuery<StackComponent>().Where(s => s.StackTypeId == "CMSteel").Sum(s => s.Count),
                    Is.EqualTo(10), "Destroying a working robot must release its cargo intact.");
            });
            Assert.That((await FindEntity("AU14BarricadeMetal", shouldSucceed: false)).IsValid(), Is.False);
            return;
        }

        await RunSeconds(7);
        await CheckTargetChange();
        AssertPrototype("AU14BarricadeMetal");
        await Server.WaitAssertion(() => Assert.That(Comp<StackComponent>(metal).Count, Is.EqualTo(6)));
    }

    [Test]
    public async Task DraggingFuelTankTransfersOnlyTheMissingFuel()
    {
        var robot = await Spawn("CMUConstructionRobot", PlayerCoords);
        var tank = await Spawn("RMCTankReagentFuel", TargetCoords);
        await Server.WaitPost(() => Comp<CMUConstructionRobotComponent>(robot).WeldingFuel -= 125);

        await DragDrop(tank, robot);
        await Server.WaitAssertion(() =>
        {
            Assert.That(ActiveDoAfters.Select(action => action.Args.Event),
                Has.One.TypeOf<CMUConstructionRobotRefuelDoAfterEvent>());
            Assert.That(Comp<CMUConstructionRobotComponent>(robot).WeldingFuel, Is.EqualTo(875));
        });
        await AwaitDoAfters();
        await Server.WaitAssertion(() =>
        {
            Assert.That(Comp<CMUConstructionRobotComponent>(robot).WeldingFuel, Is.EqualTo(1000));
            var solutions = Server.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetSolution(ToServer(tank), "tank", out _, out var solution));
            Assert.That(solution!.GetReagentQuantity(new ReagentId("WeldingFuel", null)).Float(), Is.EqualTo(875));
        });
    }
}
