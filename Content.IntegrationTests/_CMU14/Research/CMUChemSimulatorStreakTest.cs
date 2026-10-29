using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Chemistry.Research;
using Content.Shared.CMU14.Chemistry.Reagents;
using Content.Shared.CMU14.Chemistry.Research;
using Robust.Shared.Containers;

namespace Content.IntegrationTests.CMU14.Research;

[TestFixture]
public sealed class CMUChemSimulatorStreakTest : GameTest
{
    // NoGeneration keeps the test reagent out of the contract recipe pools
    [TestPrototypes]
    private const string Prototypes = """
        - type: reagent
          parent: Water
          id: CMUTestStreakRareReagent
          class: Rare
          flags: NoGeneration
        """;

    private const string Root = "CMUTestStreakRoot";

    // STATE-02 / STATE-03: repeats cost exponentially more, and the displayed cost is the charged cost
    [Test]
    public async Task RepeatedOperationsDoubleAndChargeMatchesDisplayedCost()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var simulator = SEntMan.SpawnEntity("CMUChemSimulator", map.GridCoords);
            var component = SEntMan.GetComponent<ChemSimulatorComponent>(simulator);
            var containers = Server.System<SharedContainerSystem>();
            var system = Server.System<ServerChemSimulatorSystem>();
            var research = Server.System<ServerResearchDataTerminalSystem>();
            Entity<ChemSimulatorComponent> sim = (simulator, component);

            var target = Report(Root, string.Empty);
            containers.Insert(target, containers.GetContainer(simulator, "target"));

            var faction = research.GetFaction(simulator);
            var credits = research.GetCredits(faction);
            var clearance = research.GetClearance(faction);
            var sampleData = research.GetSampleData(faction);
            try
            {
                research.UpdateClearance(credits, clearance, faction, 10000);

                // unrepeated prices for both modes
                component.Mode = ChemSimulatorMode.Suppress;
                component.TargetProperty = "Antitoxic";
                system.RefreshCosts(sim);
                var suppressBase = new Dictionary<string, int>(component.PropertyCosts);

                component.Mode = ChemSimulatorMode.Amplify;
                system.RefreshCosts(sim);
                var amplifyBase = new Dictionary<string, int>(component.PropertyCosts);
                Assert.That(amplifyBase["Antitoxic"], Is.GreaterThanOrEqualTo(2),
                    "base cost must be above 1 for the rare discount to be visible");
                Assert.That(system.GetOperationKey(sim), Is.EqualTo(Root + ":" + ChemSimulatorMode.Amplify));

                // first run pays the base price, then the price doubles
                ChargeAndCheck(amplifyBase["Antitoxic"]);
                AssertCosts(amplifyBase, 2);

                // second run in a row pays double, then the price quadruples
                ChargeAndCheck(amplifyBase["Antitoxic"] * 2);
                AssertCosts(amplifyBase, 4);

                // a simulated child of the same chemical is the same lineage, so the streak carries
                containers.Remove(target, containers.GetContainer(simulator, "target"));
                var child = Report("TAU-0-CMUTestStreakRoot-ab", Root);
                containers.Insert(child, containers.GetContainer(simulator, "target"));
                system.RefreshCosts(sim);
                AssertCosts(amplifyBase, 4);

                // a Rare recipe pick knocks one off before the multiplier, and that is what gets charged
                component.PickedRecipeChem = "CMUTestStreakRareReagent";
                system.RefreshCosts(sim);
                var discounted = Math.Max(1, amplifyBase["Antitoxic"] - 1) * 4;
                Assert.That(component.PropertyCosts["Antitoxic"], Is.EqualTo(discounted));
                ChargeAndCheck(discounted);
                component.PickedRecipeChem = null;
                system.RefreshCosts(sim);
                AssertCosts(amplifyBase, 8);

                // another mode is a different operation and starts unrepeated
                component.Mode = ChemSimulatorMode.Suppress;
                system.RefreshCosts(sim);
                AssertCosts(suppressBase, 1);
                ChargeAndCheck(suppressBase["Antitoxic"]);

                // and running it broke the amplify streak
                component.Mode = ChemSimulatorMode.Amplify;
                system.RefreshCosts(sim);
                AssertCosts(amplifyBase, 1);
                Assert.That(component.OperationStreak, Is.EqualTo(1));
                Assert.That(component.LastOperation, Is.EqualTo(Root + ":" + ChemSimulatorMode.Suppress));
            }
            finally
            {
                research.UpdateClearance(credits, clearance, faction, sampleData);
            }

            void ChargeAndCheck(int expected)
            {
                var displayed = component.PropertyCosts["Antitoxic"];
                Assert.That(displayed, Is.EqualTo(expected));
                var before = research.GetSampleData(faction);
                Assert.That(system.TryChargeSimulation(sim, out var charged), Is.True);
                Assert.That(charged, Is.EqualTo(displayed), "charged cost differs from the displayed cost");
                Assert.That(before - research.GetSampleData(faction), Is.EqualTo(displayed));
            }

            void AssertCosts(Dictionary<string, int> baseline, int multiplier)
            {
                Assert.That(component.PropertyCosts.Keys, Is.EquivalentTo(baseline.Keys));
                foreach (var (property, cost) in baseline)
                {
                    Assert.That(component.PropertyCosts[property], Is.EqualTo(cost * multiplier),
                        $"{property} should cost x{multiplier}");
                }
            }

            EntityUid Report(string id, string originalId)
            {
                var report = SEntMan.SpawnEntity(null, map.GridCoords);
                var data = SEntMan.AddComponent<ResearchReportComponent>(report);
                data.Completed = true;
                data.Data = new GeneratedReagentData
                {
                    ID = id,
                    OriginalID = originalId,
                    Effects = new() { ["Antitoxic"] = 4, ["Anticorrosive"] = 4 },
                };
                return report;
            }
        });
    }
}
