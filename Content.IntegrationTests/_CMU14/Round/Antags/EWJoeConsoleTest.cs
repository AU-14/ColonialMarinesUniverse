using System.Linq;
using Content.Server.CMU14.Chemistry.Research;
using Content.Server.CMU14.ColonyEconomy;
using Content.Server.CMU14.Round.Antags.EarlyWarning;
using Content.Server.CMU14.Round.Antags.EarlyWarning.Consoles;
using Content.Server.CMU14.Round.Antags.EarlyWarning.Objectives;
using Content.Server.Mind;
using Content.Server.Objectives;
using Content.Shared.CMU14.ColonyEconomy;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Roles;
using Content.Shared.UserInterface;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.IntegrationTests._CMU14.Round.Antags;

/// <summary>
/// Phase E2: console assimilation. The action's do-after marks, locks and corrupts a console and
/// credits the pinned objective; the repair chain needs screwdriver, wirecutters and the core
/// shard in that order; the shard drops exactly once; every effect stays within the ±40% clamp.
/// </summary>
[TestFixture]
public sealed class EWJoeConsoleTest
{
    private const string ColonyJoe = "AU14MobWorkingJoeColony";
    private const string Human = "MobHuman";
    private const string BudgetConsole = "AUbudgetConsole";
    private const string CorporateConsole = "AU14CorporateConsole";
    private const string ResearchTerminal = "CMUResearchDataTerminal";
    private const string DepartmentConsole = "AUDepartmentConsoleMedical";
    private const string CommsConsole = "AUColonyCommsConsole";
    private const string Screwdriver = "Screwdriver";
    private const string Wirecutter = "Wirecutter";
    private const string Shard = "CMUEWJoeCoreShard";
    private const string IdCard = "PassengerIDCard";
    private const string EWMindRole = "MindRoleEWJoe";
    private const string AssimilateAction = "CMUActionEWJoeAssimilate";
    private const string AssimilateObjective = "CMUEWJoeAssimilateConsoleObjective";

    [Test]
    public async Task AssimilateDoAfterMarksLocksCorruptsAndCredits()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var timing = server.ResolveDependency<IGameTiming>();

        EntityUid joe = default, console = default, bystander = default, objective = default;
        Entity<Content.Shared.Mind.MindComponent> mind = default;
        var faction = "";

        await server.WaitAssertion(() =>
        {
            var rule = entities.System<EWJoeRuleSystem>();
            var mindSys = entities.System<MindSystem>();
            var objectives = entities.System<ObjectivesSystem>();
            var roles = entities.System<SharedRoleSystem>();
            var research = entities.System<ServerResearchDataTerminalSystem>();

            joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            bystander = entities.SpawnEntity(Human, map.GridCoords);
            // the research terminal has no access reader, so only the assimilation lock can refuse its UI
            console = entities.SpawnEntity(ResearchTerminal, map.GridCoords);
            faction = entities.GetComponent<Content.Shared.CMU14.Chemistry.Research.ResearchDataTerminalComponent>(console).Faction;
            research.UpdateSampleData(100, faction);

            mind = mindSys.CreateMind(null, "Early Warning Joe");
            mindSys.TransferTo(mind, joe);
            roles.MindAddRole(mind, EWMindRole, silent: true);

            var comp = entities.EnsureComponent<EWJoeComponent>(joe);

            // the objective pins the only console on the map before it is touched; it is created before
            // the level-up so the level-1 pool offer cannot take the console first
            Assert.That(objectives.TryCreateObjective((mind, mind.Comp), AssimilateObjective, out var created), "objective created");
            objective = created!.Value;
            mindSys.AddObjective(mind, mind.Comp, objective);
            Assert.That(entities.GetComponent<EWJoeAssimilateConditionComponent>(objective).Target, Is.EqualTo(console));

            rule.ApplyLevel((joe, comp), 1);
            Assert.That(comp.GrantedActions.Any(a => entities.GetComponent<MetaDataComponent>(a).EntityPrototype?.ID == AssimilateAction),
                "level 1 grants the assimilate action");

            // playtest 4: a console of the right type that no objective names is refused. Spawned after the
            // level-up, so the level-1 offer cannot have pinned it.
            var unpinned = entities.SpawnEntity(ResearchTerminal, map.GridCoords);
            var consoleSys = entities.System<EWJoeConsoleSystem>();
            Assert.That(consoleSys.CanAssimilate((joe, comp), unpinned, out var unpinnedReason), Is.False, "an unpinned console is refused");
            Assert.That(unpinnedReason, Is.EqualTo("cmu-ew-joe-assimilate-not-called"));
            var refused = new EWJoeAssimilateActionEvent { Performer = joe, Target = unpinned };
            entities.EventBus.RaiseLocalEvent(joe, refused);
            Assert.That(refused.Handled, Is.False, "the action starts nothing on it");
            Assert.That(ActiveDoAfters(entities, joe), Is.Zero);
            Assert.That(consoleSys.CanAssimilate((joe, comp), console, out _), "the pinned console is accepted");
            Assert.That(objectives.IsCompleted(objective, (mind, mind.Comp)), Is.False, "not complete before assimilation");

            var open = new ActivatableUIOpenAttemptEvent(bystander, false);
            entities.EventBus.RaiseLocalEvent(console, open);
            Assert.That(open.Cancelled, Is.False, "UI opens before assimilation");

            var ev = new EWJoeAssimilateActionEvent { Performer = joe, Target = console };
            entities.EventBus.RaiseLocalEvent(joe, ev);
            Assert.That(ev.Handled, "action handled");
            Assert.That(ActiveDoAfters(entities, joe), Is.EqualTo(1), "one do-after running");
            Assert.That(entities.HasComponent<EWJoeAssimilatedConsoleComponent>(console), Is.False, "not marked before the do-after ends");
        });

