#pragma warning disable RA0002 // Integration regression intentionally inspects restricted component state.

using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.Round.Objectives.Components;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Content.Shared.Research.Systems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Research;

[TestFixture]
[TestOf(typeof(SharedResearchSystem))]
public sealed class ResearchObjectiveTechnologyMergeRegressionTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: techDiscipline
  id: ResearchObjectiveMergeDiscipline
  name: research-objective-merge-discipline
  color: '#123456'
  icon:
    sprite: Interface/Misc/research_disciplines.rsi
    state: industrial
  tierPrerequisites:
    1: 0
    2: 1
    3: 1

- type: technology
  id: ResearchObjectiveMergeTarget
  name: research-technology-salvage-weapons
  icon:
    sprite: Interface/Misc/research_disciplines.rsi
    state: industrial
  discipline: ResearchObjectiveMergeDiscipline
  tier: 1

- type: technology
  id: ResearchObjectiveMergeHidden
  name: research-technology-salvage-weapons
  icon:
    sprite: Interface/Misc/research_disciplines.rsi
    state: industrial
  discipline: ResearchObjectiveMergeDiscipline
  tier: 1
  hidden: true

- type: technology
  id: ResearchObjectiveMergeTierLocked
  name: research-technology-salvage-weapons
  icon:
    sprite: Interface/Misc/research_disciplines.rsi
    state: industrial
  discipline: ResearchObjectiveMergeDiscipline
  tier: 2

- type: technology
  id: ResearchObjectiveMergePrerequisite
  name: research-technology-salvage-weapons
  icon:
    sprite: Interface/Misc/research_disciplines.rsi
    state: industrial
  discipline: ResearchObjectiveMergeDiscipline
  tier: 1
  technologyPrerequisites:
  - ResearchObjectiveMergeHidden

- type: entity
  id: ResearchObjectiveMergeDatabase
  components:
  - type: TechnologyDatabase
    supportedDisciplines:
    - ResearchObjectiveMergeDiscipline

- type: entity
  id: ResearchObjectiveMergeEmpty
  components:
  - type: CMUObjective
    id: research-objective-merge-empty
    objectiveDescription: Empty objective
    allowedPresets: [ResearchObjectiveMerge]
    factions: [govfor]
    objectiveLevel: 1

- type: entity
  id: ResearchObjectiveMergeUnrelated
  components:
  - type: CMUObjective
    id: research-objective-merge-unrelated
    objectiveDescription: Unrelated objective
    allowedPresets: [ResearchObjectiveMerge]
    factions: [govfor]
    objectiveLevel: 1
    techUnlocks:
    - ResearchObjectiveMergeTierLocked

- type: entity
  id: ResearchObjectiveMergeMatching
  components:
  - type: CMUObjective
    id: research-objective-merge-matching
    objectiveDescription: Matching objective
    allowedPresets: [ResearchObjectiveMerge]
    factions: [govfor, opfor]
    objectiveLevel: 1
    techUnlocks:
    - ResearchObjectiveMergeTarget
