using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Pair;
using Content.Server.CMU14.Yautja.BadBlood;
using Content.Shared.CMU14.Round.Objectives.Components;
using Content.Shared.CMU14.Round.Objectives.Type;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Destructible;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// FLOW-119: marines are paid for a Clan Totem they destroy, never for one that is merely deleted
[TestFixture]
public sealed class TotemObjectiveTest : GameTest
{
    private const string Totem = "CMUBadBloodTotem";
    private const string Objective = "CMUBadBloodTotemObjective";
    private const string CountingObjective = "CMUTestTotemObjectiveCounting";
    private const string Faction = "govfor";

    // the real objective completes on the first Totem, which needs a whole round around it. this one only counts
    [TestPrototypes]
    private const string Prototypes = """
- type: entity
  id: CMUTestTotemObjectiveCounting
  components:
  - type: CMUObjective
    id: cmutesttotemcounting
    objectiveDescription: test
    allowedPresets:
    - CMUTestNoPreset
    factions:
    - govfor
    objectiveLevel: 1
  - type: DestroyObjective
    targetPrototype: CMUBadBloodTotem
    spawnCount: 0
    destroyCount: 50
    requireDestruction: true
""";

    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task TotemObjectiveIsARepeatingEightPointDestroyObjective()
    {
        await Server.WaitAssertion(() =>
        {
            Assert.That(BadBloodTotemObjectiveSystem.ObjectivePrototype.Id, Is.EqualTo(Objective));

            var proto = Server.ProtoMan.Index<EntityPrototype>(Objective);
            var factory = Server.EntMan.ComponentFactory;
            Assert.That(proto.TryComp<CMUObjectiveComponent>(out var objective, factory), Is.True);
            Assert.That(proto.TryComp<DestroyObjectiveComponent>(out var destroy, factory), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(destroy!.TargetPrototype, Is.EqualTo(Totem));
                Assert.That(destroy.RequireDestruction, Is.True, "a deleted Totem must pay nobody");
                Assert.That(destroy.DestroyCount, Is.EqualTo(1), "every Totem pays");
                Assert.That(destroy.SpawnCount, Is.Zero, "the objective must not raise Totems of its own");

                Assert.That(objective!.Repeating, Is.True);
                Assert.That(objective.MaxRepeatable, Is.EqualTo(10));
                Assert.That(objective.CustomPoints, Is.EqualTo(8));
                Assert.That(objective.FactionNeutral, Is.False);
                Assert.That(objective.Factions, Does.Contain(Faction));
                Assert.That(objective.AllowedPresets, Does.Contain("DistressSignal"));
            });

            // no round, no planet: nothing to activate, and nothing breaks
            var objectives = Server.System<BadBloodTotemObjectiveSystem>();
            Assert.That(objectives.EnsureObjective(), Is.False);
            Assert.That(objectives.TryGetObjective(out _), Is.False);
        });
    }

    [Test]
    public async Task OnlyADestroyedTotemIsCounted()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var spawned = new List<EntityUid>();
        EntityUid objective = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                objective = entMan.SpawnEntity(CountingObjective, map.GridCoords);
                spawned.Add(objective);

                // a Totem the clan takes down itself, or one cleaned up, is a plain deletion
                var removed = SpawnMarkedTotem(entMan, map, objective);
                Assert.That(entMan.GetComponent<BadBloodTotemComponent>(removed).Registered, Is.True);
                entMan.DeleteEntity(removed);
                Assert.That(Credited(entMan, objective), Is.Zero, "a Totem that is merely deleted pays nothing");

                var destroyed = SpawnMarkedTotem(entMan, map, objective);
                spawned.Add(destroyed);
                Assert.That(Server.System<SharedDestructibleSystem>().DestroyEntity(destroyed), Is.True);
            });

            await Pair.RunTicksSync(5);

            await Server.WaitAssertion(() =>
            {
                Assert.That(Credited(Server.EntMan, objective), Is.EqualTo(1), "a destroyed Totem is counted exactly once");
            });
        }
        finally
        {
            await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
            await Server.WaitPost(() =>
            {
                foreach (var uid in spawned.Concat(party.All))
                {
                    if (Server.EntMan.EntityExists(uid))
                        Server.EntMan.DeleteEntity(uid);
                }
            });
        }
    }

    private static EntityUid SpawnMarkedTotem(IEntityManager entMan, TestMapData map, EntityUid objective)
    {
        var totem = entMan.SpawnEntity(Totem, map.GridCoords);
        entMan.EnsureComponent<DestroyMarkedForComponent>(totem).AssociatedObjectives[objective] = Faction;
        return totem;
    }

    private static int Credited(IEntityManager entMan, EntityUid objective)
    {
        return entMan.GetComponent<DestroyObjectiveComponent>(objective).AmountDestroyedPerFaction.GetValueOrDefault(Faction);
    }
}
