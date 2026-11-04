using System.Linq;
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Expeditions;
using Content.Shared.CCVar;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Expeditions;

[TestFixture, NonParallelizable]
public sealed class CMUAgentPerformanceTest : GameTest
{
    private static readonly Robust.Shared.Prototypes.ProtoId<Content.Shared.NPC.Prototypes.NpcFactionPrototype> Govfor = "GOVFOR";
    public override PoolSettings PoolSettings => new() { Dirty = true };

    [Test]
    public async Task DeferredDecisionStillRunsWeaponExecution()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var cfg = Server.ResolveDependency<IConfigurationManager>();
            cfg.SetCVar(CCVars.CMUExpeditionThinksPerTick, 1);
            cfg.SetCVar(CCVars.CMUExpeditionThinkBudgetMs, 1000f);
            var first = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var front = SEntMan.EnsureComponent<CMUExpeditionAgentComponent>(first);
            front.Home = map.GridCoords.Offset(new System.Numerics.Vector2(50, 0));
            front.LeashRange = 1;
            front.NextThink = SGameTiming.CurTime - TimeSpan.FromSeconds(2);
            var second = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var deferred = SEntMan.EnsureComponent<CMUExpeditionAgentComponent>(second);
            deferred.NextThink = SGameTiming.CurTime - TimeSpan.FromSeconds(1);
            deferred.State = CMUExpeditionAgentState.Aim;
            deferred.FireAt = SGameTiming.CurTime;
            Server.System<CMUExpeditionAgentSystem>().Update(0);
            Assert.That(front.ThinkSamples, Is.EqualTo(1));
            Assert.That(deferred.ThinkSamples, Is.Zero);
            Assert.That(deferred.State, Is.EqualTo(CMUExpeditionAgentState.Engage));
            Assert.That(deferred.LastFireCheck, Is.EqualTo("weapon-not-ready"),
                "Even an agent whose decision was deferred must execute its native weapon check.");
        });
    }

    [Test]
    public async Task OverdueDecisionsAreCappedAndEveryAgentGetsATurn()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var cfg = Server.ResolveDependency<IConfigurationManager>();
            cfg.SetCVar(CCVars.CMUExpeditionThinksPerTick, 1);
            cfg.SetCVar(CCVars.CMUExpeditionThinkBudgetMs, 1000f);
            var system = Server.System<CMUExpeditionAgentSystem>();
            var agents = new List<CMUExpeditionAgentComponent>();
            for (var i = 0; i < 12; i++)
            {
                var uid = SEntMan.SpawnEntity("CMUExpeditionScavenger", map.GridCoords);
                var agent = SEntMan.GetComponent<CMUExpeditionAgentComponent>(uid);
                // A cheap home-return decision isolates scheduling from random combat.
                agent.Home = map.GridCoords.Offset(new System.Numerics.Vector2(50, 0));
                agent.LeashRange = 1;
                agent.NextThink = SGameTiming.CurTime - TimeSpan.FromSeconds(1);
                agents.Add(agent);
            }
            for (var update = 0; update < agents.Count; update++)
            {
                system.Update(0);
                Assert.That(system.ThinksLastUpdate, Is.EqualTo(1));
                Assert.That(agents.Count(agent => agent.ThinkSamples > 0), Is.EqualTo(update + 1),
                    "An overdue agent must keep its place ahead of already served agents.");
            }
            Assert.That(agents.All(agent => agent.ThinkSamples == 1), Is.True);
            Assert.That(system.DeferredThinksLastUpdate, Is.Zero);
        });
    }

    [Test]
    public async Task GunfireMembershipMatchesAcquisitionWithoutScanningAllContacts()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var system = Server.System<CMUExpeditionAgentSystem>();
            var factions = Server.System<NpcFactionSystem>();
            var uid = SEntMan.SpawnEntity("CMUExpeditionScavenger", map.GridCoords);
            var agent = SEntMan.GetComponent<CMUExpeditionAgentComponent>(uid);
            var hostile = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new System.Numerics.Vector2(3, 0)));
            factions.AddFaction(hostile, Govfor);
            var friendly = SEntMan.SpawnEntity("CMUExpeditionScavenger", map.GridCoords);
            var neutral = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            factions.ClearFactions(neutral);
            var distant = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new System.Numerics.Vector2(100, 0)));
            factions.ClearFactions(distant);
            var nearbyMethod = typeof(CMUExpeditionAgentSystem).GetMethod("NearbyExpeditionHostile", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var acquireMethod = typeof(CMUExpeditionAgentSystem).GetMethod("ExpeditionHostiles", BindingFlags.Instance | BindingFlags.NonPublic)!;
            void Check(EntityUid target, bool expected)
            {
                var nearby = (bool) nearbyMethod.Invoke(system, new object[] { uid, agent, target })!;
                var acquired = ((IEnumerable<EntityUid>) acquireMethod.Invoke(system, new object[] { uid, agent })!).Contains(target);
                Assert.That(nearby, Is.EqualTo(expected));
                Assert.That(acquired, Is.EqualTo(expected));
            }
            Check(hostile, true);
            Check(friendly, false);
            Check(neutral, false);
            Check(distant, false);
            factions.AggroEntity(uid, distant);
            Check(distant, true);
            factions.IgnoreEntity(uid, hostile);
            Check(hostile, false);
            agent.TargetFactions.Add("GOVFOR");
            Check(neutral, false);
            Check(distant, false);
            agent.TargetFactions.Clear();
            agent.TargetFactions.Add("AllHostile");
            Check(neutral, false);
            Check(friendly, false);
            SEntMan.DeleteEntity(hostile);
            Check(hostile, false);
        });
    }
}
