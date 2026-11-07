using System.Linq;
using Content.IntegrationTests.Tests.GameRules;
using Content.Server.Antag;
using Content.Server.Antag.Components;
using Content.Server.CMU14.Round;
using Content.Server.CMU14.Round.Antags.EarlyWarning;
using Content.Server.CMU14.Round.Antags.EarlyWarning.Consoles;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Objectives.Components;
using Content.Shared.Antag;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.CMU14.Threats;
using Content.Shared.CMU14.Threats.GrandMachine;
using Content.Shared.GameTicking.Components;
using Content.Shared.Gibbing;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Medical;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Projectiles;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Content.Shared.Throwing;
using Content.Shared.Timing;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Round.Antags;

/// <summary>
/// Phase E7 (main audit §14). T1: only colony Working Joes are chosen, never on top of another antag.
/// </summary>
[TestFixture]
public sealed class EWJoeSelectionTest : AntagTest
{
    [Test]
    public async Task OnlyColonyJoesAreChosenAndNeverOnTopOfAnotherAntag()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var session = ServerSession!;
            var mind = SMind.GetOrCreateMind(session.UserId);
            var roles = Server.System<SharedRoleSystem>();
            var body = SEntMan.SpawnEntity("AU14MobWorkingJoeColony", map.GridCoords);
            SMind.TransferTo(mind.Owner, body);

            var ruleUid = SEntMan.Spawn("EWJoe");
            var rule = new Entity<AntagSelectionComponent>(ruleUid, SEntMan.GetComponent<AntagSelectionComponent>(ruleUid));
            var ew = SProtoMan.Index<AntagSpecifierPrototype>(EWJoeRuleSystem.AntagId);

            roles.MindAddJobRole(mind.Owner, jobPrototype: "AU14JobCivilianColonist");
            Assert.That(AntagSys.IsMindValid(session, ew), Is.False, "a colonist is never the Early Warning Joe");

            roles.MindRemoveRole<JobRoleComponent>(mind.Owner);
            roles.MindAddJobRole(mind.Owner, jobPrototype: "AU14JobColonyWorkingJoe");
            Assert.That(AntagSys.IsMindValid(session, ew), "a colony Working Joe can be");
            Assert.That(AntagSys.IsSessionValid(session, rule, ew), "and nothing else stops it");

            // already a Runaway Synth: no second antag on top
            roles.MindAddRole(mind.Owner, "MindRoleRunawaySynth", silent: true);
            Assert.Multiple(() =>
            {
                Assert.That(AntagSys.IsSessionValid(session, rule, ew), Is.False, "an antag already is not chosen again");
                Assert.That(AntagSys.TryMakeAntag(rule, ew, session, checkPref: false), Is.False);
                Assert.That(rule.Comp.PreSelectedSessions.Values.SelectMany(s => s), Is.Empty, "and takes no slot");
            });

            SEntMan.DeleteEntity(ruleUid);
        });
    }
}

/// <summary>
/// Phase E7 (main audit §14): T10 Grand Machine suppression, T11 the reset key's real zap, T8 the shard
/// on gibbing and deletion, kill credit through projectiles and throws, and the nullspace refusal.
/// </summary>
[TestFixture]
public sealed class EWJoeE7Test
{
    private const string ColonyJoe = "AU14MobWorkingJoeColony";
    private const string Human = "MobHuman";
    private const string Console = "CMUResearchDataTerminal";

    private const string Bullet = "CMBulletSentry10x50mm";
    private const string Knife = "RMCKitchenKnife";
    private const string EWMindRole = "MindRoleEWJoe";
    private const string KillCountObjective = "CMUEWJoeKillCountObjective";

    // a stand-in for the Grand Machine threat, which has no prototype yet; never offered in any gamemode
    [TestPrototypes]
    private const string Prototypes = @"
- type: threat
  id: GrandMachineThreat
  whitelistedGamemodes: [ CMUEWJoeTestNoSuchGamemode ]
";

