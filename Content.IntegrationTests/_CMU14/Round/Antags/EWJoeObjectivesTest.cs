using System.Linq;
using Content.Server.CMU14.Round;
using Content.Server.CMU14.Round.Antags.EarlyWarning;
using Content.Server.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Server.CMU14.Round.Antags.EarlyWarning.Objectives;
using Content.Server.Mind;
using Content.Server.Objectives;
using Content.Server.Objectives.Components;
using Content.Server.Objectives.Systems;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Presets;
using Content.Shared.CMU14.Threats;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Objectives.Components;
using Content.Shared.Roles;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Round.Antags;

/// <summary>
/// Early Warning Joe phase E1: each level offers three objectives from its pool, two completions
/// advance the level and withdraw the leftover (D8), a Colony Fall completion before the threat is
/// selected counts double (A-17), organic kills that resolve to the Joe feed the murder counter, and
/// the assimilate objective pins one console and finishes on the assimilated marker.
/// </summary>
[TestFixture]
public sealed class EWJoeObjectivesTest
{
    private const string ColonyJoe = "AU14MobWorkingJoeColony";
    private const string Human = "MobHuman";
    private const string Revolver = "RMCWeaponRevolverM44";
    private const string BudgetConsole = "AUbudgetConsole";
    private const string EWMindRole = "MindRoleEWJoe";
    private const string KillCountObjective = "CMUEWJoeKillCountObjective";
    private const string AssimilateObjective = "CMUEWJoeAssimilateConsoleObjective";
    private const string GrandMachineIssuer = "GrandMachine";
    // the collect mechanics (pick up once, spent never again) on guns, which every test map can spawn;
    // the real firearms directive is an Offer objective now
    private const string FirearmsObjective = "CMUEWJoeTestCollectGunsObjective";
    private const string PowerCellsObjective = "CMUEWJoeCollectPowerCellsObjective";
    private const string PowerCell = "RMCPowerCell";
    private const string ColonyFallThreat = "ApeThreatCF";

    [TestPrototypes]
    private const string Prototypes = """
- type: entity
  parent: CMUEWJoeBaseCollectObjective
  id: CMUEWJoeTestCollectGunsObjective
  components:
  - type: EWJoeCollectCondition
    min: 2
    max: 3
    title: cmu-ew-joe-objective-collect-intel-title
    description: cmu-ew-joe-objective-collect-intel-description
    whitelist:
      components:
      - Gun
""";

    /// <summary>
    /// Playtest fixes: an item counts once the Joe has picked it up even if it is put down again
    /// (rifles cannot be bagged), and items that paid for a credited collect objective never count
    /// toward a later one, in progress or in the existence check at assignment.
    /// </summary>
    [Test]
    public async Task CollectCountsObtainedOnceAndSpentItemsNeverAgain()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var mindSys = entities.System<MindSystem>();
            var roles = entities.System<SharedRoleSystem>();
            var objectives = entities.System<ObjectivesSystem>();
            var ladder = entities.System<EWJoeObjectiveSystem>();
            var hands = entities.System<SharedHandsSystem>();

            var joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var mind = mindSys.CreateMind(null, "Early Warning Joe");
            mindSys.TransferTo(mind, joe);
            roles.MindAddRole(mind, EWMindRole, silent: true);
            var comp = entities.EnsureComponent<EWJoeComponent>(joe);

            // exactly two firearms exist, so the objective (min 2, max 3) clamps to 2
            var revolverA = entities.SpawnEntity(Revolver, map.GridCoords);
            var revolverB = entities.SpawnEntity(Revolver, map.GridCoords);

            // the loadout fills a hand; both hands must be free for the two-gun section below
            foreach (var held in hands.EnumerateHeld(joe).ToArray())
                hands.TryDrop(joe, held);

            Assert.That(objectives.TryCreateObjective((mind, mind.Comp), FirearmsObjective, out var first), "first firearms objective");
            mindSys.AddObjective(mind, mind.Comp, first!.Value);
            comp.CurrentObjectives.Add(first.Value);
            // a second, unfinished objective on offer so one completion does not advance the level
            Assert.That(mindSys.TryAddObjective(mind, mind.Comp, KillCountObjective));
            comp.CurrentObjectives.Add(mind.Comp.Objectives.Last());
            var condition = entities.GetComponent<EWJoeCollectConditionComponent>(first.Value);
            Assert.That(condition.Target, Is.EqualTo(2), "target clamped to what exists");

