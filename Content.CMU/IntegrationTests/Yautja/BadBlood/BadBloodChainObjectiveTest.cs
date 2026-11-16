using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Round.Objectives;
using Content.Server.CMU14.Yautja.BadBlood;
using Content.Shared.CMU14.Round.Objectives.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// COUPLING-501: the Bad Blood chain shows as four GOVFOR objectives that complete from the chain's stage events, and win
// points never activate a final (level 3) objective while the clan's rule runs
[TestFixture]
public sealed class BadBloodChainObjectiveTest : GameTest
{
    private const string Faction = "govfor";
    private const string FinalObjective = "CMUTestBadBloodFinalObjective";

    // a final objective that win points would otherwise switch on
    [TestPrototypes]
    private const string Prototypes = """
- type: entity
  id: CMUTestBadBloodFinalObjective
  components:
  - type: CMUObjective
    id: cmutestbadbloodfinal
    objectiveDescription: test final
    allowedPresets:
    - DistressSignal
    factions:
    - govfor
    objectiveLevel: 3
    finalType: InstantWin
""";

    private static readonly (BadBloodObjectiveStage Stage, EntProtoId Objective)[] Stages =
    [
        (BadBloodObjectiveStage.GateMet, BadBloodChainObjectiveSystem.GateObjective),
        (BadBloodObjectiveStage.Signature, BadBloodChainObjectiveSystem.SignatureObjective),
        (BadBloodObjectiveStage.Uplink, BadBloodChainObjectiveSystem.UplinkObjective),
        (BadBloodObjectiveStage.StrikeStarted, BadBloodChainObjectiveSystem.StrikeObjective),
    ];

    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task ChainObjectivesCompleteAndNoFinalObjectiveActivates()
    {
        var map = await Pair.CreateTestMap();
        var spawned = new List<EntityUid>();
        EntityUid final = default;

        // the test map stands in for the round's planet, with an objective master on it
        await Server.WaitPost(() =>
        {
            var entMan = Server.EntMan;
            var objectives = Server.System<ObjectiveControlSystem>();
            SetField(objectives, "_planetMapId", map.MapId);

            var master = entMan.SpawnEntity(null, map.GridCoords);
            entMan.AddComponent<CMUObjectiveMasterComponent>(master);
            spawned.Add(master);
            SetField(objectives, "_objectiveMasterUid", master);

            final = entMan.SpawnEntity(FinalObjective, map.GridCoords);
            spawned.Add(final);
        });

        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);

        try
        {
            await Server.WaitAssertion(() =>
            {
                var objectives = Server.System<ObjectiveControlSystem>();
                var chain = Server.System<BadBloodChainObjectiveSystem>();
                Assert.That(party.Rule, Is.Not.Null, "the Bad Blood rule must start");
                Assert.That(objectives.IsFinalObjectiveSuppressed(Faction), Is.True,
                    "the rule start suppresses GOVFOR's final objective");

                Assert.Multiple(() =>
                {
                    foreach (var (_, proto) in Stages)
                    {
                        Assert.That(chain.TryGetObjective(proto, out var objective), Is.True, $"{proto} is on the planet");
                        if (objective.Owner == default)
                            continue;

                        var comp = objective.Comp;
                        Assert.That(comp.Active, Is.True, $"{proto} is switched on");
                        Assert.That(comp.Faction, Is.EqualTo(Faction));
                        Assert.That(comp.ObjectiveLevel, Is.EqualTo(1));
                        Assert.That(comp.FinalType, Is.EqualTo(CMUObjectiveComponent.FinalObjectiveType.Boon));
                        Assert.That(comp.StatusesPerFaction.GetValueOrDefault(Faction),
                            Is.EqualTo(CMUObjectiveComponent.ObjectiveStatus.Incomplete));
                    }
                });

                var (pointsBefore, _) = objectives.GetWinPointsExact(Faction);

                // a stage with no objective changes nothing
                var started = new BadBloodObjectiveStageEvent(BadBloodObjectiveStage.GatePart, "test");
                Server.EntMan.EventBus.RaiseEvent(EventSource.Local, ref started);
                foreach (var (_, proto) in Stages)
                    Assert.That(chain.IsObjectiveCompleted(proto), Is.False);

                foreach (var (stage, proto) in Stages)
                {
                    var ev = new BadBloodObjectiveStageEvent(stage, "test");
                    Server.EntMan.EventBus.RaiseEvent(EventSource.Local, ref ev);
                    Assert.That(chain.IsObjectiveCompleted(proto), Is.True, $"{stage} completes {proto}");
                }

                var (pointsAfter, _) = objectives.GetWinPointsExact(Faction);
                Assert.That(pointsAfter, Is.EqualTo(pointsBefore), "the chain objectives pay no points");

                // well past the 100 points that would hand GOVFOR a final objective
                objectives.AwardRawPointsToFaction(Faction, 150);
                Assert.Multiple(() =>
                {
                    Assert.That(Server.EntMan.GetComponent<CMUObjectiveComponent>(final).Active, Is.False,
                        "no final objective activates while the Bad Blood rule runs");
                    Assert.That(objectives.IsWinActive, Is.False);
                });
            });
        }
        finally
        {
            await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
            await Server.WaitPost(() =>
            {
                var objectives = Server.System<ObjectiveControlSystem>();
                SetField(objectives, "_planetMapId", MapId.Nullspace);
                SetField(objectives, "_objectiveMasterUid", EntityUid.Invalid);

                foreach (var uid in spawned.Concat(party.All))
                {
                    if (Server.EntMan.EntityExists(uid))
                        Server.EntMan.DeleteEntity(uid);
                }
            });
        }
    }

    private static void SetField(ObjectiveControlSystem system, string name, object value)
    {
        typeof(ObjectiveControlSystem).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(system, value);
    }
}