        var ticks = (int) Math.Ceiling(9.5 / timing.TickPeriod.TotalSeconds);
        await pair.RunTicksSync(ticks);

        await server.WaitAssertion(() =>
        {
            var consoles = entities.System<EWJoeConsoleSystem>();
            var objectives = entities.System<ObjectivesSystem>();
            var research = entities.System<ServerResearchDataTerminalSystem>();
            var comp = entities.GetComponent<EWJoeComponent>(joe);

            Assert.Multiple(() =>
            {
                Assert.That(entities.TryGetComponent(console, out EWJoeAssimilatedConsoleComponent? marker), "console marked");
                Assert.That(marker!.AssimilatedBy, Is.EqualTo(joe));
                Assert.That(marker.RepairStep, Is.Zero);
                Assert.That(marker.Effect is { } fx && entities.EntityExists(fx) && entities.GetComponent<TransformComponent>(fx).ParentUid == console,
                    "corruption effect spawned on the console");
                Assert.That(comp.ConsolesAssimilated, Is.EqualTo(1));
                Assert.That(research.GetSampleData(faction), Is.InRange(60, 140), "sample data within ±40%");
                Assert.That(objectives.IsCompleted(objective, (mind, mind.Comp)), "objective credited");
            });

            // the lock refuses the UI to anyone
            var attempt = new ActivatableUIOpenAttemptEvent(bystander, false);
            entities.EventBus.RaiseLocalEvent(console, attempt);
            Assert.That(attempt.Cancelled, "UI open attempt cancelled");

            // a second assimilation is refused
            Assert.That(consoles.CanAssimilate((joe, comp), console, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("cmu-ew-joe-assimilate-already"));

            // a bystander is not a console
            Assert.That(consoles.CanAssimilate((joe, comp), bystander, out _), Is.False);

            // the real do-after reached the melding system: with no synthetic player on the map, the
            // river starts searching (FLOW-05) instead of granting the copy at once
            Assert.That(comp.PendingMelds, Is.EqualTo(1), "assimilation through the do-after scheduled the river");
            Assert.That(comp.RespawnReady, Is.False, "the copy waits for the search window");

            // once the ladder credits the objective, a repaired console is not the Joe's to take again
            comp.CurrentObjectives.Add(objective);
            entities.System<EWJoeObjectiveSystem>().Evaluate((joe, comp));
            Assert.That(entities.GetComponent<EWJoeAssimilateConditionComponent>(objective).Spent, "the credited objective is spent");
            entities.RemoveComponent<EWJoeAssimilatedConsoleComponent>(console);
            Assert.That(consoles.CanAssimilate((joe, comp), console, out var afterRepair), Is.False);
            Assert.That(afterRepair, Is.EqualTo("cmu-ew-joe-assimilate-not-called"), "a spent objective does not open its console again");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RepairChainNeedsThreeStepsInOrderAndFreesTheConsole()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var timing = server.ResolveDependency<IGameTiming>();

        EntityUid joe = default, console = default, human = default, screwdriver = default, wirecutter = default, shard = default;
        EntityUid? effectUid = null;

        await server.WaitAssertion(() =>
        {
            var consoles = entities.System<EWJoeConsoleSystem>();
            var hands = entities.System<SharedHandsSystem>();

            joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            human = entities.SpawnEntity(Human, map.GridCoords);
            console = entities.SpawnEntity(ResearchTerminal, map.GridCoords);
            screwdriver = entities.SpawnEntity(Screwdriver, map.GridCoords);
            wirecutter = entities.SpawnEntity(Wirecutter, map.GridCoords);
            shard = entities.SpawnEntity(Shard, map.GridCoords);

            var comp = entities.EnsureComponent<EWJoeComponent>(joe);
            consoles.Assimilate((joe, comp), console);
            var marker = entities.GetComponent<EWJoeAssimilatedConsoleComponent>(console);
            Assert.That(marker.RepairStep, Is.Zero);
            effectUid = marker.Effect;

            // wrong order: wirecutters and the shard do nothing at step 0
            Assert.That(consoles.TryStartRepairStep((console, marker), human, wirecutter), Is.False, "wirecutters refused at step 0");
            Assert.That(consoles.TryStartRepairStep((console, marker), human, shard), Is.False, "shard refused at step 0");
            Assert.That(marker.RepairStep, Is.Zero);

            // the real interaction path, tool in hand, starts the screwdriver do-after
            Assert.That(hands.TryPickupAnyHand(human, screwdriver), "picked up the screwdriver");
            var interact = new InteractUsingEvent(human, screwdriver, console, entities.GetComponent<TransformComponent>(console).Coordinates);
            entities.EventBus.RaiseLocalEvent(console, interact);
            Assert.That(interact.Handled, "screwdriver interaction handled");
            Assert.That(ActiveDoAfters(entities, human), Is.EqualTo(1), "screwdriver do-after running");
        });

        var toolTicks = (int) Math.Ceiling(6.5 / timing.TickPeriod.TotalSeconds);
        await pair.RunTicksSync(toolTicks);

        await server.WaitAssertion(() =>
        {
            var consoles = entities.System<EWJoeConsoleSystem>();
            var hands = entities.System<SharedHandsSystem>();
            var marker = entities.GetComponent<EWJoeAssimilatedConsoleComponent>(console);
            Assert.That(marker.RepairStep, Is.EqualTo(1), "screwdriver opened the panel");

            Assert.That(consoles.TryStartRepairStep((console, marker), human, screwdriver), Is.False, "screwdriver refused at step 1");
            hands.TryDrop(human, screwdriver);
            Assert.That(hands.TryPickupAnyHand(human, wirecutter), "picked up the wirecutters");
            Assert.That(consoles.TryStartRepairStep((console, marker), human, wirecutter), Is.True, "wirecutters start step 1");
            Assert.That(ActiveDoAfters(entities, human), Is.EqualTo(1), "wirecutter do-after running");
        });

        await pair.RunTicksSync(toolTicks);

        await server.WaitAssertion(() =>
        {
            var consoles = entities.System<EWJoeConsoleSystem>();
            var hands = entities.System<SharedHandsSystem>();
            var marker = entities.GetComponent<EWJoeAssimilatedConsoleComponent>(console);
            Assert.That(marker.RepairStep, Is.EqualTo(2), "wirecutters cleared the cabling");

            Assert.That(consoles.TryStartRepairStep((console, marker), human, wirecutter), Is.False, "wirecutters refused at step 2");
            hands.TryDrop(human, wirecutter);
            Assert.That(hands.TryPickupAnyHand(human, shard), "picked up the shard");
            Assert.That(consoles.TryStartRepairStep((console, marker), human, shard), Is.True, "shard starts the last step");
            Assert.That(ActiveDoAfters(entities, human), Is.EqualTo(1), "shard do-after running");
        });

        var shardTicks = (int) Math.Ceiling(9.5 / timing.TickPeriod.TotalSeconds);
        await pair.RunTicksSync(shardTicks);

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(entities.HasComponent<EWJoeAssimilatedConsoleComponent>(console), Is.False, "marker removed");
                Assert.That(effectUid is { } fx && entities.Deleted(fx), "corruption effect removed with the marker");
                Assert.That(entities.Deleted(shard), "shard consumed");
                Assert.That(entities.GetComponent<EWJoeComponent>(joe).ConsolesAssimilated, Is.EqualTo(1), "the count is history, not state");
            });

            // the UI opens again; the console is free, and (playtest 4) only an objective naming it again
            // would open it to the Joe
            var attempt = new ActivatableUIOpenAttemptEvent(human, false);
            entities.EventBus.RaiseLocalEvent(console, attempt);
            Assert.That(attempt.Cancelled, Is.False, "UI free after repair");

            var consoles = entities.System<EWJoeConsoleSystem>();
            Assert.That(consoles.CanAssimilate((joe, entities.GetComponent<EWJoeComponent>(joe)), console, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("cmu-ew-joe-assimilate-not-called"), "refused only for want of an objective");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ShardDropsOnceOnDeathAndEffectsStayClamped()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var consoles = entities.System<EWJoeConsoleSystem>();
            var mobState = entities.System<MobStateSystem>();
            var corporate = entities.System<CorporateConsoleSystem>();
            var research = entities.System<ServerResearchDataTerminalSystem>();
            var colonyBudget = entities.System<ColonyBudgetSystem>();
            var timing = server.ResolveDependency<IGameTiming>();

            var joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var comp = entities.EnsureComponent<EWJoeComponent>(joe);

            // --- effects
            var budgetConsole = entities.SpawnEntity(BudgetConsole, map.GridCoords);
            colonyBudget.SetBudget(1000f);
            consoles.Assimilate((joe, comp), budgetConsole);
            Assert.That(colonyBudget.GetBudget(), Is.InRange(599f, 1401f), "colony budget within ±40%");

            var corporateConsole = entities.SpawnEntity(CorporateConsole, map.GridCoords);
            corporate.AddToCorporateBudget(1000f);
            var corporateBefore = entities.GetComponent<CorporateConsoleComponent>(corporateConsole).CorporateBudget;
            Assert.That(corporateBefore, Is.GreaterThan(0f));
            consoles.Assimilate((joe, comp), corporateConsole);
            var corporateAfter = entities.GetComponent<CorporateConsoleComponent>(corporateConsole).CorporateBudget;
            Assert.That(corporateAfter, Is.InRange(corporateBefore * 0.6f - 1, corporateBefore * 1.4f + 1), "corporate budget within ±40%");

            var terminal = entities.SpawnEntity(ResearchTerminal, map.GridCoords);
            var faction = entities.GetComponent<Content.Shared.CMU14.Chemistry.Research.ResearchDataTerminalComponent>(terminal).Faction;
            research.UpdateSampleData(100, faction);
            research.GetResearch(faction).NextReroll = timing.CurTime + TimeSpan.FromHours(1);
            consoles.Assimilate((joe, comp), terminal);
            Assert.That(research.GetSampleData(faction), Is.InRange(60, 140), "sample data within ±40%");
            Assert.That(research.GetResearch(faction).NextReroll, Is.LessThanOrEqualTo(timing.CurTime), "board reroll forced");

            var department = entities.SpawnEntity(DepartmentConsole, map.GridCoords);
            var deptComp = entities.GetComponent<DepartmentConsoleComponent>(department);
            var cardA = entities.SpawnEntity(IdCard, map.GridCoords);
            var cardB = entities.SpawnEntity(IdCard, map.GridCoords);
            deptComp.Members.Add(cardA);
            deptComp.Members.Add(cardB);
            deptComp.SalaryOverrides[cardB] = 100;
            consoles.Assimilate((joe, comp), department);
            Assert.That(deptComp.SalaryOverrides[cardA], Is.InRange(deptComp.DefaultSalary * 0.6 - 1, deptComp.DefaultSalary * 1.4 + 1), "default salary varied");
            Assert.That(deptComp.SalaryOverrides[cardB], Is.InRange(59, 141), "override varied around its own value");

            var comms = entities.SpawnEntity(CommsConsole, map.GridCoords);
            consoles.Assimilate((joe, comp), comms);
            var commsMarker = entities.GetComponent<EWJoeAssimilatedConsoleComponent>(comms);
            Assert.That(commsMarker.NextSpoof, Is.GreaterThan(timing.CurTime), "comms console schedules spoofing");
            Assert.That(entities.GetComponent<EWJoeAssimilatedConsoleComponent>(corporateConsole).NextSpoof, Is.EqualTo(TimeSpan.Zero), "other consoles never spoof");

            Assert.That(comp.ConsolesAssimilated, Is.EqualTo(5));

            // --- stand-down (D10) drops the shard when a console it corrupted is still locked
            var joe2 = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var comp2 = entities.EnsureComponent<EWJoeComponent>(joe2);
            var spare = entities.SpawnEntity(CommsConsole, map.GridCoords);
            consoles.Assimilate((joe2, comp2), spare);
            Assert.That(EWJoeTestHelpers.CountShards(entities), Is.Zero);
            entities.RemoveComponent<EWJoeComponent>(joe2);
            Assert.That(EWJoeTestHelpers.CountShards(entities), Is.EqualTo(1), "stand-down with a corrupted console drops the shard");

            // --- death drops once
            mobState.ChangeMobState(joe, MobState.Dead);
            Assert.That(EWJoeTestHelpers.CountShards(entities), Is.EqualTo(2), "one shard on death");
            Assert.That(comp.ShardDropped, Is.True);

            mobState.ChangeMobState(joe, MobState.Alive);
            mobState.ChangeMobState(joe, MobState.Dead);
            Assert.That(EWJoeTestHelpers.CountShards(entities), Is.EqualTo(2), "no second shard after a revive");
        });

        await pair.CleanReturnAsync();
    }

    private static int ActiveDoAfters(IEntityManager entities, EntityUid uid)
    {
        if (!entities.TryGetComponent(uid, out DoAfterComponent? doAfter))
            return 0;

        return doAfter.DoAfters.Values.Count(d => !d.Completed && !d.Cancelled);
    }

}
