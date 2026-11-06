using System.Linq;
using Content.Server.Antag;
using Content.Server.Antag.Components;
using Content.Server.CMU14.Round.Antags.EarlyWarning;
using Content.Server.CMU14.Round.Antags.EarlyWarning.Objectives;
using Content.Server.Mind;
using Content.Server.Objectives.Systems;
using Content.Server.Objectives.Components;
using Content.Shared._RMC14.Armor;
using Content.Shared.Actions.Components;
using Content.Shared.Antag;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.CMU14.Threats.GrandMachine;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Objectives.Components;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Round.Antags;

/// <summary>
/// Cross-phase properties from the E0-E3 congruence audit: the real selection path (camera, markers,
/// level 0), death as a terminal state for the ladder, the level-4 console gate waiving itself on a
/// planet with no console to offer, stand-down removing what selection added, and the three console
/// whitelists agreeing.
/// </summary>
[TestFixture]
public sealed class EWJoeCongruenceTest
{
    private const string ColonyJoe = "AU14MobWorkingJoeColony";
    private const string Rule = "EWJoe";
    private const string Specifier = "EWJoe";
    private const string Camera = "RMCM5CameraGear";
    private const string EWMindRole = "MindRoleEWJoe";
    private const string KillCountObjective = "CMUEWJoeKillCountObjective";
    private const string AssimilateAction = "CMUActionEWJoeAssimilate";
    private const string AssimilateObjective = "CMUEWJoeAssimilateConsoleObjective";

    [Test]
    public async Task SelectionStripsTheCameraAppliesLevelZeroAndStandDownUndoesIt()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var protos = server.ResolveDependency<IPrototypeManager>();

        var innateActions = 0;
        await server.WaitAssertion(() =>
        {
            var inventory = entities.System<InventorySystem>();

            var joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            // the Working Joe body brings its own actions; only the Early Warning grants may come and go
            innateActions = entities.GetComponent<ActionsComponent>(joe).Actions.Count;
            Assert.That(inventory.TryGetSlotEntity(joe, "ears2", out var worn) && entities.GetComponent<MetaDataComponent>(worn.Value).EntityPrototype?.ID == Camera,
                "a colony Joe spawns wearing the overwatch camera");
            var hadArmorUser = entities.HasComponent<CMArmorUserComponent>(joe);

            // the real selection path: the rule's AfterAntagEntitySelectedEvent for the EWJoe specifier
            var rule = entities.SpawnEntity(Rule, MapCoordinates.Nullspace);
            var selection = entities.GetComponent<AntagSelectionComponent>(rule);
            var specifier = protos.Index<AntagSpecifierPrototype>(Specifier);
            var ev = new AfterAntagEntitySelectedEvent(null, joe, (rule, selection), specifier);
            entities.EventBus.RaiseLocalEvent(rule, ref ev, true);
        });

        await pair.RunTicksSync(2);

