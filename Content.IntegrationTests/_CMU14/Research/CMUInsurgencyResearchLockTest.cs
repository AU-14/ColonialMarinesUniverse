using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Chemistry.Research;
using Content.Shared.CMU14.Chemistry.Reagents;
using Content.Shared.CMU14.Chemistry.Research;
using Content.Shared.CMU14.Threats;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.IntegrationTests._CMU14.Research;

/// <summary>
/// FLOW-24: while an Insurgency rule is active, GOVFOR cannot pick contracts, buy cooldown, earn anything from
/// completed chemicals or run the simulator. Other factions are unaffected, and GOVFOR researches normally
/// without the rule.
/// </summary>
[TestFixture]
public sealed class CMUInsurgencyResearchLockTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    // Rare keeps CompleteChemical on the flat sample-data path; NoGeneration keeps it out of recipe pools.
    [TestPrototypes]
    private const string Prototypes = """
        - type: reagent
          parent: Water
          id: CMUTestInsurgencyLockReagent
          class: Rare
          flags: NoGeneration
        """;

    private const string Reagent = "CMUTestInsurgencyLockReagent";
    private static readonly string[] Factions = { "govfor", "opfor", "corporate" };

    [Test]
    public async Task InsurgencyLocksOnlyGovforResearch()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var research = SEntMan.System<ServerResearchDataTerminalSystem>();
            var simulators = SEntMan.System<ServerChemSimulatorSystem>();
            var containers = SEntMan.System<SharedContainerSystem>();
            var now = Server.ResolveDependency<IGameTiming>().CurTime;
            var proto = SProtoMan.Index<ReagentPrototype>(Reagent);

            var saved = new Dictionary<string, (int Credits, int Clearance, int Sample)>();
            foreach (var faction in Factions)
                saved[faction] = (research.GetCredits(faction), research.GetClearance(faction), research.GetSampleData(faction));

            EntityUid? rule = null;
            EntityUid? terminal = null;
            EntityUid? simulator = null;
            EntityUid? report = null;
            try
            {
                Assert.That(research.IsResearchBlocked("govfor"), Is.False, "GOVFOR must research outside Insurgency.");

                rule = StartInsurgency();
                Assert.Multiple(() =>
                {
                    Assert.That(research.IsResearchBlocked("govfor"), Is.True);
                    Assert.That(research.IsResearchBlocked("GOVFOR"), Is.True, "Faction ids compare case-insensitively.");
                    Assert.That(research.IsResearchBlocked("opfor"), Is.False);
                    Assert.That(research.IsResearchBlocked("corporate"), Is.False);
                    Assert.That(research.IsResearchBlocked("colony"), Is.False);
                });

                // Picking a contract is refused and leaves the offer and the reroll timer untouched.
                terminal = SEntMan.SpawnEntity("CMUResearchDataTerminalGovfor", map.GridCoords);
                var terminalComp = SEntMan.GetComponent<ResearchDataTerminalComponent>(terminal.Value);
                var gov = research.GetResearch("govfor");
                gov.Selectable.Add(new GeneratedReagentData { ID = "cmu-insurgency-lock-offer" });
                gov.Picked = false;
                gov.NextReroll = now + TimeSpan.FromSeconds(90);
                Entity<ResearchDataTerminalComponent> terminalEnt = (terminal.Value, terminalComp);
                Assert.That(research.PickChem("cmu-insurgency-lock-offer", terminalEnt), Is.False);
                Assert.Multiple(() =>
                {
                    Assert.That(gov.Picked, Is.False);
                    Assert.That(gov.Selectable.Any(o => o.ID == "cmu-insurgency-lock-offer"), Is.True);
                    Assert.That(gov.PickedContracts, Does.Not.Contain("cmu-insurgency-lock-offer"));
                });
                gov.Selectable.Clear();

                // Cooldown purchases: GOVFOR refused, OPFOR still works.
                foreach (var faction in new[] { "govfor", "opfor" })
                {
                    research.UpdateClearance(2, -1, faction);
                    var r = research.GetResearch(faction);
                    r.Picked = true;
                    r.NextReroll = now + TimeSpan.FromSeconds(90);
                }
                Assert.Multiple(() =>
                {
                    Assert.That(research.ReduceCooldown("govfor"), Is.False);
                    Assert.That(research.GetCredits("govfor"), Is.EqualTo(2), "A refused purchase must not charge.");
                    Assert.That(research.ReduceCooldown("opfor"), Is.True);
                    Assert.That(research.GetCredits("opfor"), Is.EqualTo(1));
                });

                // Completing a chemical pays GOVFOR nothing and does not mark it completed; others are paid.
                foreach (var faction in Factions)
                    research.UpdateClearance(0, -1, faction, 0);
                foreach (var faction in Factions)
                    research.CompleteChemical(proto, faction, null);
                Assert.Multiple(() =>
                {
                    Assert.That(research.GetSampleData("govfor"), Is.Zero);
                    Assert.That(research.GetResearch("govfor").CompletedChemicals, Does.Not.Contain(Reagent));
                    Assert.That(research.GetSampleData("opfor"), Is.EqualTo(1));
                    Assert.That(research.GetSampleData("corporate"), Is.EqualTo(1));
                });

                // The simulator refuses to start or charge for GOVFOR.
                simulator = SEntMan.SpawnEntity("CMUChemSimulator", map.GridCoords);
                var simComp = SEntMan.GetComponent<ChemSimulatorComponent>(simulator.Value);
                Entity<ChemSimulatorComponent> sim = (simulator.Value, simComp);
                report = SEntMan.SpawnEntity(null, map.GridCoords);
                var reportComp = SEntMan.AddComponent<ResearchReportComponent>(report.Value);
                reportComp.Completed = true;
                reportComp.Data = new GeneratedReagentData
                {
                    ID = "CMUTestInsurgencyLockRoot",
                    OriginalID = string.Empty,
                    Effects = new() { ["Antitoxic"] = 4, ["Anticorrosive"] = 4 },
                };
                containers.Insert(report.Value, containers.GetContainer(simulator.Value, "target"));
                simComp.Mode = ChemSimulatorMode.Amplify;
                simComp.TargetProperty = "Antitoxic";
                Assert.That(research.GetFaction(simulator.Value), Is.EqualTo("govfor"),
                    "The simulator should use the nearby GOVFOR terminal.");
                research.UpdateClearance(0, -1, "govfor", 10000);

                Assert.That(simulators.TryChargeSimulation(sim, out var refused), Is.False);
                Assert.Multiple(() =>
                {
                    Assert.That(refused, Is.Zero);
                    Assert.That(research.GetSampleData("govfor"), Is.EqualTo(10000));
                    Assert.That(simComp.StatusBar, Is.EqualTo(Loc.GetString("research-sim-status-locked")));
                });

                SEntMan.EventBus.RaiseLocalEvent(simulator.Value, new BeginChemSimulatorProcessEvent());
                Assert.That(simComp.Stage, Is.Not.EqualTo(ChemSimulatorStage.Begin), "A locked simulator must not start.");

                // Without Insurgency, GOVFOR researches normally again.
                SEntMan.DeleteEntity(rule.Value);
                rule = null;
                Assert.That(research.IsResearchBlocked("govfor"), Is.False);

                Assert.That(simulators.TryChargeSimulation(sim, out var charged), Is.True);
                Assert.That(research.GetSampleData("govfor"), Is.EqualTo(10000 - charged));

                research.UpdateClearance(0, -1, "govfor", 0);
                research.CompleteChemical(proto, "govfor", null);
                Assert.Multiple(() =>
                {
                    Assert.That(research.GetSampleData("govfor"), Is.EqualTo(1));
                    Assert.That(research.GetResearch("govfor").CompletedChemicals, Does.Contain(Reagent));
                });

                research.UpdateClearance(2, -1, "govfor");
                gov.Picked = true;
                gov.NextReroll = now + TimeSpan.FromSeconds(90);
                Assert.That(research.ReduceCooldown("govfor"), Is.True);
            }
            finally
            {
                if (rule is { } r)
                    SEntMan.DeleteEntity(r);
                if (simulator is { } s)
                    SEntMan.DeleteEntity(s);
                if (report is { } rep && SEntMan.EntityExists(rep))
                    SEntMan.DeleteEntity(rep);
                if (terminal is { } t)
                    SEntMan.DeleteEntity(t);
                foreach (var faction in Factions)
                {
                    var data = research.GetResearch(faction);
                    data.CompletedChemicals.Remove(Reagent);
                    data.Selectable.Clear();
                    data.Picked = false;
                    data.NextReroll = TimeSpan.Zero;
                    var (credits, clearance, sample) = saved[faction];
                    research.UpdateClearance(credits, clearance, faction, sample);
                }
            }
        });
    }

    private EntityUid StartInsurgency()
    {
        // A bare active rule entity: starting the real InsWinConditionsRule would also run its win checks.
        var rule = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
        SEntMan.AddComponent<GameRuleComponent>(rule);
        SEntMan.AddComponent<ActiveGameRuleComponent>(rule);
        SEntMan.AddComponent<InsurgencyRuleComponent>(rule);
        return rule;
    }
}
