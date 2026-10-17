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

    // BUG-864: relating TRF into a chem that still has ATX cancels both, the player has to see that first
    [Test]
    public async Task RelateWarnsWhenReferenceConflictsWithRemainingTargetProperty()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var simulator = SEntMan.SpawnEntity("CMUChemSimulator", map.GridCoords);
            var component = SEntMan.GetComponent<ChemSimulatorComponent>(simulator);
            var containers = Server.System<SharedContainerSystem>();
            var system = Server.System<ServerChemSimulatorSystem>();

            var target = Report(new() { ["Hypoxemic"] = 6, ["Antitoxic"] = 6, ["Ketogenic"] = 2 });
            var reference = Report(new() { ["Transformative"] = 6, ["Hemogenic"] = 1 });
            containers.Insert(target, containers.GetContainer(simulator, "target"));
            containers.Insert(reference, containers.GetContainer(simulator, "reference"));

            component.Mode = ChemSimulatorMode.Relate;
            component.TargetProperty = "Hypoxemic";
            component.ReferenceProperty = "Transformative";
            Assert.That(system.TryGetConflictWarning((simulator, component), out var warning), Is.True);
            Assert.That(warning, Does.Contain("CANCEL"));

            // swapping the conflicting property itself out is fine
            component.TargetProperty = "Antitoxic";
            Assert.That(system.TryGetConflictWarning((simulator, component), out _), Is.False);

            // no conflict at all
            component.TargetProperty = "Hypoxemic";
            component.ReferenceProperty = "Hemogenic";
            Assert.That(system.TryGetConflictWarning((simulator, component), out _), Is.False);

            // amplify never inserts anything
            component.Mode = ChemSimulatorMode.Amplify;
            component.ReferenceProperty = "Transformative";
            Assert.That(system.TryGetConflictWarning((simulator, component), out _), Is.False);

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
