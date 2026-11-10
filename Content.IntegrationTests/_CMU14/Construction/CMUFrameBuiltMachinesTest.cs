using Content.IntegrationTests.Fixtures;
using System.Linq;
using Content.Server.Construction.Components;
using Content.Server.Destructible;
using Content.Server.Destructible.Thresholds.Behaviors;
using Content.Shared.Destructible;
using Content.Shared.CMU14.Crafting;
using Content.Shared.CMU14.Portable;
using Content.Shared._RMC14.Requisitions;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared.Construction.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Construction;

// ARCH-01 (GraphAudit-FrameAndTileAlignment): the chemical synthesizer and the tray arms are built on the RMC machine frame
// from a board, like the other CMU machines, not crafted from industry kits and carried.
[TestFixture]
public sealed class CMUFrameBuiltMachinesTest : GameTest
{
    private static readonly (string Machine, string Board)[] Machines =
    {
        ("CMUChemSynthesizer", "CMUChemSynthesizerMachineCircuitboard"),
        ("CMUTrayArmHarvester", "CMUTrayArmHarvesterMachineCircuitboard"),
        ("CMUTrayArmPlanter", "CMUTrayArmPlanterMachineCircuitboard"),
    };

    // The seed extractor and the cryo pod are taken apart into (and built from) the RMC machine frame too.
    [TestCase("SeedExtractor", "SeedExtractorMachineCircuitboard")]
    [TestCase("CryoPod", "CryoPodMachineCircuitboard")]
    public async Task UpstreamMachinesUseTheRmcFrame(string machineId, string boardId)
    {
        await Server.WaitAssertion(() =>
        {
            var machine = Server.ProtoMan.Index<EntityPrototype>(machineId);
            var factory = SEntMan.ComponentFactory;
            Assert.That(machine.TryComp<ConstructionComponent>(out var construction, factory), Is.True);
            Assert.That(construction!.Graph.Id, Is.EqualTo("CMMachineFrame"), $"{machineId} is still on the upstream machine frame.");
            Assert.That(construction.Node, Is.EqualTo("machine"));
            Assert.That(machine.TryComp<MachineComponent>(out var comp, factory) && comp.Board?.Id == boardId, Is.True);
        });
    }

    // Every ASRS console type (colony, corporate, GOVFOR, OPFOR) gets the Machine Boards tab from its catalog prototype.
    [TestCase("CMASRSConsoleColony")]
    [TestCase("AU14CorporateASRSConsole")]
    [TestCase("CMASRSConsoleGovfor")]
    [TestCase("CMASRSConsoleOpfor")]
    public async Task EveryConsoleTypeGetsOneMachineBoardsTab(string console)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var comp = SComp<RequisitionsComputerComponent>(SSpawnAtPosition(console, map.GridCoords));
            var protos = Server.ProtoMan;
            var factory = SEntMan.ComponentFactory;
            var tabs = comp.Categories.Where(c => c.Name == "Machine Boards").ToList();
            Assert.That(tabs, Has.Count.EqualTo(1), $"{console} has {tabs.Count} Machine Boards tabs.");
            foreach (var (board, cost) in new[]
                     {
                         ("CMUChemSynthesizerMachineCircuitboard", 60),
                         ("CMUTrayArmHarvesterMachineCircuitboard", 35),
                         ("CMUTrayArmPlanterMachineCircuitboard", 35),
                         ("SeedExtractorMachineCircuitboard", 40),
                     })
            {
                Assert.That(tabs[0].Entries.Any(e => e.Crate.Id == board && e.Cost == cost), Is.True,
                    $"{console} does not sell {board} for {cost} under Machine Boards.");
            }

