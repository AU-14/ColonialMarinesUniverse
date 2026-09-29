using Content.Server._RMC14.Mentor.ImaginaryFriend;
using Content.Server.CMU14.Round.Objectives;
using Content.Server.CMU14.Threats.Rules;
using Content.Server.Mind;
using Content.Shared._RMC14.Mentor.ImaginaryFriend;
using Content.Shared.CMU14.Round.Objectives.Components;
using Content.Shared.CMU14.Round.Objectives.Type;
using Content.Shared.CMU14.Threats;
using Content.Shared.CMU14.Threats.Mobs.ZombieSummoner;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Systems;
using Content.Shared.Zombies;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._CMU14;

[TestFixture]
public sealed class ReportedRoleRegressionTest
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task ImaginaryFriendRetainsMentorsCurrentBody(bool deleteImaginer)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        EntityUid body = default, mindUid = default;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var minds = entities.System<MindSystem>();
            var mind = minds.CreateMind(null, "Mentor");
            mindUid = mind;
            var original = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            body = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            var imaginer = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            var ghost = entities.SpawnEntity("MentorObserver", map.GridCoords);
            minds.TransferTo(mind, original);
            minds.TransferTo(mind, body);
            minds.Visit(mind, ghost);
            entities.System<ImaginaryFriendSystem>().BecomeImaginaryFriend(imaginer, ghost);
            Assert.That(mind.Comp.OwnedEntity, Is.EqualTo(body));
            Assert.That(mind.Comp.VisitingEntity, Is.Not.Null);
            var friend = mind.Comp.VisitingEntity!.Value;
            Assert.That(entities.HasComponent<ImaginaryFriendComponent>(friend), Is.True);
            if (deleteImaginer)
                entities.DeleteEntity(imaginer);
            else
                entities.EventBus.RaiseLocalEvent(friend, new ImaginaryFriendStopBeingFriendsActionEvent());
        });
        await pair.RunTicksSync(2);
        await pair.Server.WaitAssertion(() =>
        {
            var mind = pair.Server.EntMan.GetComponent<Content.Shared.Mind.MindComponent>(mindUid);
            Assert.That(mind.OwnedEntity, Is.EqualTo(body));
            Assert.That(mind.VisitingEntity, Is.Null);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ReanimatedHumansRemainEliminated()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var human = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            var helper = entities.System<ThreatRuleHelper>();
            var state = entities.GetComponent<MobStateComponent>(human);
            Assert.That(helper.IsEliminated(human, state), Is.False);
            entities.EnsureComponent<ZombieComponent>(human);
            Assert.That(helper.IsEliminated(human, state), Is.True);
            Assert.That(helper.IsExcludedFromVictory(human, state), Is.False,
                "Reanimated casualties belong in the eliminated count, even when nobody controls them.");
            entities.EnsureComponent<ZombieSummonerMinionComponent>(human);
            Assert.That(helper.IsExcludedFromVictory(human, state), Is.True,
                "Summoned reinforcements must not inflate either the survivor or casualty counts.");
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ThreatKillsCountWithoutGenericJobMind(bool lateAssignment)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var target = entities.SpawnEntity("CMXenoDrone", map.GridCoords);
            if (!lateAssignment)
                AssignThreat();
            var objective = entities.SpawnEntity("killthreatobjectiveds", map.GridCoords);
            var data = entities.GetComponent<CMUObjectiveComponent>(objective);
            data.Active = true;
            data.Faction = "govfor";
            var kill = entities.GetComponent<KillObjectiveComponent>(objective);
            kill.KillCount = 2;
            entities.EventBus.RaiseLocalEvent(objective, new ObjectiveActivatedEvent());
            if (lateAssignment)
                AssignThreat();
            entities.System<MobStateSystem>().ChangeMobState(target, MobState.Dead);
            Assert.That(kill.AmountKilledPerFaction.GetValueOrDefault("govfor"), Is.EqualTo(1));

            void AssignThreat()
            {
                entities.EnsureComponent<ThreatComponent>(target).ObjectiveJob = "AU14JobThreatMember";
                entities.System<NpcFactionSystem>().AddFaction(target, "THREAT");
                entities.EventBus.RaiseEvent(EventSource.Local, new ObjectiveWatchedEntityStartupEvent(target));
            }
        });
        await pair.CleanReturnAsync();
    }
}
