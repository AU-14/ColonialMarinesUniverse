using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Chemistry.Research;
using Content.Shared.CMU14.Chemistry.Reagents;
using Content.Shared.CMU14.Chemistry.Research;
using Content.Shared.Interaction;
using Robust.Shared.Containers;

namespace Content.IntegrationTests.CMU14.Research;

[TestFixture]
public sealed class CMUChemSimulatorReadinessTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: reagent
          parent: Water
          id: CMUTestSimulatorReagent
          class: Basic
          generated: true
        """;

    [Test]
    public async Task InsertingTargetRefreshesCostsBeforeCheckingSelectedReference()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var simulator = SEntMan.SpawnEntity("CMUChemSimulator", map.GridCoords);
            var user = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            var component = SEntMan.GetComponent<ChemSimulatorComponent>(simulator);
            var reference = Report(new() { ["Cryometabolizing"] = 1 });
            var target = Report(new() { ["Toxic"] = 1, ["Corrosive"] = 1 });
            var containers = Server.System<SharedContainerSystem>();
            containers.Insert(reference, containers.GetContainer(simulator, "reference"));
            component.Mode = ChemSimulatorMode.Add;
            component.ReferenceProperty = "Cryometabolizing";
            var research = Server.System<ServerResearchDataTerminalSystem>();
            var faction = research.GetFaction(simulator);
            var credits = research.GetCredits(faction);
            var clearance = research.GetClearance(faction);
            try
            {
                research.UpdateClearance(100, 5, faction);

                // Reinserting a target preserves the selected reference but starts with no cost table.
                var interaction = new InteractUsingEvent(user, target, simulator, map.GridCoords);
                SEntMan.EventBus.RaiseLocalEvent(simulator, interaction);
                Assert.That(containers.GetContainer(simulator, "target").ContainedEntities, Does.Contain(target));
                Assert.That(component.Ready, Is.True, component.StatusBar);
                Assert.That(component.PropertyCosts["Cryometabolizing"], Is.GreaterThan(0));
            }
            finally
            {
                research.UpdateClearance(credits, clearance, faction);
            }

            EntityUid Report(Dictionary<string, int> properties)
            {
                var report = SEntMan.SpawnEntity(null, map.GridCoords);
                var data = SEntMan.AddComponent<ResearchReportComponent>(report);
                data.Completed = true;
                data.Data = new GeneratedReagentData { ID = "CMUTestSimulatorReagent", Effects = properties };
                return report;
            }
        });
    }
}