            // pick up and put down: acquisition counts, possession is not required
            Assert.That(hands.TryPickupAnyHand(joe, revolverA), "pick up A");
            Assert.That(hands.TryDrop(joe, revolverA), "drop A");
            Assert.That(objectives.GetProgress(first.Value, (mind, mind.Comp)), Is.EqualTo(0.5f).Within(0.01f), "A counts after being dropped");

            Assert.That(hands.TryPickupAnyHand(joe, revolverB), "pick up B");
            Assert.That(hands.TryDrop(joe, revolverB), "drop B");
            Assert.That(objectives.IsCompleted(first.Value, (mind, mind.Comp)), "both counted while holding neither");

            // credit spends the items; the credited objective itself keeps reading complete
            ladder.Evaluate((joe, comp));
            Assert.That(comp.Level, Is.Zero, "one of two is not enough to advance");
            Assert.That(comp.CompletedCredit.ContainsKey(first.Value), "credited");
            Assert.That(comp.SpentCollectibles, Is.EquivalentTo(new[] { revolverA, revolverB }), "both spent");
            Assert.That(objectives.IsCompleted(first.Value, (mind, mind.Comp)), "credited objective still complete after its items were spent");

            // a reroll with only spent firearms on the planet is refused
            Assert.That(objectives.TryCreateObjective((mind, mind.Comp), FirearmsObjective, out _), Is.False, "no fresh firearms: refused");

            // fresh firearms allow a reroll, and the spent ones never count toward it
            var revolverC = entities.SpawnEntity(Revolver, map.GridCoords);
            var revolverD = entities.SpawnEntity(Revolver, map.GridCoords);
            Assert.That(objectives.TryCreateObjective((mind, mind.Comp), FirearmsObjective, out var second), "second firearms objective");
            mindSys.AddObjective(mind, mind.Comp, second!.Value);
            Assert.That(entities.GetComponent<EWJoeCollectConditionComponent>(second.Value).Target, Is.EqualTo(2), "only the fresh pair exists for it");

            Assert.That(hands.TryPickupAnyHand(joe, revolverA), "hold spent A");
            Assert.That(hands.TryPickupAnyHand(joe, revolverB), "hold spent B");
            Assert.That(objectives.GetProgress(second.Value, (mind, mind.Comp)), Is.EqualTo(0f).Within(0.01f), "spent items never count again");

            Assert.That(hands.TryDrop(joe, revolverA));
            Assert.That(hands.TryPickupAnyHand(joe, revolverC), "pick up fresh C");
            Assert.That(objectives.GetProgress(second.Value, (mind, mind.Comp)), Is.EqualTo(0.5f).Within(0.01f), "a fresh item counts");
            Assert.That(entities.Deleted(revolverD), Is.False);

            // a withdrawn, unfinished collect objective also spends what was obtained for it: the level
            // advances on the two finished kill counters and the half-done firearms objective is withdrawn
            comp.CurrentObjectives.Clear();
            comp.CompletedCredit.Clear();
            comp.CurrentObjectives.Add(second.Value);
            var counter = entities.System<CounterConditionSystem>();
            for (var i = 0; i < 2; i++)
            {
                Assert.That(mindSys.TryAddObjective(mind, mind.Comp, KillCountObjective));
                var kill = mind.Comp.Objectives.Last();
                comp.CurrentObjectives.Add(kill);
                var target = entities.GetComponent<NumberObjectiveComponent>(kill).Target;
                for (var k = 0; k < target; k++)
                    counter.IncreaseCount(kill);
            }

            ladder.Evaluate((joe, comp));
            Assert.That(comp.Level, Is.EqualTo(1), "two kill counters advanced the level");
            Assert.That(mind.Comp.Objectives, Does.Not.Contain(second.Value), "unfinished firearms objective withdrawn");
            Assert.That(comp.SpentCollectibles, Does.Contain(revolverC), "the rifle obtained for the withdrawn objective is spent");
            Assert.That(comp.SpentCollectibles, Does.Not.Contain(revolverD), "an untouched rifle is not");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// RB-34: Tithe of Power asks for 3-5 cells, and only cells the Joe holds count: one picked up and put down
    /// again does not, and the count falls when a held cell leaves its frame.
    /// </summary>
    [Test]
    public async Task PowerCellsMustBeHeld()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var mindSys = entities.System<MindSystem>();
            var objectives = entities.System<ObjectivesSystem>();
            var hands = entities.System<SharedHandsSystem>();
            var containers = entities.System<SharedContainerSystem>();

