// CMU14
using System.Linq;
using Content.Server.CMU14.Round;
using Content.Server.CMU14.Round.Antags.EarlyWarning.Objectives;
using Content.Server.Mind;
using Content.Server.Objectives.Components;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.CMU14.Threats;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Random;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Round.Antags;

/// <summary>
/// N8 (FLOW-03, FLOW-04, ECS-04, ARCH-03 batch 1): the Early Warning Joe's xenofauna directives follow any
/// non-humanoid threat life rather than the Distress Signal preset. They are refused until living xenofauna is
/// on the planet (RB-37: a selected threat flagged xenofauna is not enough), the maim counter credits a non-xeno xenofauna
/// body, xenofauna kills make victims, and every pool the level table walks resolves to real prototypes.
/// </summary>
[TestFixture]
public sealed class EWJoeXenofaunaObjectivesTest
{
    private const string ColonyJoe = "AU14MobWorkingJoeColony";
    private const string Human = "CMMobHuman";
    // apes carry no HumanoidProfile; the threat system stamps ThreatComponent on threat-party bodies at spawn
    private const string Ape = "CMUMobApe";
    private const string Facehugger = "CMXenoParasite";
    private const string EWMindRole = "MindRoleEWJoe";
    private const string MaimObjective = "CMUEWJoeMaimXenoObjective";
    private const string MaimEarlyObjective = "CMUEWJoeMaimXenoEarlyObjective";
    private const string FlaggedThreat = "ApeThreatCF";
    private const string HumanoidThreat = "CultistThreatCF";

    private static readonly string[] LevelPools =
    {
        "CMUEWJoeObjectivesLevel0",
        "CMUEWJoeObjectivesLevel1",
        "CMUEWJoeObjectivesLevel2",
        "CMUEWJoeObjectivesLevel3",
        "CMUEWJoeObjectivesLevel4",
    };

    [Test]
    public async Task PresenceGateRefusesWithoutXenofaunaAndAcceptsWithOne()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var mindSys = entities.System<MindSystem>();
            var auRound = entities.System<AuRoundSystem>();
            var xenoObjectives = entities.System<EWJoeXenoObjectiveSystem>();
            var protos = server.ResolveDependency<IPrototypeManager>();

            Assert.That(auRound.SelectedThreat, Is.Null, "The test server has no selected threat.");

            var joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var mind = mindSys.CreateMind(null, "Early Warning Joe");
            mindSys.TransferTo(mind, joe);
            entities.System<SharedRoleSystem>().MindAddRole(mind, EWMindRole, silent: true);
            entities.EnsureComponent<EWJoeComponent>(joe);

