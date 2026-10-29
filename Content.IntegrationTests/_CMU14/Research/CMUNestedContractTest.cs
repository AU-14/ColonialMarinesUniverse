using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Chemistry.Reagents;
using Content.Server.CMU14.Chemistry.Research;
using Content.Shared.CMU14.Chemistry.Reagents;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Research;

/// <summary>
/// ARCH-02 / ARCH-03 / FLOW-02: Hard contracts can nest another contract chemical as a capped reactant, the
/// nested chemical is registered before its parent, the contract paper and the terminal's Contracts tab share
/// one formatter, and completing a contract pays exactly one sample data (two for Hard).
/// </summary>
[TestFixture]
public sealed class CMUNestedContractTest : GameTest
{
    // Generated prototypes survive entity cleanup and would change later tests' reactions.
    public override PoolSettings PoolSettings => new() { Connected = false, Destructive = true };

    private static GeneratedReagentData NewOffer(int tier, int index, string hint)
        => new()
        {
            ID = $"CMUNestedTest-T{tier}-{index}",
            Name = $"nested test {tier}-{index}",
            GenTier = tier,
            Class = ReagentClass.Ultra,
            ScanPointYield = tier switch { 1 => 3, 2 => 5, _ => 7 },
            RecipeHint = hint,
            Effects = new Dictionary<string, int> { ["Antitoxic"] = 1 },
            Recipe = [],
        };

    /// <summary>
    /// Legalizes forced-nesting Hard offers until one nests. A fallback to a plain recipe is allowed when every
    /// nesting attempt collides, but must be rare.
    /// </summary>
    private (GeneratedReagentData Parent, GeneratedReagentData Sub) LegalizeNested(string faction, int baseIndex)
    {
        var gen = SEntMan.System<ServerReagentGeneratorSystem>();
        var research = SEntMan.System<ServerResearchDataTerminalSystem>();
        var hint = gen.ChemicalGenClassesList["H1"].First();
        for (var i = 0; i < 10; i++)
        {
            var offer = NewOffer(3, baseIndex + i, hint);
            Assert.That(research.LegalizeChem(offer, faction, forceNested: true, reuseCompleted: false), Is.True,
                "a Hard offer must legalize, nested or not");
            var data = gen.ProceduralReagentData[offer.ID];
            if (string.IsNullOrEmpty(data.SubContractID))
                continue;
            return (data, gen.ProceduralReagentData[data.SubContractID]);
        }
        Assert.Fail("ten forced-nesting Hard offers all fell back to a plain recipe");
        return default;
    }

    [Test]
    public async Task HardNestedContractNeedsCappedSubChemicalLoadedFirst()
    {
        await Server.WaitAssertion(() =>
        {
            var gen = SEntMan.System<ServerReagentGeneratorSystem>();
            var seed = NewOffer(1, 0, string.Empty);
            gen.GenerateStats(ref seed); // forces Setup()
            const int max = ServerReagentGeneratorSystem.MaxPreciousMetalParts;

            var (parent, sub) = LegalizeNested("cmunestedtest", 100);
            var reagents = SProtoMan.GetInstances<ReagentPrototype>();
            Assert.Multiple(() =>
            {
                Assert.That(parent.SubContractIsNew, Is.True, "with no completed contracts, Chem A must be new");
                Assert.That(parent.Recipe.ContainsKey(sub.ID), Is.True, "Chem A must be a reactant of X");
                Assert.That(parent.Recipe[sub.ID].Item2, Is.False, "Chem A is a reactant, not a catalyst");
                Assert.That(parent.Recipe[sub.ID].Item1, Is.InRange(1, max), "Chem A is capped like a precious metal");
                Assert.That(sub.GenTier, Is.InRange(1, 2), "a new sub-contract is Easy or Intermediate");
                Assert.That(sub.SubContractID, Is.Empty, "a sub-contract never nests further");
                Assert.That(sub.Class, Is.EqualTo(ReagentClass.Ultra));
                Assert.That(sub.ID, Does.Contain("cmunestedtest"), "sub-contract IDs are faction-scoped");
                Assert.That(ServerResearchDataTerminalSystem.SubContractCollides(parent, sub), Is.False,
                    "Chem A's reactants must not overlap X's other reactants");
            });

            // A is registered before X: both prototypes and reactions exist, and A was recorded first.
            var keys = gen.ProceduralReagentData.Keys.ToList();
            Assert.Multiple(() =>
            {
                Assert.That(reagents.ContainsKey(sub.ID), Is.True, "Chem A's reagent prototype is not loaded");
                Assert.That(reagents.ContainsKey(parent.ID), Is.True, "X's reagent prototype is not loaded");
                Assert.That(keys.IndexOf(sub.ID), Is.LessThan(keys.IndexOf(parent.ID)), "Chem A must be registered before X");
                Assert.That(SProtoMan.Index<ReactionPrototype>(sub.ID).Products.Keys.Select(k => k.Id), Does.Contain(sub.ID));
                Assert.That(SProtoMan.Index<ReactionPrototype>(parent.ID).Reactants.Keys.Select(k => k.Id), Does.Contain(sub.ID),
                    "X's loaded reaction must need Chem A");
            });

            // TAU contract chemicals are capped wherever recipes are assembled (simulator copies included).
            Assert.That(gen.IsCappedReagent(sub.ID), Is.True);
            var copied = new Dictionary<string, (int, bool)> { [sub.ID] = (7, false), ["RMCCarbon"] = (4, false) };
            gen.ClampPreciousAmounts(copied);
            Assert.That(copied, Is.EqualTo(new Dictionary<string, (int, bool)> { [sub.ID] = (max, false), ["RMCCarbon"] = (4, false) }));
        });
    }