";

    [Test]
    public async Task ObjectiveCompletionGateComposesWithAvailabilityAndCards()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var system = Server.System<Content.Server.Research.Systems.ResearchSystem>();
            var database = SEntMan.SpawnEntity("ResearchObjectiveMergeDatabase", map.GridCoords);
            var empty = SEntMan.SpawnEntity("ResearchObjectiveMergeEmpty", map.GridCoords);
            var unrelated = SEntMan.SpawnEntity("ResearchObjectiveMergeUnrelated", map.GridCoords);
            EntityUid matchingA = default;
            EntityUid matchingB = default;

            try
            {
                var component = SEntMan.GetComponent<TechnologyDatabaseComponent>(database);
                var target = Server.ProtoMan.Index<TechnologyPrototype>("ResearchObjectiveMergeTarget");
                var hidden = Server.ProtoMan.Index<TechnologyPrototype>("ResearchObjectiveMergeHidden");
                var tierLocked = Server.ProtoMan.Index<TechnologyPrototype>("ResearchObjectiveMergeTierLocked");
                var prerequisite = Server.ProtoMan.Index<TechnologyPrototype>("ResearchObjectiveMergePrerequisite");

                Assert.That(system.IsTechnologyAvailable(component, target), Is.True,
                    "empty and unrelated objective unlock lists must not gate ordinary availability");

                matchingA = SEntMan.SpawnEntity("ResearchObjectiveMergeMatching", map.GridCoords);
                var objectiveA = SEntMan.GetComponent<CMUObjectiveComponent>(matchingA);
                objectiveA.StatusesPerFaction["govfor"] = CMUObjectiveComponent.ObjectiveStatus.Incomplete;
                Assert.That(system.IsTechnologyAvailable(component, target), Is.False,
                    "a matching incomplete objective locks the technology");

                objectiveA.StatusesPerFaction["govfor"] = CMUObjectiveComponent.ObjectiveStatus.Failed;
                Assert.That(system.IsTechnologyAvailable(component, target), Is.False,
                    "a matching failed objective also locks the technology");

                objectiveA.StatusesPerFaction["opfor"] = CMUObjectiveComponent.ObjectiveStatus.Completed;
                Assert.That(system.IsTechnologyAvailable(component, target), Is.True,
                    "one completed faction status unlocks that matching objective");

                matchingB = SEntMan.SpawnEntity("ResearchObjectiveMergeMatching", map.GridCoords);
                var objectiveB = SEntMan.GetComponent<CMUObjectiveComponent>(matchingB);
                objectiveB.StatusesPerFaction["govfor"] = CMUObjectiveComponent.ObjectiveStatus.Incomplete;
                Assert.That(system.IsTechnologyAvailable(component, target), Is.False,
                    "every matching objective must contain a completed faction status");

                objectiveB.StatusesPerFaction["govfor"] = CMUObjectiveComponent.ObjectiveStatus.Completed;
                Assert.That(system.IsTechnologyAvailable(component, target), Is.True);
                Assert.That(system.GetAvailableTechnologies(database, component).Select(tech => tech.ID),
                    Does.Contain(target.ID));

                system.UpdateTechnologyCards(database, component);
                Assert.That(component.CurrentTechnologyCards, Is.EqualTo(new[] { target.ID }),
                    "the completed objective gate must feed the technology-card selection path");

                objectiveB.StatusesPerFaction["govfor"] = CMUObjectiveComponent.ObjectiveStatus.Failed;
                system.UpdateTechnologyCards(database, component);
                Assert.Multiple(() =>
                {
                    Assert.That(system.GetAvailableTechnologies(database, component).Select(tech => tech.ID),
                        Does.Not.Contain(target.ID));
                    Assert.That(component.CurrentTechnologyCards, Is.Empty,
                        "a newly failed matching objective removes the locked card");
                });

                Assert.Multiple(() =>
                {
                    Assert.That(system.IsTechnologyAvailable(component, hidden), Is.False);
                    Assert.That(system.IsTechnologyAvailable(component, tierLocked), Is.False);
                    Assert.That(system.IsTechnologyAvailable(component, prerequisite), Is.False);
                });

                component.UnlockedTechnologies.Add(hidden.ID);
                Assert.That(system.IsTechnologyAvailable(component, prerequisite), Is.True,
                    "the upstream prerequisite gate still unlocks after its prerequisite is present");
                component.UnlockedTechnologies.Add(prerequisite.ID);
                Assert.That(system.IsTechnologyAvailable(component, prerequisite), Is.False,
                    "already-unlocked technology remains unavailable");

                component.SupportedDisciplines.Clear();
                Assert.That(system.IsTechnologyAvailable(component, target), Is.False,
                    "unsupported disciplines remain unavailable before objective evaluation");
            }
            finally
            {
                foreach (var entity in new[] { matchingB, matchingA, unrelated, empty, database })
                {
                    if (entity.Valid && SEntMan.EntityExists(entity))
                        SEntMan.DeleteEntity(entity);
                }
            }
        });
    }
}

#pragma warning restore RA0002
