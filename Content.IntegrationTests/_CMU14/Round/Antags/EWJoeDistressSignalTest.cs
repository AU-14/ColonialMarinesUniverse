using System.Linq;
using Content.Server.CMU14.Round;
using Content.Server.CMU14.Round.Antags.EarlyWarning.Objectives;
using Content.Server.GameTicking.Presets;
using Content.Server.Mind;
using Content.Server.Objectives.Components;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.Mobs;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Systems;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.IntegrationTests._CMU14.Round.Antags;

/// <summary>
/// An Early Warning Joe an admin makes in a Distress Signal round can roll two xeno directives: maim N
/// xenofauna, and gather N of their victims in an area. Neither is offered in any other mode.
/// </summary>
[TestFixture]
public sealed class EWJoeDistressSignalTest
{
    private const string ColonyJoe = "AU14MobWorkingJoeColony";
    private const string Human = "CMMobHuman";
    private const string Xeno = "CMXenoDrone";
    private const string EWMindRole = "MindRoleEWJoe";
    private const string MaimObjective = "CMUEWJoeMaimXenoObjective";
    private const string DeliverObjective = "CMUEWJoeDeliverVictimsObjective";

    /// <summary>
    /// The real path: the Joe's damage takes a xeno past its critical threshold, RMC holds it in critical
    /// grace, and the critical that follows names nobody. It still counts.
    /// </summary>
    [Test]
    public async Task AXenoTheJoeCritsThroughGraceCounts()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var timing = server.ResolveDependency<IGameTiming>();

        EntityUid xeno = default;
        CounterConditionComponent counter = default!;
        GamePresetPrototype? previous = null;
        var auRound = entities.System<AuRoundSystem>();

        await server.WaitAssertion(() =>
        {
            var mindSys = entities.System<MindSystem>();
            var protos = server.ResolveDependency<IPrototypeManager>();
            var thresholds = entities.System<MobThresholdSystem>();

            previous = auRound.SelectedPreset;
            auRound.SetPreset(protos.Index<GamePresetPrototype>(EWJoeXenoObjectiveSystem.DistressSignalPreset));

            var joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var mind = mindSys.CreateMind(null, "Early Warning Joe");
            mindSys.TransferTo(mind, joe);
            entities.System<SharedRoleSystem>().MindAddRole(mind, EWMindRole, silent: true);
            var comp = entities.EnsureComponent<EWJoeComponent>(joe);
            Assert.That(mindSys.TryAddObjective(mind, mind.Comp, MaimObjective));
            var maim = mind.Comp.Objectives.Last();
            comp.CurrentObjectives.Add(maim);
            counter = entities.GetComponent<CounterConditionComponent>(maim);

            xeno = entities.SpawnEntity(Xeno, map.GridCoords);
            Assert.That(thresholds.TryGetThresholdForState(xeno, MobState.Critical, out var crit), "the xeno has a critical threshold");
            Assert.That(thresholds.TryGetThresholdForState(xeno, MobState.Dead, out var dead));
            var amount = crit!.Value + (dead!.Value - crit.Value) / 2;

            entities.System<DamageableSystem>().TryChangeDamage(xeno,
                new DamageSpecifier(protos.Index<DamageTypePrototype>("Blunt"), amount), ignoreResistances: true, origin: joe);
        });

        await pair.RunTicksSync((int) Math.Ceiling(3 / timing.TickPeriod.TotalSeconds));

        try
        {
            await server.WaitAssertion(() =>
            {
                Assert.That(entities.System<MobStateSystem>().IsCritical(xeno), "the xeno is down after its grace");
                Assert.That(counter.Count, Is.EqualTo(1), "and the Joe gets the credit");
            });
        }
        finally
        {
            await server.WaitPost(() => auRound.SetPreset(previous));
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task XenoDirectivesExistOnlyInDistressSignal()
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
            var mobState = entities.System<MobStateSystem>();

            var joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var mind = mindSys.CreateMind(null, "Early Warning Joe");
            mindSys.TransferTo(mind, joe);
            entities.System<SharedRoleSystem>().MindAddRole(mind, EWMindRole, silent: true);
            var comp = entities.EnsureComponent<EWJoeComponent>(joe);

            var previous = auRound.SelectedPreset;
            try
            {
                auRound.SetPreset(protos.Index<GamePresetPrototype>("ColonyFall"));
                Assert.That(xenoObjectives.IsDistressSignal(), Is.False);
                Assert.That(mindSys.TryAddObjective(mind, mind.Comp, MaimObjective), Is.False, "no xeno directives outside Distress Signal");
                Assert.That(mindSys.TryAddObjective(mind, mind.Comp, DeliverObjective), Is.False);

                auRound.SetPreset(protos.Index<GamePresetPrototype>(EWJoeXenoObjectiveSystem.DistressSignalPreset));
                Assert.That(xenoObjectives.IsDistressSignal());
                Assert.That(mindSys.TryAddObjective(mind, mind.Comp, MaimObjective), "maim is offered in Distress Signal");
                var maim = mind.Comp.Objectives.Last();
                comp.CurrentObjectives.Add(maim);
                var counter = entities.GetComponent<CounterConditionComponent>(maim);

                var first = entities.SpawnEntity(Xeno, map.GridCoords);
                var second = entities.SpawnEntity(Xeno, map.GridCoords);
                var stranger = entities.SpawnEntity(Human, map.GridCoords);
                var third = entities.SpawnEntity(Xeno, map.GridCoords);

                mobState.ChangeMobState(first, MobState.Critical, origin: joe);
                Assert.That(counter.Count, Is.EqualTo(1), "a xeno the Joe crits counts");
                mobState.ChangeMobState(first, MobState.Dead, origin: joe);
                Assert.That(counter.Count, Is.EqualTo(1), "and only once");
                mobState.ChangeMobState(second, MobState.Dead, origin: joe);
                Assert.That(counter.Count, Is.EqualTo(2), "a xeno it kills outright counts too");
                mobState.ChangeMobState(third, MobState.Critical, origin: stranger);
                Assert.That(counter.Count, Is.EqualTo(2), "someone else's work does not");

                // the deliver directive needs a powered area on the planet; the bare test map has none
                Assert.That(mindSys.TryAddObjective(mind, mind.Comp, DeliverObjective), Is.False, "no area to pin, no directive");

                // what counts as a victim
                var burst = entities.SpawnEntity(Human, map.GridCoords);
                entities.EnsureComponent<VictimBurstComponent>(burst);
                var plain = entities.SpawnEntity(Human, map.GridCoords);
                Assert.Multiple(() =>
                {
                    Assert.That(xenoObjectives.IsXenofaunaVictim(burst), "a burst body is a victim");
                    Assert.That(xenoObjectives.IsXenofaunaVictim(plain), Is.False, "an untouched one is not");
                    Assert.That(xenoObjectives.IsXenofaunaVictim(first), Is.False, "nor a xeno");
                });
            }
            finally
            {
                auRound.SetPreset(previous);
            }
        });

        await pair.CleanReturnAsync();
    }
}