    [Test]
    public async Task ReusedSubContractIsACompletedContract()
    {
        await Server.WaitAssertion(() =>
        {
            var gen = SEntMan.System<ServerReagentGeneratorSystem>();
            var research = SEntMan.System<ServerResearchDataTerminalSystem>();
            var seed = NewOffer(1, 0, string.Empty);
            gen.GenerateStats(ref seed);
            const string faction = "cmunestedreuse";

            var done = NewOffer(1, 300, gen.ChemicalGenClassesList["C1"].First());
            Assert.That(research.LegalizeChem(done), Is.True);
            research.GetResearch(faction).CompletedChemicals.Add(done.ID);

            var hint = gen.ChemicalGenClassesList["H1"].First();
            var nested = false;
            for (var i = 0; i < 10 && !nested; i++)
            {
                var offer = NewOffer(3, 310 + i, hint);
                Assert.That(research.LegalizeChem(offer, faction, forceNested: true, reuseCompleted: true), Is.True);
                var data = gen.ProceduralReagentData[offer.ID];
                if (string.IsNullOrEmpty(data.SubContractID))
                    continue;
                nested = true;
                Assert.Multiple(() =>
                {
                    Assert.That(data.SubContractID, Is.EqualTo(done.ID), "the only completed contract must be reused");
                    Assert.That(data.SubContractIsNew, Is.False);
                    Assert.That(data.Recipe[done.ID].Item1, Is.LessThanOrEqualTo(ServerReagentGeneratorSystem.MaxPreciousMetalParts));
                });
            }
            Assert.That(nested, Is.True, "ten forced-nesting Hard offers all fell back to a plain recipe");
        });
    }

    [Test]
    public async Task ContractPaperAndTabShareTheFormatter()
    {
        await Server.WaitAssertion(() =>
        {
            var gen = SEntMan.System<ServerReagentGeneratorSystem>();
            var research = SEntMan.System<ServerResearchDataTerminalSystem>();
            var seed = NewOffer(1, 0, string.Empty);
            gen.GenerateStats(ref seed);
            var (parent, sub) = LegalizeNested("cmunestedpaper", 200);
            var reagents = SProtoMan.GetInstances<ReagentPrototype>();

            var paper = research.FormatContract(parent, false, "C123");
            var tab = research.FormatContract(parent, true);
            var subHeader = Loc.GetString("research-report-subcontract-header", ("NAME", sub.Name));
            var footer = Loc.GetString("cmu-paper-contract-footer");
            var propertiesHeader = Loc.GetString("admin-chemical-contract-properties-header");

            Assert.Multiple(() =>
            {
                Assert.That(paper, Does.Contain(subHeader), "a new sub-contract's section is printed on the contract");
                foreach (var (reagent, (amount, _)) in sub.Recipe)
                {
                    var line = Loc.GetString("research-report-ingredient", ("AMOUNT", amount),
                        ("NAME", reagents[reagent].LocalizedName));
                    Assert.That(paper, Does.Contain(line), $"Chem A's reactant {reagent} is missing from the contract");
                }
                Assert.That(paper, Does.Contain(Loc.GetString("research-report-ingredient",
                        ("AMOUNT", parent.Recipe[sub.ID].Item1), ("NAME", reagents[sub.ID].LocalizedName))),
                    "X's recipe must list Chem A");
                Assert.That(paper, Does.Not.Contain(propertiesHeader), "player contracts keep hiding their properties");
                Assert.That(tab, Does.Contain(propertiesHeader), "the Contracts tab lists properties");
                Assert.That(tab, Does.Not.Contain(footer), "the tab entry has no paper footer");
            });

            // The tab and the paper render the sub-contract section with the same code.
            var start = paper.IndexOf(subHeader, StringComparison.Ordinal);
            var end = paper.IndexOf(footer, StringComparison.Ordinal);
            Assert.That(end, Is.GreaterThan(start));
            Assert.That(tab, Does.Contain(paper.Substring(start, end - start)), "paper and tab sub-contract sections differ");
        });
    }

