using System.Linq;
using Content.Server.CMU14.Round.Antags.EarlyWarning;
using Content.Server.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Server.CMU14.Round.Antags.EarlyWarning.Objectives;
using Content.Server.Mind;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Shared.CMU14.Round.Antags.EarlyWarning.Melding;
using Content.Shared.CMU14.Threats.GrandMachine;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Roles;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.IntegrationTests._CMU14.Round.Antags;

/// <summary>
/// The full implementation audit's repairs: a respawn's drop resets the directives and leaves a locker;
/// removing the antag by any means takes its role; the round-end record keeps the peak; Overclock stops
/// when the Joe goes down; Consecrate needs sight and spares thralls; an overheat refusal keeps the
/// cooldown; Scour refuses gear in a container; Drown takes organics only.
/// </summary>
[TestFixture]
public sealed class EWJoeAuditFixTest
{
    private const string ColonyJoe = "AU14MobWorkingJoeColony";
    private const string Human = "CMMobHuman";
    private const string Yautja = "CMUMobYautja";
    private const string Wall = "WallSolid";
    private const string EWMindRole = "MindRoleEWJoe";
    private const string KillCountObjective = "CMUEWJoeKillCountObjective";
    private const string Armor = "CMArmorM3Medium";

    private static (EntityUid Joe, EWJoeComponent Comp, Entity<Content.Shared.Mind.MindComponent> Mind) SpawnJoe(IEntityManager entities, EntityCoordinates at)
    {
        var mindSys = entities.System<MindSystem>();
        var joe = entities.SpawnEntity(ColonyJoe, at);
        var mind = mindSys.CreateMind(null, "Early Warning Joe");
        mindSys.TransferTo(mind, joe);
        entities.System<SharedRoleSystem>().MindAddRole(mind, EWMindRole, silent: true);
        var comp = entities.EnsureComponent<EWJoeComponent>(joe);
        return (joe, comp, mind);
    }

    [Test]
    public async Task RespawnDropResetsDirectivesLeavesContainersAndKeepsThePeak()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var rule = entities.System<EWJoeRuleSystem>();
            var mindSys = entities.System<MindSystem>();
            var roles = entities.System<SharedRoleSystem>();
            var (joe, comp, mind) = SpawnJoe(entities, map.GridCoords);
            rule.ApplyLevel((joe, comp), 4);

            // a level-4 directive on offer, with credit already earned
            Assert.That(mindSys.TryAddObjective(mind, mind.Comp, KillCountObjective));
            var old = mind.Comp.Objectives.Last();
            comp.CurrentObjectives.Add(old);
            comp.CompletedCredit[old] = 1;

            // CORE-01: the drop takes them with it
            rule.LowerLevel((joe, comp), 2);
            Assert.Multiple(() =>
            {
                Assert.That(comp.CurrentObjectives, Does.Not.Contain(old), "the higher level's directive is gone from the offer");
                Assert.That(comp.CompletedCredit, Is.Empty, "and so is its credit");
            });

            // CORE-04: the round-end record keeps the highest level on the mind
            Assert.That(roles.MindHasRole<EWJoeRoleComponent>((mind.Owner, mind.Comp), out var role), "the antag role");
            Assert.Multiple(() =>
            {
                Assert.That(role!.Value.Comp2.Recorded);
                Assert.That(role.Value.Comp2.HighestLevel, Is.EqualTo(4), "the peak, not the level after the drop");
            });

            // CORE-02: a Joe that fell inside something is taken out first. No refuge is searched for (no
            // attempts), so the Joe is not moved at all: only the release can have taken it out.
            comp.RespawnAttempts = 0;
            var box = entities.SpawnEntity(null, map.GridCoords);
            var containers = entities.System<SharedContainerSystem>();
            var container = containers.EnsureContainer<Container>(box, "ew-test-locker");
            Assert.That(containers.Insert(joe, container), "the Joe is shut in");
            entities.System<EWJoeRespawnSystem>().Respawn((joe, comp), entities.System<SharedTransformSystem>().GetMapCoordinates(box));
            Assert.That(container.ContainedEntities, Does.Not.Contain(joe), "the respawn took it out of the container");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RunningTogglesShowActiveAndDeadThrallsLeaveThePanel()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var (joe, comp, _) = SpawnJoe(entities, map.GridCoords);
            entities.System<EWJoeRuleSystem>().ApplyLevel((joe, comp), 3);

            // ABIL-05: a running Overclock shows as active on its action, and stops showing when it ends
            var action = comp.GrantedActions.First(a => entities.GetComponent<MetaDataComponent>(a).EntityPrototype?.ID == "CMUActionEWJoeOverclock");
            var actionComp = entities.GetComponent<Content.Shared.Actions.Components.ActionComponent>(action);
            var start = new EWJoeOverclockActionEvent { Performer = joe, Action = (action, actionComp) };
            entities.EventBus.RaiseLocalEvent(joe, start);
            Assert.That(actionComp.Toggled, "Overclock shows as active");
            entities.RemoveComponent<EWJoeOverclockComponent>(joe);
            Assert.That(actionComp.Toggled, Is.False, "and stops when it ends, however it ends");

