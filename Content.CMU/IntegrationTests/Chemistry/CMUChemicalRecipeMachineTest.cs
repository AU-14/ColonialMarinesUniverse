#pragma warning disable RA0002 // Inspect the resolved requisitions catalogs.

using System.Linq;
using Content.Client.Guidebook.Components;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared._RMC14.Vendors;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.CMU14.Chemistry;
using Content.Shared.CMU14.ColonyEconomy;
using Content.Shared.FixedPoint;
using Content.Shared.Guidebook;
using Content.Shared.Interaction;
using Content.Shared.Paper;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Chemistry;

[TestFixture]
public sealed class CMUChemicalRecipeMachineTest : GameTest
{
    [Test]
    public async Task MachineOffersMedicalMachinesGuide()
    {
        await Client.WaitAssertion(() =>
        {
            ProtoId<GuideEntryPrototype> guide = "CMUGuideMedicalMachines";
            var factory = Client.ResolveDependency<IComponentFactory>();
            var machine = CProtoMan.Index<EntityPrototype>("CMUChemicalRecipeMachine");
            Assert.That(machine.TryComp<GuideHelpComponent>(out var help, factory), Is.True);
            Assert.That(help!.Guides.First(), Is.EqualTo(guide));
            Assert.That(CProtoMan.Index(guide), Is.Not.Null);
        });
    }

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

    [TestCase("CMVendorCircuits")]
    [TestCase("CMVendorElectronics")]
    public async Task ElectronicsVendorsStockMachineBoard(string vendorId)
    {
        await Server.WaitAssertion(() =>
        {
            var vendor = Server.ProtoMan.Index<EntityPrototype>(vendorId)
                .Components.Values.Select(c => c.Component).OfType<CMAutomatedVendorComponent>().Single();
            Assert.That(vendor.Sections.SelectMany(s => s.Entries)
                .Any(e => e.Id == "CMUCircuitboardChemicalRecipeMachine" && e.Amount > 0), Is.True);
        });
    }

    [Test]
    public async Task CashCircuitVendorSellsMachineBoard()
    {
        await Server.WaitAssertion(() =>
        {
            var vendor = Server.ProtoMan.Index<EntityPrototype>("CMUCashVendorCircuits")
                .Components.Values.Select(c => c.Component).OfType<AU14CashVendorComponent>().Single();
            Assert.That(vendor.Items.Any(e => e.ItemId == "CMUCircuitboardChemicalRecipeMachine" && e.BasePrice > 0), Is.True);
        });
    }

    [TestCase("CMCargoCatalog")]
    [TestCase("USCMCargoCatalog")]
    [TestCase("UPPCargoCatalog")]
    [TestCase("BEARCargoCatalog")]
    [TestCase("LACNCargoCatalog")]
    [TestCase("WYPMCCargoCatalog")]
    [TestCase("VAIPOCargoCatalog")]
    [TestCase("HAZOPSCargoCatalog")]
    [TestCase("RMCCargoCatalog")]
    [TestCase("CMBCIUCargoCatalog")]
    [TestCase("ProdigyCargoCatalog")]
    [TestCase("AU14CorporateASRSConsole")]
    public async Task AsrsCatalogSellsMachineCrate(string catalogId)
    {
        await Server.WaitAssertion(() =>
        {
            var catalog = Server.ProtoMan.Index<EntityPrototype>(catalogId)
                .Components.Values.Select(c => c.Component).OfType<RequisitionsComputerComponent>().Single();
            Assert.That(catalog.Categories.SelectMany(c => c.Entries)
                .Any(e => e.Crate.Id == "CMUCrateChemicalRecipeMachine" && e.Cost > 0), Is.True, catalogId);
        });
    }

    [Test]
    public async Task CrowbarOpensCrateIntoMachine()
    {
        var map = await Pair.CreateTestMap();
        EntityUid crate = default;
        var initialMachines = 0;
        await Server.WaitAssertion(() =>
        {
            initialMachines = SEntMan.EntityQuery<CMUChemicalRecipeMachineComponent>().Count();
            crate = SEntMan.SpawnEntity("CMUCrateChemicalRecipeMachine", map.GridCoords);
            var crowbar = SEntMan.SpawnEntity("CMCrowbar", map.GridCoords);
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var interact = new InteractUsingEvent(user, crowbar, crate, map.GridCoords);
            SEntMan.EventBus.RaiseLocalEvent(crate, interact);
            Assert.That(interact.Handled, Is.True);
        });

        await Pair.RunSeconds(1);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(crate), Is.True);
            Assert.That(SEntMan.EntityQuery<CMUChemicalRecipeMachineComponent>().Count(), Is.EqualTo(initialMachines + 1));
        });
    }

    private void Send(EntityUid machine, EntityUid actor, BoundUserInterfaceMessage message)
    {
        message.Actor = actor;
        message.UiKey = CMUChemicalRecipeMachineUiKey.Key;
        SEntMan.EventBus.RaiseLocalEvent(machine, (object) message);
    }
}