        EntityUid joeUid = default;
        await server.WaitAssertion(() =>
        {
            var inventory = entities.System<InventorySystem>();
            var query = entities.EntityQueryEnumerator<EWJoeComponent>();
            Assert.That(query.MoveNext(out joeUid, out var comp), "selection marked the Joe");

            Assert.Multiple(() =>
            {
                Assert.That(entities.HasComponent<GrandMachineTouchedComponent>(joeUid), "lore marker");
                Assert.That(entities.HasComponent<CMArmorUserComponent>(joeUid), "armor user marker");
                Assert.That(comp!.Level, Is.Zero);
                Assert.That(comp.AppliedLevel, Is.Zero, "level 0 applied");
                // Test of Faith, Optical Zoom, Disrupt, Offer, Signal Status and the Gaze and Machine wheels
                Assert.That(comp.GrantedActions, Has.Count.EqualTo(7), "level-0 kit");
                Assert.That(entities.GetComponent<ActionsComponent>(joeUid).Actions, Has.Count.EqualTo(innateActions + 7), "kit on top of the innate actions");
                Assert.That(inventory.TryGetSlotEntity(joeUid, "ears2", out _), Is.False, "camera gone (D13)");
            });

            // stand-down: everything selection added goes, including the armor marker it added
            var addedArmor = comp.AddedArmorUser;
            entities.RemoveComponent<EWJoeComponent>(joeUid);
            Assert.That(entities.HasComponent<CMArmorUserComponent>(joeUid), Is.EqualTo(!addedArmor), "armor marker restored to its pre-selection state");
            Assert.That(entities.GetComponent<ActionsComponent>(joeUid).Actions, Has.Count.EqualTo(innateActions), "granted actions removed, innate ones kept");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DeadJoeDoesNotLevelAndConsoleGateWaivesWithoutConsoles()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var mindSys = entities.System<MindSystem>();
            var roles = entities.System<SharedRoleSystem>();
            var rule = entities.System<EWJoeRuleSystem>();
            var ladder = entities.System<EWJoeObjectiveSystem>();
            var counter = entities.System<CounterConditionSystem>();
            var mobState = entities.System<MobStateSystem>();

            var joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var mind = mindSys.CreateMind(null, "Early Warning Joe");
            mindSys.TransferTo(mind, joe);
            roles.MindAddRole(mind, EWMindRole, silent: true);
            var comp = entities.EnsureComponent<EWJoeComponent>(joe);
            rule.ApplyLevel((joe, comp), 0);

            // two finished kill-count objectives on offer
            comp.CurrentObjectives.Clear();
            for (var i = 0; i < 2; i++)
            {
                Assert.That(mindSys.TryAddObjective(mind, mind.Comp, KillCountObjective));
                var objective = mind.Comp.Objectives.Last();
                comp.CurrentObjectives.Add(objective);
                var target = entities.GetComponent<NumberObjectiveComponent>(objective).Target;
                for (var k = 0; k < target; k++)
                    counter.IncreaseCount(objective);
            }

            // a dead Joe earns nothing (ARCH-C01)
            mobState.ChangeMobState(joe, MobState.Dead);
            ladder.Evaluate((joe, comp));
            Assert.That(comp.Level, Is.Zero, "no level-up while dead");
            Assert.That(comp.CompletedCredit, Is.Empty, "no credit while dead");

            // alive again, the same work counts
            mobState.ChangeMobState(joe, MobState.Alive);
            ladder.Evaluate((joe, comp));
            Assert.That(comp.Level, Is.EqualTo(1), "level-up once alive");

            // level 4 on a planet with no console: the forced offer fails and the gate waives itself (FLOW-C02)
            rule.ApplyLevel((joe, comp), 4);
            Assert.That(comp.CurrentObjectives.Any(o => entities.HasComponent<EWJoeAssimilateConditionComponent>(o)), Is.False,
                "no console objective could be offered");
            comp.CurrentObjectives.Clear();
            comp.CompletedCredit.Clear();
            for (var i = 0; i < 2; i++)
            {
                Assert.That(mindSys.TryAddObjective(mind, mind.Comp, KillCountObjective));
                var objective = mind.Comp.Objectives.Last();
                comp.CurrentObjectives.Add(objective);
                var target = entities.GetComponent<NumberObjectiveComponent>(objective).Target;
                for (var k = 0; k < target; k++)
                    counter.IncreaseCount(objective);
            }

            ladder.Evaluate((joe, comp));
            Assert.That(comp.Level, Is.EqualTo(5), "two credits carry level 4 when no console exists");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TheThreeConsoleWhitelistsAgree()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var protos = server.ResolveDependency<IPrototypeManager>();
        var factory = server.ResolveDependency<IComponentFactory>();

        await server.WaitAssertion(() =>
        {
            var fromComponent = new EWJoeComponent().ConsoleWhitelist.Components!.ToHashSet();

            var action = protos.Index<EntityPrototype>(AssimilateAction);
            Assert.That(action.TryGetComponent(out EntityTargetActionComponent? targetAction, factory), "action prototype has EntityTargetAction");
            var fromAction = targetAction!.Whitelist!.Components!.ToHashSet();

            var objective = protos.Index<EntityPrototype>(AssimilateObjective);
            Assert.That(objective.TryGetComponent(out EWJoeAssimilateConditionComponent? condition, factory), "objective prototype has the condition");
            var fromObjective = condition!.Whitelist.Components!.ToHashSet();

            Assert.Multiple(() =>
            {
                Assert.That(fromAction, Is.EquivalentTo(fromComponent), "action whitelist matches the component default");
                Assert.That(fromObjective, Is.EquivalentTo(fromComponent), "objective whitelist matches the component default");
            });
        });

        await pair.CleanReturnAsync();
    }
}