            bool IsBoard(string id) => protos.Index<EntityPrototype>(id).TryComp<MachineBoardComponent>(out _, factory);
            foreach (var category in comp.Categories.Where(c => c.Name != "Machine Boards"))
            {
                foreach (var entry in category.Entries)
                {
                    var crate = protos.Index<EntityPrototype>(entry.Crate);
                    var onlyBoards = crate.TryComp<Content.Shared.Storage.Components.StorageFillComponent>(out var fill, factory)
                        && fill.Contents.Count > 0 && fill.Contents.All(c => c.PrototypeId is { } id && IsBoard(id));
                    Assert.That(IsBoard(entry.Crate.Id) || onlyBoards, Is.False,
                        $"{console} sells {entry.Crate.Id} under {category.Name}, not Machine Boards.");
                }
            }
        });
    }

    // ARCH-02/ARCH-03/ARCH-04: every ASRS sells the boards at their prices in one "Machine Boards" tab, and every machine
    // board (or crate holding only machine boards) it sells is in that tab. Colony and corporate consoles share the colony
    // catalog; each platoon catalog replaces a military console's list in the round.
    [TestCase("CMASRSConsoleColony")]
    [TestCase("AU14CorporateASRSConsole")]
    [TestCase("USCMCargoCatalog")]
    [TestCase("RMCCargoCatalog")]
    [TestCase("UPPCargoCatalog")]
    [TestCase("WYPMCCargoCatalog")]
    [TestCase("CMBCIUCargoCatalog")]
    [TestCase("HAZOPSCargoCatalog")]
    [TestCase("LACNCargoCatalog")]
    [TestCase("ProdigyCargoCatalog")]
    [TestCase("VAIPOCargoCatalog")]
    [TestCase("BEARCargoCatalog")]
    public async Task EveryAsrsSellsTheBoardsInOneTab(string catalog)
    {
        await Server.WaitAssertion(() =>
        {
            var protos = Server.ProtoMan;
            var factory = SEntMan.ComponentFactory;
            var proto = protos.Index<EntityPrototype>(catalog);
            Assert.That(proto.TryComp<RequisitionsComputerComponent>(out var computer, factory), Is.True);
            var tabs = computer!.Categories.Where(c => c.Name == "Machine Boards").ToList();
            Assert.That(tabs, Has.Count.EqualTo(1), $"{catalog} has {tabs.Count} Machine Boards tabs.");

            foreach (var (board, cost) in new[]
                     {
                         ("CMUChemSynthesizerMachineCircuitboard", 60),
                         ("CMUTrayArmHarvesterMachineCircuitboard", 35),
                         ("CMUTrayArmPlanterMachineCircuitboard", 35),
                         ("SeedExtractorMachineCircuitboard", 40),
                     })
            {
                Assert.That(tabs[0].Entries.Any(e => e.Crate.Id == board && e.Cost == cost), Is.True,
                    $"{catalog} does not sell {board} for {cost} under Machine Boards.");
            }

            bool IsBoard(string id) => protos.Index<EntityPrototype>(id).TryComp<MachineBoardComponent>(out _, factory);
            foreach (var category in computer.Categories.Where(c => c.Name != "Machine Boards"))
            {
                foreach (var entry in category.Entries)
                {
                    var crate = protos.Index<EntityPrototype>(entry.Crate);
                    var onlyBoards = crate.TryComp<Content.Shared.Storage.Components.StorageFillComponent>(out var fill, factory)
                        && fill.Contents.Count > 0 && fill.Contents.All(c => c.PrototypeId is { } id && IsBoard(id));
                    Assert.That(IsBoard(entry.Crate.Id) || onlyBoards, Is.False,
                        $"{catalog} sells {entry.Crate.Id} under {category.Name}, not Machine Boards.");
                }
            }
        });
    }

    // ARCH-03: the seed extractor board is built from stacks this fork stocks (the upstream Manipulator/Glass/Cable are not).
    [Test]
    public async Task SeedExtractorBoardUsesStockedStacks()
    {
        await Server.WaitAssertion(() =>
        {
            var board = Server.ProtoMan.Index<EntityPrototype>("SeedExtractorMachineCircuitboard");
            Assert.That(board.TryComp<MachineBoardComponent>(out var comp, SEntMan.ComponentFactory), Is.True);
            Assert.That(comp!.StackRequirements.Keys.Select(k => k.Id),
                Is.EquivalentTo(new[] { "CMSteel", "CMGlass", "CMUThinCableLV" }));
            Assert.That(comp.TagRequirements, Is.Empty);
            Assert.That(comp.ComponentRequirements, Is.Empty);
        });
    }

    [Test]
    public async Task MachinesAreBuiltOnTheRmcFrameFromTheirBoards()
    {
        await Server.WaitAssertion(() =>
        {
            var protos = Server.ProtoMan;
            var factory = SEntMan.ComponentFactory;
            foreach (var (machineId, boardId) in Machines)
            {
                var machine = protos.Index<EntityPrototype>(machineId);
                var board = protos.Index<EntityPrototype>(boardId);
                Assert.Multiple(() =>
                {
                    Assert.That(machine.TryComp<ConstructionComponent>(out var construction, factory), Is.True, $"{machineId} has no construction graph.");
                    Assert.That(construction!.Graph.Id, Is.EqualTo("CMMachineFrame"), $"{machineId} is not on the RMC machine frame graph.");
                    Assert.That(construction.Node, Is.EqualTo("machine"));
                    Assert.That(machine.TryComp<MachineComponent>(out var comp, factory) && comp.Board?.Id == boardId, Is.True,
                        $"{machineId} does not name {boardId} as its board.");
                    Assert.That(board.TryComp<MachineBoardComponent>(out var boardComp, factory) && boardComp.Prototype.Id == machineId, Is.True,
                        $"{boardId} does not build {machineId}.");
                    Assert.That(machine.TryComp<AnchorableComponent>(out _, factory), Is.True, $"{machineId} cannot be wrenched.");
                    Assert.That(machine.TryComp<PortableStructureComponent>(out _, factory), Is.False, $"{machineId} is still a carried industry structure.");
                });
            }

            foreach (var recipe in protos.EnumeratePrototypes<CraftingRecipePrototype>())
            {
                foreach (var (machineId, _) in Machines)
                    Assert.That(recipe.Output.Id, Is.Not.EqualTo(machineId), $"{machineId} can still be crafted ({recipe.ID}).");
            }
        });
    }

    // A destroyed synthesizer leaves one wreck, of the RMC frame it is built on; thresholds are inherited additively, so the
    // upstream machine's wreck must not come in through a parent.
    [Test]
    public async Task DestroyedSynthesizerLeavesOneRmcFrameWreck()
    {
        await Server.WaitAssertion(() =>
        {
            var proto = Server.ProtoMan.Index<EntityPrototype>("CMUChemSynthesizer");
            Assert.That(proto.TryComp<DestructibleComponent>(out var destructible, SEntMan.ComponentFactory), Is.True);
            var spawns = destructible!.Thresholds
                .SelectMany(t => t.Behaviors.OfType<SpawnEntitiesBehavior>())
                .SelectMany(b => b.Spawn.Keys.Select(k => k.Id))
                .ToList();
            Assert.Multiple(() =>
            {
                Assert.That(spawns, Has.Exactly(1).EqualTo("CMMachineFrameDestroyed"));
                Assert.That(spawns, Does.Not.Contain("MachineFrameDestroyed"), "It still leaves the upstream frame's wreck too.");
                Assert.That(destructible.Thresholds.Any(t => t.Behaviors.OfType<DoActsBehavior>().Any(b => b.HasAct(ThresholdActs.Destruction))),
                    Is.True, "Nothing destroys the synthesizer.");
            });
        });
    }

    [Test]
    public async Task SpawnedMachinesCarryTheirBoard()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            foreach (var (machineId, boardId) in Machines)
            {
                var machine = SSpawnAtPosition(machineId, map.GridCoords);
                var board = SComp<MachineComponent>(machine).BoardContainer.ContainedEntities;
                Assert.That(board, Has.Count.EqualTo(1), $"{machineId} has no board inside it.");
                Assert.That(SEntMan.GetComponent<MetaDataComponent>(board[0]).EntityPrototype?.ID, Is.EqualTo(boardId));
                SEntMan.DeleteEntity(machine);
            }
        });
    }
}