            // CORE-06: the panel lists living thralls only
            var thrall = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            comp.Thralls.Add(thrall);
            var objectives = entities.System<EWJoeObjectiveSystem>();
            objectives.UpdateStatus((joe, comp));
            Assert.That(entities.GetComponent<EWJoeStatusComponent>(joe).Thralls, Has.Count.EqualTo(1));
            entities.System<MobStateSystem>().ChangeMobState(thrall, MobState.Dead);
            objectives.UpdateStatus((joe, comp));
            Assert.That(entities.GetComponent<EWJoeStatusComponent>(joe).Thralls, Is.Empty, "a dead thrall leaves the panel");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RemovingTheAntagAnyWayTakesItsRole()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var (joe, _, mind) = SpawnJoe(entities, map.GridCoords);
            entities.EnsureComponent<GrandMachineTouchedComponent>(joe);
            var roles = entities.System<SharedRoleSystem>();
            Assert.That(roles.MindHasRole<EWJoeRoleComponent>(mind.Owner));

            // CORE-03: an admin removing the component, not a stand-down
            entities.RemoveComponent<EWJoeComponent>(joe);
            Assert.Multiple(() =>
            {
                Assert.That(roles.MindHasRole<EWJoeRoleComponent>(mind.Owner), Is.False, "the antag role goes");
                Assert.That(entities.HasComponent<GrandMachineTouchedComponent>(joe), Is.False, "and the machine's mark");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OverclockStopsWhenTheJoeGoesDown()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var timing = server.ResolveDependency<IGameTiming>();
        EntityUid joe = default;

        await server.WaitAssertion(() =>
        {
            (joe, var comp, _) = SpawnJoe(entities, map.GridCoords);
            entities.System<EWJoeRuleSystem>().ApplyLevel((joe, comp), 3);
            entities.System<EWJoeHeatSystem>().StartOverclock(joe);
            entities.System<MobStateSystem>().ChangeMobState(joe, MobState.Critical);
        });

        await pair.RunTicksSync((int) Math.Ceiling(1.5 / timing.TickPeriod.TotalSeconds));

        await server.WaitAssertion(() =>
        {
            Assert.That(entities.HasComponent<EWJoeOverclockComponent>(joe), Is.False, "ABIL-01: a downed Joe's overclock stops");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RefusalsCostNothing()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var (joe, comp, _) = SpawnJoe(entities, map.GridCoords);
            entities.System<EWJoeRuleSystem>().ApplyLevel((joe, comp), 3);
            var heat = entities.GetComponent<EWJoeHeatComponent>(joe);

            // ABIL-02: a body behind a wall cannot be the arc's first link
            entities.SpawnEntity(Wall, EWJoeTestHelpers.TileEast(entities, map, 1));
            var behind = entities.SpawnEntity(Human, EWJoeTestHelpers.TileEast(entities, map, 2));
            var throughWall = new EWJoeConsecrateActionEvent { Performer = joe, Target = behind };
            entities.EventBus.RaiseLocalEvent(joe, throughWall);

            // ABIL-03: nor the Joe's own thrall
            var thrall = entities.SpawnEntity(Human, EWJoeTestHelpers.TileEast(entities, map, -1));
            entities.EnsureComponent<RiverMeldedComponent>(thrall).Master = joe;
            var onThrall = new EWJoeConsecrateActionEvent { Performer = joe, Target = thrall };
            entities.EventBus.RaiseLocalEvent(joe, onThrall);

            Assert.Multiple(() =>
            {
                Assert.That(throughWall.Handled, Is.False, "no arc through a wall");
                Assert.That(onThrall.Handled, Is.False, "no arc on a thrall");
                Assert.That(heat.Heat, Is.Zero, "and neither cost heat");
            });

            // ABIL-07: an overheated core refuses Overclock at the action attempt, before any cooldown starts
            heat.Overheated = true;
            var overclockAction = comp.GrantedActions.First(a => entities.GetComponent<MetaDataComponent>(a).EntityPrototype?.ID == "CMUActionEWJoeOverclock");
            var attempt = new Content.Shared.Actions.Events.ActionAttemptEvent(joe);
            entities.EventBus.RaiseLocalEvent(overclockAction, ref attempt);
            Assert.That(attempt.Cancelled, "the attempt is refused");
            Assert.That(attempt.Reason, Is.EqualTo(Loc.GetString("cmu-ew-joe-heat-overheated")), "and says why");

            var overclock = new EWJoeOverclockActionEvent { Performer = joe };
            entities.EventBus.RaiseLocalEvent(joe, overclock);
            Assert.That(overclock.Handled, Is.False, "the refusal is not a use");
            heat.Overheated = false;

            // ABIL-09: gear in a bag is refused before the do-after
            var bag = entities.SpawnEntity(null, map.GridCoords);
            var armor = entities.SpawnEntity(Armor, map.GridCoords);
            var bagSlot = entities.System<SharedContainerSystem>().EnsureContainer<Container>(bag, "ew-test-bag");
            Assert.That(entities.System<SharedContainerSystem>().Insert(armor, bagSlot), "armor packed");
            var scour = new EWJoeScourActionEvent { Performer = joe, Target = armor };
            entities.EventBus.RaiseLocalEvent(joe, scour);
            Assert.That(scour.Handled, Is.False, "packed gear is refused at once");

            // ABIL-10: Drown takes organics only, by the shared rule
            var hunter = entities.SpawnEntity(Yautja, map.GridCoords);
            Assert.That(entities.System<EWJoeCombatSystem>().CanDrown((joe, comp), hunter, out var drownReason), Is.False);
            Assert.That(drownReason, Is.EqualTo("cmu-ew-joe-drown-invalid"), "refused as not a person, before the grab is even checked");
        });

        await pair.CleanReturnAsync();
    }
}