    [Test]
    public async Task GrandMachineThreatStandsTheJoeDownAndEndsItsRule()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var round = entities.System<AuRoundSystem>();
            var ticker = entities.System<GameTicker>();
            var protos = server.ResolveDependency<IPrototypeManager>();
            try
            {
                var joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
                var mind = entities.System<MindSystem>().CreateMind(null, "Early Warning Joe");
                entities.System<MindSystem>().TransferTo(mind, joe);
                entities.System<SharedRoleSystem>().MindAddRole(mind, EWMindRole, silent: true);
                var comp = entities.EnsureComponent<EWJoeComponent>(joe);
                entities.EnsureComponent<GrandMachineTouchedComponent>(joe);
                entities.System<EWJoeRuleSystem>().ApplyLevel((joe, comp), 2);

                round.SetSelectedThreat(protos.Index<ThreatPrototype>(EWJoeRuleSystem.GrandMachineThreatId));
                Assert.Multiple(() =>
                {
                    Assert.That(entities.HasComponent<EWJoeComponent>(joe), Is.False, "the Joe stands down");
                    Assert.That(entities.HasComponent<GrandMachineTouchedComponent>(joe), Is.False);
                    Assert.That(entities.System<SharedRoleSystem>().MindHasRole<EWJoeRoleComponent>(mind), Is.False, "the role goes");
                });

                // a rule added while the Grand Machine is the threat ends at once
                var rule = ticker.AddGameRule("EWJoe");
                Assert.That(entities.HasComponent<EndedGameRuleComponent>(rule), "the rule ends before any selection");
            }
            finally
            {
                round.SetSelectedThreat(null);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ShardDropsOnceWhenTheJoeIsGibbedOrDeleted()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var consoles = entities.System<EWJoeConsoleSystem>();

            EntityUid CorruptingJoe()
            {
                var joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
                var comp = entities.EnsureComponent<EWJoeComponent>(joe);
                consoles.Assimilate((joe, comp), entities.SpawnEntity(Console, map.GridCoords));
                return joe;
            }

            entities.System<GibbingSystem>().Gib(CorruptingJoe());
            Assert.That(EWJoeTestHelpers.CountShards(entities), Is.EqualTo(1), "one shard when gibbed");

            entities.DeleteEntity(CorruptingJoe());
            Assert.That(EWJoeTestHelpers.CountShards(entities), Is.EqualTo(2), "one shard when deleted outright");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task KillsByTheJoesBulletsAndThrowsCount()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var mindSys = entities.System<MindSystem>();
            var mobState = entities.System<MobStateSystem>();

            var joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var mind = mindSys.CreateMind(null, "Early Warning Joe");
            mindSys.TransferTo(mind, joe);
            entities.System<SharedRoleSystem>().MindAddRole(mind, EWMindRole, silent: true);
            var comp = entities.EnsureComponent<EWJoeComponent>(joe);

            Assert.That(mindSys.TryAddObjective(mind, mind.Comp, KillCountObjective));
            var counter = mind.Comp.Objectives.Last();
            comp.CurrentObjectives.Add(counter);
            var count = entities.GetComponent<CounterConditionComponent>(counter);

            var bullet = entities.SpawnEntity(Bullet, map.GridCoords);
            entities.GetComponent<ProjectileComponent>(bullet).Shooter = joe;
            mobState.ChangeMobState(entities.SpawnEntity(Human, map.GridCoords), MobState.Dead, origin: bullet);
            Assert.That(count.Count, Is.EqualTo(1), "a kill by the Joe's bullet counts");

            var knife = entities.SpawnEntity(Knife, map.GridCoords);
            entities.EnsureComponent<ThrownItemComponent>(knife).Thrower = joe;
            mobState.ChangeMobState(entities.SpawnEntity(Human, map.GridCoords), MobState.Dead, origin: knife);
            Assert.That(count.Count, Is.EqualTo(2), "a kill by something it threw counts");

            var stranger = entities.SpawnEntity(Human, map.GridCoords);
            var other = entities.SpawnEntity(Bullet, map.GridCoords);
            entities.GetComponent<ProjectileComponent>(other).Shooter = stranger;
            mobState.ChangeMobState(entities.SpawnEntity(Human, map.GridCoords), MobState.Dead, origin: other);
            Assert.That(count.Count, Is.EqualTo(2), "someone else's bullet does not");
        });

        await pair.CleanReturnAsync();
    }

}