            var joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var mind = mindSys.CreateMind(null, "Early Warning Joe");
            mindSys.TransferTo(mind, joe);
            entities.System<SharedRoleSystem>().MindAddRole(mind, EWMindRole, silent: true);
            entities.EnsureComponent<EWJoeComponent>(joe);

            foreach (var held in hands.EnumerateHeld(joe).ToArray())
                hands.TryDrop(joe, held);

            // exactly three fresh cells lie on the planet (the Joe's own are spent at assignment)
            var cells = new List<EntityUid>();
            for (var i = 0; i < 3; i++)
                cells.Add(entities.SpawnEntity(PowerCell, map.GridCoords));

            Assert.That(objectives.TryCreateObjective((mind, mind.Comp), PowerCellsObjective, out var created), "Tithe of Power");
            var objective = created!.Value;
            mindSys.AddObjective(mind, mind.Comp, objective);
            var collect = entities.GetComponent<EWJoeCollectConditionComponent>(objective);
            Assert.Multiple(() =>
            {
                Assert.That(collect.RequireHeld, "cells must be held");
                Assert.That(collect.Min, Is.EqualTo(3));
                Assert.That(collect.Max, Is.EqualTo(5));
                Assert.That(collect.Target, Is.InRange(3, 5));
            });
            // three are asked for here, whatever else the map holds
            collect.Target = 3;

            // picked up and put down: acquisition no longer counts
            Assert.That(hands.TryPickupAnyHand(joe, cells[0]), "pick up a cell");
            Assert.That(objectives.GetProgress(objective, (mind, mind.Comp)), Is.EqualTo(1f / 3f).Within(0.01f), "a held cell counts");
            Assert.That(hands.TryDrop(joe, cells[0]), "put it down");
            Assert.That(objectives.GetProgress(objective, (mind, mind.Comp)), Is.EqualTo(0f).Within(0.01f), "a dropped cell does not");

            // all three on the frame at once completes it; one leaving undoes that
            var pouch = containers.EnsureContainer<Container>(joe, "ew-test-pouch");
            foreach (var cell in cells)
                Assert.That(containers.Insert(cell, pouch), Is.True);
            Assert.That(objectives.IsCompleted(objective, (mind, mind.Comp)), "three held");

            Assert.That(containers.Remove(cells[2], pouch), Is.True);
            Assert.That(objectives.IsCompleted(objective, (mind, mind.Comp)), Is.False, "two held is not enough");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// RB-36: Colony Fall's double credit outlasts the threat vote. It ends when the threat arrives (its
    /// win-condition rules are added in the same callback that spawns its bodies), not when it is selected.
    /// </summary>
    [Test]
    public async Task ColonyFallDoubleCreditLastsUntilTheThreatArrives()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var system = entities.System<EWJoeObjectiveSystem>();
            var auRound = entities.System<AuRoundSystem>();
            var ticker = entities.System<GameTicker>();
            var joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var comp = entities.EnsureComponent<EWJoeComponent>(joe);

