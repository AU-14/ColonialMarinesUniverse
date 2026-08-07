using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._CMU14.Threats;
using Content.Shared._RMC14.Intel;
using Content.Shared._RMC14.Intel.Tech;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Intel;

[TestFixture]
public sealed class CMUFactionTechPrototypeTest : GameTest
{
    private static readonly (string Console, string Tree, string Faction)[] FactionPrototypes =
    [
        ("RMCTechTreeConsoleGovfor", "RMCIntelTechTree_govfor", Team.GovFor),
        ("RMCTechTreeConsoleOpfor", "RMCIntelTechTree_opfor", Team.OpFor),
        ("RMCTechTreeConsoleCLF", "RMCIntelTechTree_clf", Team.CLF),
    ];

    private static readonly (string Console, string Faction)[] IntelConsolePrototypes =
    [
        ("RMCComputerIntelGovfor", Team.GovFor),
        ("RMCComputerIntelOpfor", Team.OpFor),
        ("RMCComputerIntelCLF", Team.CLF),
    ];

    [Test]
    public async Task FactionConsolesAndTreesAreLoadable()
    {
        var componentFactory = Server.ResolveDependency<IComponentFactory>();
        var prototypes = Server.ResolveDependency<IPrototypeManager>();

        await Server.WaitAssertion(() =>
        {
            foreach (var (consoleId, treeId, faction) in FactionPrototypes)
            {
                Assert.That(prototypes.TryIndex<EntityPrototype>(consoleId, out var consolePrototype), Is.True,
                    $"Missing faction tech console {consoleId}.");
                Assert.That(
                    consolePrototype!.TryComp<TechControlConsoleComponent>(out var console, componentFactory),
                    Is.True,
                    $"{consoleId} has no tech-control component.");
                Assert.That(console!.Team, Is.EqualTo(faction));

                AssertFactionTree(prototypes, componentFactory, treeId);
            }

            foreach (var (consoleId, faction) in IntelConsolePrototypes)
            {
                Assert.That(prototypes.TryIndex<EntityPrototype>(consoleId, out var intelConsolePrototype),
                    Is.True,
                    $"Missing faction intel-upload console {consoleId}.");
                Assert.That(
                    intelConsolePrototype!.TryComp<IntelConsoleComponent>(out var intelConsole, componentFactory),
                    Is.True,
                    $"{consoleId} has no intel-console component.");
                Assert.That(intelConsole!.Team, Is.EqualTo(faction));
            }

            AssertFactionTree(prototypes, componentFactory, "RMCIntelTechTree_ua");
        });
    }

    [Test]
    public async Task TreeOverridesAreValidated()
    {
        await Server.WaitAssertion(() =>
        {
            var intel = Server.System<IntelSystem>();
            intel.ClearTeamTechTreeOverrides();

            if (intel.TryGetTechTree(Team.OpFor, out var existing))
                SDeleteNow(existing.Value.Owner);

            intel.SetTeamTechTreeOverride(Team.OpFor, "RMCComputerIntel");
            var tree = intel.EnsureTechTree(Team.OpFor);

            try
            {
                Assert.Multiple(() =>
                {
                    Assert.That(SComp<MetaDataComponent>(tree).EntityPrototype?.ID,
                        Is.EqualTo("RMCIntelTechTree_opfor"));
                    Assert.That(tree.Comp.Tree.Options, Is.Not.Empty);
                });
            }
            finally
            {
                SDeleteNow(tree.Owner);
            }

            intel.SetTeamTechTreeOverride(Team.OpFor, "RMCIntelTechTree_ua");
            var overridden = intel.EnsureTechTree(Team.OpFor);

            try
            {
                Assert.That(SComp<MetaDataComponent>(overridden).EntityPrototype?.ID,
                    Is.EqualTo("RMCIntelTechTree_ua"));
            }
            finally
            {
                SDeleteNow(overridden.Owner);
                intel.ClearTeamTechTreeOverrides();
            }
        });
    }

