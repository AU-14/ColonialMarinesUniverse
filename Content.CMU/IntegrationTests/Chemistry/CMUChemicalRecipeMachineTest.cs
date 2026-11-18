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

    [Test]
    public async Task SynthesisChargesThePrintedDoseAfterEditing()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var master = SEntMan.SpawnEntity("CMChemMaster", map.GridCoords);
            var pill = SEntMan.SpawnEntity("CMPill", map.GridCoords);
            var machine = SEntMan.SpawnEntity("CMUChemicalRecipeMachine", map.GridCoords);
            var beaker = SEntMan.SpawnEntity("Beaker", map.GridCoords);
            var solutions = Server.System<SharedSolutionContainerSystem>();
            var paperSystem = Server.System<PaperSystem>();
            var slots = Server.System<Content.Shared.Containers.ItemSlots.ItemSlotsSystem>();

            Assert.That(solutions.TryGetSolution(pill, "food", out var dose), Is.True);
            Assert.That(solutions.TryAddReagent(dose!.Value, "CMDylovene", 3), Is.True);
            var scan = new InteractUsingEvent(user, pill, master, map.GridCoords);
            SEntMan.EventBus.RaiseLocalEvent(master, scan);
            var hands = Server.System<Content.Shared.Hands.EntitySystems.SharedHandsSystem>();
            var sheet = hands.GetActiveItem(user)!.Value;
            var printed = SEntMan.GetComponent<PaperComponent>(sheet).Content;
            Assert.That(slots.TryInsert(machine, "recipe", sheet, user), Is.True);
            Assert.That(slots.TryInsert(machine, "vessel", beaker, user), Is.True);
            Assert.That(solutions.TryGetFitsInDispenser(beaker, out _, out var output), Is.True);
            Assert.That(solutions.TryGetSolution(machine, CMUChemicalRecipeMachineComponent.StockSolutionId,
                out var stock), Is.True);
            foreach (var input in new[] { "RMCSilicon", "RMCPotassium", "RMCNitrogen" })
                Assert.That(solutions.TryAddReagent(stock!.Value, input, 1), Is.True);

            // All three ingredients are present. The sheet must not turn a consumed input into a reusable catalyst.
            paperSystem.SetContent(sheet, printed.Replace("CONSUME|RMCSilicon|", "CATALYST|RMCSilicon|"));
            Send(machine, user, new CMUChemicalRecipeMachineSynthesizeMessage());
            Assert.That(output!.Volume, Is.EqualTo(FixedPoint2.Zero));
            Assert.That(stock!.Value.Comp.Solution.Volume, Is.EqualTo(FixedPoint2.New(3)));

            paperSystem.SetContent(sheet, printed);
            Send(machine, user, new CMUChemicalRecipeMachineSynthesizeMessage());
            Assert.That(output.GetTotalPrototypeQuantity("CMDylovene"), Is.EqualTo(FixedPoint2.New(3)));
            Assert.That(stock.Value.Comp.Solution.Volume, Is.EqualTo(FixedPoint2.Zero));
        });
    }

    [Test]
    public async Task CrowbarOpensCrateIntoMachine()
    {
        var map = await Pair.CreateTestMap();
        EntityUid crate = default;
        await Server.WaitAssertion(() =>
        {
            crate = SEntMan.SpawnEntity("CMUCrateChemicalRecipeMachine", map.GridCoords);
            var crowbar = SEntMan.SpawnEntity("CMCrowbar", map.GridCoords);
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var interact = new InteractUsingEvent(user, crowbar, crate, map.GridCoords);
            SEntMan.EventBus.RaiseLocalEvent(crate, interact);
            Assert.That(interact.Handled, Is.True);
        });

        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(crate), Is.True);
            var nearby = Server.System<EntityLookupSystem>()
                .GetEntitiesInRange<CMUChemicalRecipeMachineComponent>(map.GridCoords, 0.5f);
            Assert.That(nearby, Has.Count.EqualTo(1));
        });
    }

    private void Send(EntityUid machine, EntityUid actor, BoundUserInterfaceMessage message)
    {
        message.Actor = actor;
        message.UiKey = CMUChemicalRecipeMachineUiKey.Key;
        SEntMan.EventBus.RaiseLocalEvent(machine, (object) message);
    }
}
