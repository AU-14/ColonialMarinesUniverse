using Content.Server.Destructible;
using Content.Shared.CMU14.Round.Objectives.Type;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// COUPLING-102: a destroy objective with requireDestruction only counts a real destruction, never a bare deletion
[TestFixture]
public sealed class DestroyObjectiveRequireDestructionTest
{
    private const string Faction = "govfor";

    [TestPrototypes]
    private const string Prototypes = """
- type: entity
  id: CMUTestDestroyObjectiveDefault
  components:
  - type: CMUObjective
    id: cmutestdestroydefault
    objectiveDescription: test
    allowedPresets:
    - CMUTestNoPreset
    factions:
    - govfor
    objectiveLevel: 1
  - type: DestroyObjective
    destroyCount: 5

- type: entity
  id: CMUTestDestroyObjectiveRequireDestruction
  components:
  - type: CMUObjective
    id: cmutestdestroyrequire
    objectiveDescription: test
    allowedPresets:
    - CMUTestNoPreset
    factions:
    - govfor
    objectiveLevel: 1
  - type: DestroyObjective
    destroyCount: 5
    requireDestruction: true

- type: entity
  id: CMUTestDestroyObjectiveTarget
""";

    // Flag on: a deletion pays nothing and a destruction pays once. Flag off: both pay, as before.
    [TestCase("CMUTestDestroyObjectiveRequireDestruction", 0, 1)]
    [TestCase("CMUTestDestroyObjectiveDefault", 1, 2)]
    public async Task DeletionAndDestructionCreditTheObjectiveByItsFlag(string objectiveProto, int afterDeletion, int afterDestruction)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid objective = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            objective = entMan.SpawnEntity(objectiveProto, map.GridCoords);
            entMan.DeleteEntity(SpawnMarkedTarget(entMan, map.GridCoords, objective));
            Assert.That(Credited(entMan, objective), Is.EqualTo(afterDeletion));

            var destroyed = SpawnMarkedTarget(entMan, map.GridCoords, objective);
            Assert.That(entMan.System<DestructibleSystem>().DestroyEntity(destroyed), Is.True);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(Credited(server.EntMan, objective), Is.EqualTo(afterDestruction),
                "A destroyed target must be counted exactly once.");
            server.EntMan.DeleteEntity(objective);
        });

        await pair.CleanReturnAsync();
    }

    private static EntityUid SpawnMarkedTarget(IEntityManager entMan, EntityCoordinates coords, EntityUid objective)
    {
        var target = entMan.SpawnEntity("CMUTestDestroyObjectiveTarget", coords);
        entMan.EnsureComponent<DestroyMarkedForComponent>(target).AssociatedObjectives[objective] = Faction;
        return target;
    }

    private static int Credited(IEntityManager entMan, EntityUid objective)
    {
        return entMan.GetComponent<DestroyObjectiveComponent>(objective).AmountDestroyedPerFaction.GetValueOrDefault(Faction);
    }
}
