using Content.IntegrationTests.Fixtures;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.CMU14.Chemistry;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Paper;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Chemistry;

[TestFixture]
public sealed class CMUChemicalRecipeMachineTest : GameTest
{
    [Test]
    public async Task ScanningPillPrintsMachineReadableAndManualRecipe()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var master = SEntMan.SpawnEntity("CMChemMaster", map.GridCoords);
            var pill = SEntMan.SpawnEntity("CMPill", map.GridCoords);
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var solutions = Server.System<SharedSolutionContainerSystem>();

            Assert.That(solutions.TryGetSolution(pill, "food", out var pillSolution), Is.True);
            Assert.That(solutions.TryAddReagent(pillSolution!.Value, "Water", FixedPoint2.New(5)), Is.True);

            var scan = new InteractUsingEvent(user, pill, master, map.GridCoords);
            SEntMan.EventBus.RaiseLocalEvent(master, scan);

            PaperComponent? recipePaper = null;
            var query = SEntMan.EntityQueryEnumerator<PaperComponent>();
            while (query.MoveNext(out _, out var paper))
            {
                if (!paper.Content.StartsWith("CMU14-CHEM-RECIPE/1", StringComparison.Ordinal))
                    continue;

                recipePaper = paper;
                break;
            }

            Assert.That(recipePaper, Is.Not.Null);
            Assert.That(recipePaper!.Content, Does.Contain("OUTPUT|Water|5"));
            Assert.That(recipePaper.Content, Does.Contain("---"));
            Assert.That(recipePaper.Content, Does.Contain("Manual synthesis procedure:"));
            Assert.That(recipePaper.EditingDisabled, Is.False);
        });
    }

    [Test]
    public async Task RecipeMachineLoadsListedIngredientsAndProducesTheFormulation()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var machine = SEntMan.SpawnEntity("CMUChemicalRecipeMachine", map.GridCoords);
            var paper = SEntMan.SpawnEntity("CMUChemRecipePaper", map.GridCoords);
            var beaker = SEntMan.SpawnEntity("Beaker", map.GridCoords);
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var paperSystem = Server.System<PaperSystem>();
            var itemSlots = Server.System<Content.Shared.Containers.ItemSlots.ItemSlotsSystem>();
            var solutions = Server.System<SharedSolutionContainerSystem>();

            paperSystem.SetContent(paper,
                "CMU14-CHEM-RECIPE/1\nOUTPUT|Water|5\nCONSUME|Water|5\n---\nManual step: mix water.");
            Assert.That(itemSlots.TryInsert(machine, "recipe", paper, user), Is.True);
            Assert.That(itemSlots.TryInsert(machine, "vessel", beaker, user), Is.True);
            Assert.That(solutions.TryGetFitsInDispenser(beaker, out var beakerSolution, out _), Is.True);
            Assert.That(solutions.TryAddReagent(beakerSolution!.Value, "Water", FixedPoint2.New(5)), Is.True);

            Send(machine, user, new CMUChemicalRecipeMachineLoadMessage());

            Assert.That(solutions.TryGetSolution(machine, CMUChemicalRecipeMachineComponent.StockSolutionId,
                out var stock), Is.True);
            Assert.That(stock!.Value.Comp.Solution.GetTotalPrototypeQuantity("Water"), Is.EqualTo(FixedPoint2.New(5)));
            Assert.That(solutions.TryGetFitsInDispenser(beaker, out beakerSolution, out var source), Is.True);
            Assert.That(source!.GetTotalPrototypeQuantity("Water"), Is.EqualTo(FixedPoint2.Zero));

            Send(machine, user, new CMUChemicalRecipeMachineSynthesizeMessage());

            Assert.That(source.GetTotalPrototypeQuantity("Water"), Is.EqualTo(FixedPoint2.New(5)));
            Assert.That(stock.Value.Comp.Solution.GetTotalPrototypeQuantity("Water"), Is.EqualTo(FixedPoint2.Zero));
        });
    }

    private void Send(EntityUid machine, EntityUid actor, BoundUserInterfaceMessage message)
    {
        message.Actor = actor;
        message.UiKey = CMUChemicalRecipeMachineUiKey.Key;
        SEntMan.EventBus.RaiseLocalEvent(machine, (object) message);
    }
}