            try
            {
                Assert.That(xenoObjectives.XenofaunaExpected(joe), Is.False, "nothing lives here yet");
                Assert.That(mindSys.TryAddObjective(mind, mind.Comp, MaimObjective), Is.False, "no xenofauna, no maim");
                Assert.That(mindSys.TryAddObjective(mind, mind.Comp, MaimEarlyObjective), Is.False);

                // a humanoid threat is not xenofauna and its flag is unset
                auRound.SetSelectedThreat(protos.Index<ThreatPrototype>(HumanoidThreat));
                Assert.That(mindSys.TryAddObjective(mind, mind.Comp, MaimObjective), Is.False, "a cultist round alone does not open it");

                // RB-37: a flagged threat that has not spawned no longer opens it (no 10-20 min stall)
                auRound.SetSelectedThreat(protos.Index<ThreatPrototype>(FlaggedThreat));
                Assert.That(xenoObjectives.XenofaunaExpected(joe), Is.False, "the ape threat is flagged xenofauna, but none lives here yet");
                Assert.That(mindSys.TryAddObjective(mind, mind.Comp, MaimObjective), Is.False, "a selected ape threat alone does not open it");
                auRound.SetSelectedThreat(null);

                // a facehugger alone is not enough
                var hugger = entities.SpawnEntity(Facehugger, map.GridCoords);
                Assert.That(xenoObjectives.XenofaunaExpected(joe), Is.False, "parasites do not count as xenofauna presence");
                entities.DeleteEntity(hugger);

                // one living xenofauna body on the planet opens it
                var ape = entities.SpawnEntity(Ape, map.GridCoords);
                entities.EnsureComponent<ThreatComponent>(ape);
                Assert.That(EWJoeTargetRules.IsXenofauna(entities, ape), "an ape of the threat is xenofauna");
                Assert.That(mindSys.TryAddObjective(mind, mind.Comp, MaimObjective), "maim is offered once xenofauna lives here");

                // and a dead one does not
                entities.System<MobStateSystem>().ChangeMobState(ape, MobState.Dead);
                Assert.That(xenoObjectives.XenofaunaExpected(joe), Is.False, "the dead do not hold it open");
            }
            finally
            {
                auRound.SetSelectedThreat(null);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CounterCreditsNonXenoXenofaunaAndXenofaunaKillsMakeVictims()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var mindSys = entities.System<MindSystem>();
            var mobState = entities.System<MobStateSystem>();
            var xenoObjectives = entities.System<EWJoeXenoObjectiveSystem>();
            var protos = server.ResolveDependency<IPrototypeManager>();

            var joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var mind = mindSys.CreateMind(null, "Early Warning Joe");
            mindSys.TransferTo(mind, joe);
            entities.System<SharedRoleSystem>().MindAddRole(mind, EWMindRole, silent: true);
            var comp = entities.EnsureComponent<EWJoeComponent>(joe);

            var first = entities.SpawnEntity(Ape, map.GridCoords);
            entities.EnsureComponent<ThreatComponent>(first);
            var second = entities.SpawnEntity(Ape, map.GridCoords);
            entities.EnsureComponent<ThreatComponent>(second);
            var hugger = entities.SpawnEntity(Facehugger, map.GridCoords);

            Assert.That(mindSys.TryAddObjective(mind, mind.Comp, MaimObjective), "xenofauna is present");
            var maim = mind.Comp.Objectives.Last();
            comp.CurrentObjectives.Add(maim);
            var counter = entities.GetComponent<CounterConditionComponent>(maim);

            mobState.ChangeMobState(first, MobState.Critical, origin: joe);
            Assert.That(counter.Count, Is.EqualTo(1), "an ape the Joe crits counts");
            mobState.ChangeMobState(first, MobState.Dead, origin: joe);
            Assert.That(counter.Count, Is.EqualTo(1), "and only once");

            // the Joe's hit is remembered through the threat body's damage, so an origin-less crit still counts
            entities.System<DamageableSystem>().TryChangeDamage(second,
                new DamageSpecifier(protos.Index<DamageTypePrototype>("Blunt"), FixedPoint2.New(1)), ignoreResistances: true, origin: joe);
            Assert.That(entities.HasComponent<EWJoeXenoHitComponent>(second), "the hit on a threat body is remembered");
            mobState.ChangeMobState(second, MobState.Critical);
            Assert.That(counter.Count, Is.EqualTo(2), "a crit that names nobody falls back to the Joe's hit");

            mobState.ChangeMobState(hugger, MobState.Dead, origin: joe);
            Assert.That(counter.Count, Is.EqualTo(2), "a facehugger does not count");

            // victims: an organic xenofauna killed is one; one the Joe killed is not
            var mauled = entities.SpawnEntity(Human, map.GridCoords);
            var executed = entities.SpawnEntity(Human, map.GridCoords);
            var third = entities.SpawnEntity(Ape, map.GridCoords);
            entities.EnsureComponent<ThreatComponent>(third);
            mobState.ChangeMobState(mauled, MobState.Dead, origin: third);
            mobState.ChangeMobState(executed, MobState.Dead, origin: joe);
            Assert.Multiple(() =>
            {
                Assert.That(xenoObjectives.IsXenofaunaVictim(mauled), "a body the xenofauna killed is a victim");
                Assert.That(xenoObjectives.IsXenofaunaVictim(executed), Is.False, "a body the Joe killed is not");
                Assert.That(xenoObjectives.IsXenofaunaVictim(third), Is.False, "nor is the xenofauna itself");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EveryLevelPoolResolvesToExistingPrototypes()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var protos = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var level in LevelPools)
                {
                    Assert.That(protos.TryIndex<WeightedRandomPrototype>(level, out var pool), $"{level} exists");
                    if (pool == null)
                        continue;

                    Assert.That(pool.Weights, Is.Not.Empty, $"{level} lists groups");
                    foreach (var group in pool.Weights.Keys)
                    {
                        Assert.That(protos.TryIndex<WeightedRandomPrototype>(group, out var groupProto), $"{level} names group {group}, which exists");
                        if (groupProto == null)
                            continue;

                        foreach (var objective in groupProto.Weights.Keys)
                        {
                            Assert.That(protos.HasIndex<EntityPrototype>(objective), $"{group} names objective {objective}, which exists");
                        }
                    }
                }

                Assert.That(protos.HasIndex<EntityPrototype>("CMUEWJoeCollectDisksObjective"), Is.False, "the disk directive is pruned");

                // ECS-04: the early maim (1-2) sits in levels 0-1, the full one (2-3) from level 2
                var early = protos.Index<WeightedRandomPrototype>("CMUEWJoeObjectiveGroupXenoEarly");
                var late = protos.Index<WeightedRandomPrototype>("CMUEWJoeObjectiveGroupXeno");
                Assert.That(early.Weights.ContainsKey(MaimEarlyObjective));
                Assert.That(late.Weights.ContainsKey(MaimObjective));
                Assert.That(protos.Index<WeightedRandomPrototype>(LevelPools[0]).Weights.ContainsKey("CMUEWJoeObjectiveGroupXenoEarly"));
                Assert.That(protos.Index<WeightedRandomPrototype>(LevelPools[2]).Weights.ContainsKey("CMUEWJoeObjectiveGroupXeno"));

                // the threat flag: xenofauna threats only
                foreach (var id in new[] { "XenoThreat", "XenoThreatCF", "ApeThreatCF", "BiomorphsThreatCF", "NeomorphsThreat", "NeomorphsThreatCF", "WendigoThreatCF" })
                {
                    Assert.That(protos.Index<ThreatPrototype>(id).Xenofauna, $"{id} is flagged xenofauna");
                }

                foreach (var id in new[] { "CultistThreatCF", "TribalsThreatCF", "BadBloodClanThreat" })
                {
                    Assert.That(protos.Index<ThreatPrototype>(id).Xenofauna, Is.False, $"{id} is not xenofauna");
                }
            });
        });

        await pair.CleanReturnAsync();
    }
}