    [Test]
    public async Task FactionTreesKeepIndependentStateAndRouteToMatchingConsole()
    {
        await Server.WaitAssertion(() =>
        {
            var intel = Server.System<IntelSystem>();
            var govTree = intel.EnsureTechTree(Team.GovFor);
            var opTree = intel.EnsureTechTree(Team.OpFor);
            var clfTree = intel.EnsureTechTree(Team.CLF);
            var defaultTree = intel.EnsureTechTree();
            var govConsole = SSpawn("RMCTechTreeConsoleGovfor");
            var opConsole = SSpawn("RMCTechTreeConsoleOpfor");
            var clfConsole = SSpawn("RMCTechTreeConsoleCLF");

            try
            {
                intel.AddPoints(govTree, FixedPoint2.New(11));
                intel.AddPoints(opTree, FixedPoint2.New(22));
                intel.AddPoints(clfTree, FixedPoint2.New(33));

                var govConsoleTree = SComp<TechControlConsoleComponent>(govConsole).Tree;
                var opConsoleTree = SComp<TechControlConsoleComponent>(opConsole).Tree;
                var clfConsoleTree = SComp<TechControlConsoleComponent>(clfConsole).Tree;

                Assert.Multiple(() =>
                {
                    Assert.That(govTree.Owner, Is.Not.EqualTo(opTree.Owner));
                    Assert.That(govTree.Owner, Is.Not.EqualTo(clfTree.Owner));
                    Assert.That(govTree.Owner, Is.Not.EqualTo(defaultTree.Owner));
                    Assert.That(opTree.Owner, Is.Not.EqualTo(clfTree.Owner));
                    Assert.That(opTree.Owner, Is.Not.EqualTo(defaultTree.Owner));
                    Assert.That(clfTree.Owner, Is.Not.EqualTo(defaultTree.Owner));
                    Assert.That(intel.GetIntelPoints(Team.GovFor), Is.EqualTo(11));
                    Assert.That(intel.GetIntelPoints(Team.OpFor), Is.EqualTo(22));
                    Assert.That(intel.GetIntelPoints(Team.CLF), Is.EqualTo(33));
                    Assert.That(govConsoleTree.Options, Is.SameAs(govTree.Comp.Tree.Options));
                    Assert.That(govConsoleTree.Options, Is.Not.SameAs(opTree.Comp.Tree.Options));
                    Assert.That(govConsoleTree.Options, Is.Not.SameAs(clfTree.Comp.Tree.Options));
                    Assert.That(opConsoleTree.Options, Is.SameAs(opTree.Comp.Tree.Options));
                    Assert.That(opConsoleTree.Options, Is.Not.SameAs(clfTree.Comp.Tree.Options));
                    Assert.That(clfConsoleTree.Options, Is.SameAs(clfTree.Comp.Tree.Options));
                });

                Assert.That(intel.TrySpendIntelPoints(Team.GovFor, 1), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(intel.GetIntelPoints(Team.GovFor), Is.EqualTo(10));
                    Assert.That(intel.GetIntelPoints(Team.OpFor), Is.EqualTo(22));
                    Assert.That(intel.GetIntelPoints(Team.CLF), Is.EqualTo(33));
                });
            }
            finally
            {
                SDeleteNow(govConsole);
                SDeleteNow(opConsole);
                SDeleteNow(clfConsole);
                SDeleteNow(govTree.Owner);
                SDeleteNow(opTree.Owner);
                SDeleteNow(clfTree.Owner);
            }
        });
    }

    private static void AssertFactionTree(
        IPrototypeManager prototypes,
        IComponentFactory componentFactory,
        string treeId)
    {
        Assert.That(prototypes.TryIndex<EntityPrototype>(treeId, out var treePrototype), Is.True,
            $"Missing faction tech tree {treeId}.");
        Assert.That(treePrototype!.TryComp<IntelTechTreeComponent>(out var tree, componentFactory), Is.True,
            $"{treeId} has no intel-tech-tree component.");
        Assert.That(tree!.Tree.Options, Is.Not.Empty, $"{treeId} has no purchase options.");

        foreach (var party in tree.Tree.Options.SelectMany(tier => tier)
                     .SelectMany(option => option.Events)
                     .OfType<TechPartySpawnEvent>())
        {
            Assert.That(prototypes.HasIndex<ThirdPartyPrototype>(party.ThirdPartyId), Is.True,
                $"{treeId} references missing third party {party.ThirdPartyId}.");
        }
    }
}
