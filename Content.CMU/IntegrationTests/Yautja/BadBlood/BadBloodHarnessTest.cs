using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.Threats;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Yautja.BadBlood;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

[TestFixture]
public sealed class BadBloodHarnessTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task HelperSpawnsALeaderAndThreeGrunts()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map);

        await Server.WaitAssertion(() =>
        {
            Assert.That(party.Grunts, Has.Count.EqualTo(3));
            Assert.That(party.All.Distinct().Count(), Is.EqualTo(4));

            Assert.That(STryComp<ThreatComponent>(party.Leader, out var leaderThreat), Is.True);
            Assert.That(leaderThreat!.ObjectiveJob, Is.EqualTo(BadBloodTestHelpers.LeaderJob));

            foreach (var grunt in party.Grunts)
            {
                Assert.That(STryComp<ThreatComponent>(grunt, out var threat), Is.True);
                Assert.That(threat!.ObjectiveJob, Is.EqualTo(BadBloodTestHelpers.MemberJob));
            }

            var minds = Server.System<SharedMindSystem>();
            foreach (var body in party.All)
            {
                Assert.That(SEntMan.HasComponent<YautjaBadBloodComponent>(body), Is.True);
                Assert.That(minds.GetMind(body), Is.Not.Null, "every body gets a mind");
            }
        });

        await BadBloodTestHelpers.Kill(Pair, party.Grunts[0]);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<MobStateSystem>().IsDead(party.Grunts[0]), Is.True);
            Assert.That(Server.System<MobStateSystem>().IsDead(party.Leader), Is.False);
        });
    }

    [Test]
    public async Task OnlyBadBloodsAnswerAsBadBlood()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);

        await Server.WaitAssertion(() =>
        {
            var badBlood = Server.System<BadBloodSystem>();
            var hunter = SSpawnAtPosition("CMUMobYautja", map.GridCoords);

            Assert.Multiple(() =>
            {
                Assert.That(badBlood.IsBadBlood(party.Leader), Is.True);
                Assert.That(badBlood.IsBadBlood(party.Grunts[0]), Is.True);
                Assert.That(badBlood.IsBadBlood(hunter), Is.False, "an honourable hunter is not a Bad Blood");

                Assert.That(badBlood.IsBadBloodLeader(party.Leader), Is.True);
                Assert.That(badBlood.IsBadBloodLeader(party.Grunts[0]), Is.False);
                Assert.That(badBlood.IsBadBloodLeader(hunter), Is.False);
            });
        });
    }
}