    [Test]
    public async Task PickedContractsAreTracked()
    {
        await Server.WaitAssertion(() =>
        {
            var gen = SEntMan.System<ServerReagentGeneratorSystem>();
            var research = SEntMan.System<ServerResearchDataTerminalSystem>();
            var seed = NewOffer(1, 0, string.Empty);
            gen.GenerateStats(ref seed);
            var corporate = research.GetResearch("corporate");
            var offer = NewOffer(1, 400, gen.ChemicalGenClassesList["C1"].First());
            corporate.Selectable.Add(offer);
            try
            {
                Assert.That(research.PickChem(offer.ID), Is.True);
                Assert.That(corporate.PickedContracts, Does.Contain(offer.ID));
            }
            finally
            {
                corporate.Selectable.Clear();
                corporate.Picked = false;
                corporate.NextReroll = TimeSpan.Zero;
            }
        });
    }

    [Test]
    public async Task ContractSampleDataIsOneOrTwoForHard()
    {
        await Server.WaitAssertion(() =>
        {
            var gen = SEntMan.System<ServerReagentGeneratorSystem>();
            var research = SEntMan.System<ServerResearchDataTerminalSystem>();
            var seed = NewOffer(1, 0, string.Empty);
            gen.GenerateStats(ref seed);
            const string faction = "cmunestedsample";

            var easy = NewOffer(1, 500, gen.ChemicalGenClassesList["C1"].First());
            var hard = NewOffer(3, 501, gen.ChemicalGenClassesList["H1"].First());
            // No faction: admin-style legalization never nests, which keeps this a plain per-tier check.
            Assert.That(research.LegalizeChem(easy), Is.True);
            Assert.That(research.LegalizeChem(hard), Is.True);
            var reagents = SProtoMan.GetInstances<ReagentPrototype>();

            var credits = research.GetCredits(faction);
            var sample = research.GetSampleData(faction);
            research.CompleteChemical(reagents[easy.ID], faction, null);
            Assert.Multiple(() =>
            {
                Assert.That(research.GetSampleData(faction), Is.EqualTo(sample + 1), "an Easy contract pays one sample data");
                Assert.That(research.GetCredits(faction), Is.EqualTo(credits + 3), "credits are still the full reward");
            });
            research.CompleteChemical(reagents[hard.ID], faction, null);
            Assert.Multiple(() =>
            {
                Assert.That(research.GetSampleData(faction), Is.EqualTo(sample + 3), "a Hard contract pays two sample data");
                Assert.That(research.GetCredits(faction), Is.EqualTo(credits + 10));
            });
            research.CompleteChemical(reagents[hard.ID], faction, null);
            Assert.That(research.GetSampleData(faction), Is.EqualTo(sample + 3), "a faction is paid once per chemical");

            // Static rules: contracts 1/1/2, simulator output 1, other Special chemicals keep their Reward.
            Assert.Multiple(() =>
            {
                foreach (var tier in new[] { 1, 2, 3 })
                {
                    var contract = new ReagentPrototype { GenTier = tier, Class = ReagentClass.Ultra, Generated = true, Reward = 7 };
                    Assert.That(ServerResearchDataTerminalSystem.SampleDataForCompleted(contract), Is.EqualTo(tier >= 3 ? 2 : 1));
                }
                var simulated = new ReagentPrototype { GenTier = 3, Class = ReagentClass.Rare, Generated = true, Reward = 7 };
                Assert.That(ServerResearchDataTerminalSystem.SampleDataForCompleted(simulated), Is.EqualTo(1));
                var xeno = new ReagentPrototype { Class = ReagentClass.Special, Reward = 5 };
                Assert.That(ServerResearchDataTerminalSystem.SampleDataForCompleted(xeno), Is.EqualTo(5));
            });
        });
    }

    [Test]
    public async Task CompletingANestedContractIdentifiesItsSubChemical()
    {
        await Server.WaitAssertion(() =>
        {
            var gen = SEntMan.System<ServerReagentGeneratorSystem>();
            var research = SEntMan.System<ServerResearchDataTerminalSystem>();
            var seed = NewOffer(1, 0, string.Empty);
            gen.GenerateStats(ref seed);
            const string faction = "cmunestedcomplete";
            var (parent, sub) = LegalizeNested(faction, 600);
            var reagents = SProtoMan.GetInstances<ReagentPrototype>();

            var sample = research.GetSampleData(faction);
            research.CompleteChemical(reagents[parent.ID], faction, null);
            Assert.Multiple(() =>
            {
                Assert.That(gen.IdentifiedChemicals.ContainsKey(sub.ID), Is.True, "X's report would show Chem A as an unknown emission");
                Assert.That(research.GetSampleData(faction), Is.EqualTo(sample + 2), "X is a Hard contract");
                Assert.That(research.GetResearch(faction).CompletedChemicals, Does.Not.Contain(sub.ID),
                    "completing X identifies Chem A but does not complete it for the faction");
            });

            // Scanning Chem A on its own completes it under the contract rule for its own tier.
            research.CompleteChemical(reagents[sub.ID], faction, null);
            Assert.That(research.GetSampleData(faction), Is.EqualTo(sample + 3), "a sub-contract pays one sample data");
        });
    }
}