            var previousPreset = auRound.SelectedPreset;
            EntityUid? rule = null;
            auRound.SetPreset(protoMan.Index<GamePresetPrototype>("ColonyFall"));
            try
            {
                Assert.That(system.CreditFor(comp), Is.EqualTo(comp.PreThreatCreditMultiplier), "no threat yet: double");

                var threat = protoMan.Index<ThreatPrototype>(ColonyFallThreat);
                Assert.That(threat.WinConditions, Is.Not.Empty);
                auRound.SetSelectedThreat(threat);
                Assert.That(system.ThreatHasArrived(), Is.False, "selected, but not spawned");
                Assert.That(system.CreditFor(comp), Is.EqualTo(comp.PreThreatCreditMultiplier), "the vote alone does not end the window");

                rule = ticker.AddGameRule(threat.WinConditions[0]);
                Assert.That(system.ThreatHasArrived(), "its win condition is in play: it has arrived");
                Assert.That(system.CreditFor(comp), Is.EqualTo(1), "the colonist-only window is over");
            }
            finally
            {
                if (rule is { } ruleEntity)
                    entities.DeleteEntity(ruleEntity);
                auRound.SetSelectedThreat(null);
                auRound.SetPreset(previousPreset);
                entities.DeleteEntity(joe);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LevelOffersThreeAndTwoCompletionsAdvance()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var entities = server.EntMan;
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        var spawned = new List<EntityUid>();
        var withdrawn = new List<EntityUid>();

        await server.WaitAssertion(() =>
        {
            var mindSys = entities.System<MindSystem>();
            var roles = entities.System<SharedRoleSystem>();
            var rule = entities.System<EWJoeRuleSystem>();
            var system = entities.System<EWJoeObjectiveSystem>();
            var auRound = entities.System<AuRoundSystem>();

            var joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            spawned.Add(joe);
            var mind = mindSys.CreateMind(null, "Early Warning Joe");
            mindSys.TransferTo(mind, joe);
            roles.MindAddRole(mind, EWMindRole, silent: true);

            // victims with minds so the kill-person pool can see them; guns so the firearms objective exists
            for (var i = 0; i < 3; i++)
            {
                var victim = entities.SpawnEntity(Human, map.GridCoords);
                spawned.Add(victim);
                var victimMind = mindSys.CreateMind(null, $"Victim {i}");
                mindSys.TransferTo(victimMind, victim);
            }

            for (var i = 0; i < 3; i++)
            {
                spawned.Add(entities.SpawnEntity(Revolver, map.GridCoords));
            }

            var console = entities.SpawnEntity(BudgetConsole, map.GridCoords);
            spawned.Add(console);

            var comp = entities.EnsureComponent<EWJoeComponent>(joe);
            rule.ApplyLevel((joe, comp), 0);

            Assert.Multiple(() =>
            {
                Assert.That(comp.Level, Is.EqualTo(0));
                Assert.That(comp.CurrentObjectives, Has.Count.EqualTo(comp.ObjectivesPerLevel),
                    "Reaching a level offers exactly ObjectivesPerLevel objectives (D8: three offered).");
                foreach (var objective in comp.CurrentObjectives)
                {
                    Assert.That(mind.Comp.Objectives, Does.Contain(objective), "Offered objectives sit on the mind.");
                    Assert.That(entities.GetComponent<ObjectiveComponent>(objective).Issuer.Id, Is.EqualTo(GrandMachineIssuer));
                    Assert.That(entities.System<ObjectivesSystem>().GetInfo(objective, mind, mind.Comp), Is.Not.Null,
                        "Every offered objective has a title, icon and progress.");
                }
            });

            var levelZeroOffer = comp.CurrentObjectives.ToList();

            // one completion is not enough outside the Colony Fall pre-threat window
            Complete(entities, (joe, comp), mind, levelZeroOffer[0]);
            system.Evaluate((joe, comp));
            Assert.Multiple(() =>
            {
                Assert.That(comp.Level, Is.EqualTo(0), "A single completion does not advance the level.");
                Assert.That(comp.CompletedCredit[levelZeroOffer[0]], Is.EqualTo(1));
            });

            Complete(entities, (joe, comp), mind, levelZeroOffer[1]);
            system.Evaluate((joe, comp));

            withdrawn.Add(levelZeroOffer[2]);
            Assert.Multiple(() =>
            {
                Assert.That(comp.Level, Is.EqualTo(1), "Two completions advance the level (D8).");
                Assert.That(mind.Comp.Objectives, Does.Contain(levelZeroOffer[0]), "Finished objectives stay for the round-end summary.");
                Assert.That(mind.Comp.Objectives, Does.Contain(levelZeroOffer[1]));
                Assert.That(mind.Comp.Objectives, Does.Not.Contain(levelZeroOffer[2]), "The unfinished leftover is withdrawn on advance.");
                Assert.That(comp.CompletedCredit, Is.Empty, "Credit resets per level.");
                Assert.That(comp.CurrentObjectives, Is.Not.Empty, "Reaching level 1 offers the level-1 set.");
                Assert.That(comp.CurrentObjectives.Intersect(levelZeroOffer), Is.Empty, "The new offer is new objectives.");
            });

            // A-17: in Colony Fall, before the threat is selected, one completion is worth two credits
            var colonyFall = protoMan.Index<GamePresetPrototype>("ColonyFall");
            var previousPreset = auRound.SelectedPreset;
            auRound.SetPreset(colonyFall);
            try
            {
                Assert.That(auRound.SelectedThreat, Is.Null, "The test server has no selected threat.");
                Assert.That(system.CreditFor(comp), Is.EqualTo(comp.PreThreatCreditMultiplier));

                var levelOneOffer = comp.CurrentObjectives.ToList();
                Complete(entities, (joe, comp), mind, levelOneOffer[0]);
                system.Evaluate((joe, comp));
                Assert.That(comp.Level, Is.EqualTo(2),
                    "A Colony Fall completion before the threat is selected counts double (A-17), so one finishes the level.");
                withdrawn.AddRange(levelOneOffer.Skip(1));
            }
            finally
            {
                auRound.SetPreset(previousPreset);
            }

            Assert.That(system.CreditFor(comp), Is.EqualTo(1), "Outside Colony Fall a completion is worth one credit.");

            // D10: standing down withdraws the unfinished offer and keeps what was finished
            var levelTwoOffer = comp.CurrentObjectives.ToList();
            Assert.That(levelTwoOffer, Is.Not.Empty);
            entities.RemoveComponent<EWJoeComponent>(joe);
            withdrawn.AddRange(levelTwoOffer);
            Assert.Multiple(() =>
            {
                foreach (var objective in levelTwoOffer)
                {
                    Assert.That(mind.Comp.Objectives, Does.Not.Contain(objective), "Removing the antag withdraws its unfinished objectives.");
                }

                Assert.That(mind.Comp.Objectives, Does.Contain(levelZeroOffer[0]), "Finished objectives survive the stand-down.");
                Assert.That(mind.Comp.Objectives, Does.Contain(levelZeroOffer[1]));
            });
        });

        // withdrawn objective entities are deleted, not merely delisted
        await pair.RunTicksSync(2);

        await server.WaitAssertion(() =>
        {
            try
            {
                Assert.That(withdrawn, Is.Not.Empty);
                foreach (var objective in withdrawn)
                {
                    Assert.That(entities.Deleted(objective), Is.True, "Withdrawn objectives are deleted.");
                }
            }
            finally
            {
                foreach (var uid in spawned)
                {
                    entities.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task KillsResolveToTheJoeAndAssimilatePinsOneConsole()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var entities = server.EntMan;

        var spawned = new List<EntityUid>();

        await server.WaitAssertion(() =>
        {
            var mindSys = entities.System<MindSystem>();
            var roles = entities.System<SharedRoleSystem>();
            var mobState = entities.System<MobStateSystem>();
            var objectives = entities.System<ObjectivesSystem>();
            var containers = entities.System<SharedContainerSystem>();

            var joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            spawned.Add(joe);
            var mind = mindSys.CreateMind(null, "Early Warning Joe");
            mindSys.TransferTo(mind, joe);
            roles.MindAddRole(mind, EWMindRole, silent: true);
            var comp = entities.EnsureComponent<EWJoeComponent>(joe);

            Assert.That(mindSys.TryAddObjective(mind, mind.Comp, KillCountObjective), Is.True);
            var counter = mind.Comp.Objectives.Last();
            comp.CurrentObjectives.Add(counter);
            var counterComp = entities.GetComponent<CounterConditionComponent>(counter);

            EntityUid SpawnVictim(string proto)
            {
                var victim = entities.SpawnEntity(proto, map.GridCoords);
                spawned.Add(victim);
                return victim;
            }

            // a kill by the Joe's own hands
            mobState.ChangeMobState(SpawnVictim(Human), MobState.Dead, origin: joe);
            Assert.That(counterComp.Count, Is.EqualTo(1), "An organic death whose origin is the Joe counts.");

            // a kill by a weapon in the Joe's container tree
            var gun = entities.SpawnEntity(Revolver, map.GridCoords);
            spawned.Add(gun);
            var holster = containers.EnsureContainer<Container>(joe, "ew-test-holster");
            Assert.That(containers.Insert(gun, holster), Is.True);
            mobState.ChangeMobState(SpawnVictim(Human), MobState.Dead, origin: gun);
            Assert.That(counterComp.Count, Is.EqualTo(2), "A death whose origin is a weapon the Joe holds counts.");

            // a synthetic victim never counts, nor a death the Joe had no part in
            mobState.ChangeMobState(SpawnVictim(ColonyJoe), MobState.Dead, origin: joe);
            var bystander = SpawnVictim(Human);
            mobState.ChangeMobState(SpawnVictim(Human), MobState.Dead, origin: bystander);
            Assert.That(counterComp.Count, Is.EqualTo(2), "Synthetic victims and unrelated deaths do not count.");

            // assimilate: pins the only console, finishes on the marker, and a second objective finds nothing
            var console = entities.SpawnEntity(BudgetConsole, map.GridCoords);
            spawned.Add(console);
            Assert.That(mindSys.TryAddObjective(mind, mind.Comp, AssimilateObjective), Is.True, "A console on the map can be pinned.");
            var assimilate = mind.Comp.Objectives.Last();
            var condition = entities.GetComponent<EWJoeAssimilateConditionComponent>(assimilate);

            Assert.Multiple(() =>
            {
                Assert.That(condition.Target, Is.EqualTo(console));
                Assert.That(objectives.IsCompleted(assimilate, mind), Is.False);
                Assert.That(mindSys.TryAddObjective(mind, mind.Comp, AssimilateObjective), Is.False,
                    "With every console already pinned the objective is refused rather than duplicated.");
            });

            var marker = entities.EnsureComponent<EWJoeAssimilatedConsoleComponent>(console);
            marker.AssimilatedBy = bystander;
            Assert.That(objectives.IsCompleted(assimilate, mind), Is.False, "A console assimilated by someone else does not count.");

            marker.AssimilatedBy = joe;
            Assert.That(objectives.IsCompleted(assimilate, mind), Is.True, "The assimilated marker completes the objective.");

            foreach (var uid in spawned)
            {
                entities.DeleteEntity(uid);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LevelFourNeedsAConsoleAndRecoversFromALostOne()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var entities = server.EntMan;

        var spawned = new List<EntityUid>();

        await server.WaitAssertion(() =>
        {
            var mindSys = entities.System<MindSystem>();
            var roles = entities.System<SharedRoleSystem>();
            var rule = entities.System<EWJoeRuleSystem>();
            var system = entities.System<EWJoeObjectiveSystem>();

            var joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            spawned.Add(joe);
            var mind = mindSys.CreateMind(null, "Early Warning Joe");
            mindSys.TransferTo(mind, joe);
            roles.MindAddRole(mind, EWMindRole, silent: true);

            for (var i = 0; i < 3; i++)
            {
                var victim = entities.SpawnEntity(Human, map.GridCoords);
                spawned.Add(victim);
                var victimMind = mindSys.CreateMind(null, $"Victim {i}");
                mindSys.TransferTo(victimMind, victim);
            }

            for (var i = 0; i < 3; i++)
            {
                spawned.Add(entities.SpawnEntity(Revolver, map.GridCoords));
            }

            var firstConsole = entities.SpawnEntity(BudgetConsole, map.GridCoords);

            var comp = entities.EnsureComponent<EWJoeComponent>(joe);
            rule.ApplyLevel((joe, comp), 4);

            EntityUid? ConsoleObjective()
            {
                foreach (var objective in comp.CurrentObjectives)
                {
                    if (entities.HasComponent<EWJoeAssimilateConditionComponent>(objective))
                        return objective;
                }

                return null;
            }

            var forced = ConsoleObjective();
            Assert.Multiple(() =>
            {
                Assert.That(comp.Level, Is.EqualTo(4));
                Assert.That(forced, Is.Not.Null, "Level 4 always offers a console (D8).");
                Assert.That(entities.GetComponent<EWJoeAssimilateConditionComponent>(forced!.Value).Target, Is.EqualTo(firstConsole));
            });

            // finish everything except the console: credit is enough, the gate is not
            foreach (var objective in comp.CurrentObjectives.ToList())
            {
                if (objective != forced)
                    Complete(entities, (joe, comp), mind, objective);
            }

            system.Evaluate((joe, comp));
            Assert.Multiple(() =>
            {
                Assert.That(comp.CompletedCredit.Values.Sum(), Is.GreaterThanOrEqualTo(comp.CreditPerLevel));
                Assert.That(comp.Level, Is.EqualTo(4), "Level 4 cannot be left without a finished assimilation (D8).");
            });

            // the pinned console is destroyed; a new one exists; the forced objective is offered again
            entities.DeleteEntity(firstConsole);
            var secondConsole = entities.SpawnEntity(BudgetConsole, map.GridCoords);
            spawned.Add(secondConsole);
            comp.NextOfferAttempt = TimeSpan.Zero;
            system.Evaluate((joe, comp));

            var replacement = ConsoleObjective();
            Assert.Multiple(() =>
            {
                Assert.That(mind.Comp.Objectives, Does.Not.Contain(forced!.Value), "An assimilation whose console is gone is withdrawn.");
                Assert.That(replacement, Is.Not.Null, "The forced console objective is offered again.");
                Assert.That(entities.GetComponent<EWJoeAssimilateConditionComponent>(replacement!.Value).Target, Is.EqualTo(secondConsole));
                Assert.That(comp.Level, Is.EqualTo(4));
            });

            Complete(entities, (joe, comp), mind, replacement!.Value);
            system.Evaluate((joe, comp));
            Assert.Multiple(() =>
            {
                Assert.That(comp.Level, Is.EqualTo(5), "A finished assimilation completes the level-4 gate.");
                Assert.That(comp.CurrentObjectives, Is.Empty, "Melding offers nothing further.");
            });

            foreach (var uid in spawned)
            {
                entities.DeleteEntity(uid);
            }
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>Finishes one offered objective the way play would, by its condition type.</summary>
    private static void Complete(IEntityManager entities, Entity<EWJoeComponent> joe, Entity<MindComponent> mind, EntityUid objective)
    {
        if (entities.HasComponent<KillPersonConditionComponent>(objective))
        {
            var targets = entities.System<TargetObjectiveSystem>();
            Assert.That(targets.GetTarget(objective, out var targetMind), Is.True, "A kill objective has a target mind.");
            var body = entities.GetComponent<MindComponent>(targetMind!.Value).OwnedEntity;
            Assert.That(body, Is.Not.Null);
            entities.System<MobStateSystem>().ChangeMobState(body!.Value, MobState.Dead, origin: joe);
            return;
        }

        if (entities.HasComponent<EWJoeKillCounterComponent>(objective))
        {
            var target = entities.System<NumberObjectiveSystem>().GetTarget(objective);
            var counter = entities.System<CounterConditionSystem>();
            for (var i = 0; i < target; i++)
            {
                counter.IncreaseCount(objective);
            }

            return;
        }

        if (entities.TryGetComponent(objective, out EWJoeCollectConditionComponent? collect))
        {
            var containers = entities.System<SharedContainerSystem>();
            var pouch = containers.EnsureContainer<Container>(joe, "ew-test-pouch");
            var coords = entities.GetComponent<TransformComponent>(joe).Coordinates;
            // spent items no longer count, so insert enough fresh ones
            var held = entities.System<EWJoeCollectConditionSystem>().Counting(joe, collect).Count;
            for (var i = held; i < collect.Target; i++)
            {
                var item = entities.SpawnEntity(Revolver, coords);
                Assert.That(containers.Insert(item, pouch), Is.True);
            }

            return;
        }

        if (entities.TryGetComponent(objective, out EWJoeOfferConditionComponent? offer))
        {
            var offering = entities.System<EWJoeOfferSystem>();
            var coords = entities.GetComponent<TransformComponent>(joe).Coordinates;
            for (var i = offer.Offered; i < offer.Target; i++)
            {
                offering.Offer(joe, entities.SpawnEntity(Revolver, coords));
            }

            return;
        }

        if (entities.TryGetComponent(objective, out EWJoeAssimilateConditionComponent? assimilate))
        {
            Assert.That(assimilate.Target, Is.Not.Null);
            entities.EnsureComponent<EWJoeAssimilatedConsoleComponent>(assimilate.Target!.Value).AssimilatedBy = joe;
            return;
        }

        Assert.Fail($"Unknown Early Warning objective type on {entities.ToPrettyString(objective)}");
    }
}
