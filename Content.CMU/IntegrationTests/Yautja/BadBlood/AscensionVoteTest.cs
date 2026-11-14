using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Yautja.BadBlood;
using Content.Shared.CMU14.Threats;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// FLOW-113: the clan votes for a new Leader when the old one is gone and a Lodge stands
[TestFixture]
public sealed class AscensionVoteTest : GameTest
{
    private const string Lodge = "CMUBadBloodCamp";
    private const string BadBloodBracer = "CMUYautjaBadBloodBracer";

    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task VoteNeedsALodgeAndMostVotesWins()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map);
        var spawned = new List<EntityUid>();

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var rules = Server.System<BadBloodCampRuleSystem>();
                var ascension = Server.System<BadBloodAscensionSystem>();
                var mobState = Server.System<MobStateSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);

                var first = party.Grunts[0];
                var second = party.Grunts[1];
                var third = party.Grunts[2];

                Assert.That(ascension.CanStand(first, out _), Is.False, "nobody claims the Lodge while the Leader lives");

                // outside founding grace a dead Leader with no Lodge starts no vote
                rule.Comp.FoundingGrace = TimeSpan.Zero;
                mobState.ChangeMobState(party.Leader, MobState.Dead);
                Assert.Multiple(() =>
                {
                    Assert.That(rules.IsLeaderGone(), Is.True);
                    Assert.That(ascension.IsVoteOpen(), Is.False, "the vote only opens while a Lodge stands");
                    Assert.That(ascension.TryStandAsCandidate(first), Is.False);
                });

                spawned.Add(entMan.SpawnEntity(Lodge, map.GridCoords));

                // the fallen Leader's bracer, lying where they fell
                var oldBracer = entMan.SpawnEntity(BadBloodBracer, map.GridCoords.Offset(new Vector2(0, 3)));
                entMan.EnsureComponent<BadBloodLeaderBracerComponent>(oldBracer);
                spawned.Add(oldBracer);

                // 60 s, + 10 s per candidate, never past the maximum
                rule.Comp.VoteMaxDuration = TimeSpan.FromSeconds(75);
                Assert.That(ascension.TryStandAsCandidate(first), Is.True, "standing on the Stone restarts a vote");
                var vote = ascension.GetVote();
                Assert.That(vote, Is.Not.Null);
                Assert.Multiple(() =>
                {
                    Assert.That(vote!.FromBracer, Is.False);
                    Assert.That(vote.EndsAt - vote.StartedAt, Is.EqualTo(TimeSpan.FromSeconds(70)));
                    Assert.That(ascension.TryStandAsCandidate(first), Is.False, "one claim each");
                });

                Assert.That(ascension.TryStandAsCandidate(second), Is.True);
                Assert.That(vote!.EndsAt - vote.StartedAt, Is.EqualTo(TimeSpan.FromSeconds(75)),
                    "the vote never runs past its maximum");

                Assert.Multiple(() =>
                {
                    Assert.That(ascension.TryVote(party.Leader, first), Is.False, "the dead do not vote");
                    Assert.That(ascension.TryVote(third, third), Is.False, "only candidates can be voted for");
                    Assert.That(ascension.TryVote(first, first), Is.True);
                    Assert.That(ascension.TryVote(second, first), Is.True);
                    Assert.That(ascension.TryVote(third, second), Is.True);
                });

                Assert.That(ascension.GetFrontRunner(), Is.EqualTo(first));

                // a vote can be moved until the vote closes
                Assert.That(ascension.TryVote(first, second), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(ascension.CountVotes(first), Is.EqualTo(1));
                    Assert.That(ascension.CountVotes(second), Is.EqualTo(2));
                });

                Assert.That(ascension.ResolveVote(), Is.EqualTo(second), "most votes wins");
                Assert.Multiple(() =>
                {
                    Assert.That(ascension.IsVoteOpen(), Is.False);
                    Assert.That(rules.GetLeader(), Is.EqualTo(second));
                    Assert.That(rules.IsLeaderGone(), Is.False);
                    Assert.That(entMan.HasComponent<BadBloodLeaderComponent>(second), Is.True);
                    Assert.That(Server.System<BadBloodSystem>().IsBadBloodLeader(second), Is.True);
                    Assert.That(entMan.GetComponent<ThreatComponent>(second).ObjectiveJob,
                        Is.EqualTo(BadBloodTestHelpers.LeaderJob),
                        "the kill-the-leader objective follows the Leader threat job to the winner");
                    Assert.That(entMan.GetComponent<ThreatComponent>(party.Leader).ObjectiveJob,
                        Is.EqualTo(BadBloodTestHelpers.MemberJob),
                        "the fallen Leader is no longer the objective's target");
                    Assert.That(entMan.HasComponent<BadBloodScuttleComponent>(oldBracer), Is.True,
                        "the fallen Leader's bracer scuttles");
                });

                // the winner's own bracer is promoted, never scuttled
                var inventory = Server.System<Content.Shared.Inventory.InventorySystem>();
                if (inventory.TryGetSlotEntity(second, "gloves", out var own))
                {
                    Assert.That(entMan.HasComponent<BadBloodLeaderBracerComponent>(own.Value), Is.True);
                    Assert.That(entMan.HasComponent<BadBloodScuttleComponent>(own.Value), Is.False);
                }

                Assert.That(ascension.CanStand(first, out _), Is.False, "the clan has a Leader again");
            });
        }
        finally
        {
            await Cleanup(party, spawned);
        }
    }

    [Test]
    public async Task TiesGoToKillsThenToTheEarliestCandidate()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map);
        var spawned = new List<EntityUid>();

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var rules = Server.System<BadBloodCampRuleSystem>();
                var ascension = Server.System<BadBloodAscensionSystem>();
                var mobState = Server.System<MobStateSystem>();
                Assert.That(rules.TryGetRule(out _), Is.True);

                var first = party.Grunts[0];
                var second = party.Grunts[1];
                var third = party.Grunts[2];

                spawned.Add(entMan.SpawnEntity(Lodge, map.GridCoords));
                mobState.ChangeMobState(party.Leader, MobState.Dead);
                Assert.That(ascension.IsVoteOpen(), Is.True, "the vote opens the moment the Leader falls");
                Assert.That(ascension.GetFrontRunner(), Is.Null, "nobody has claimed the Lodge yet");

                Assert.That(ascension.TryStandAsCandidate(first), Is.True);
                Assert.That(ascension.TryStandAsCandidate(second), Is.True);
                Assert.That(ascension.TryStandAsCandidate(third), Is.True);

                Assert.That(ascension.GetFrontRunner(), Is.EqualTo(first),
                    "no votes and no kills: whoever stood first");

                // killing a Yautja is not a worthy kill and breaks no tie
                var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords.Offset(new Vector2(0, 4)));
                spawned.Add(hunter);
                var minds = Server.System<SharedMindSystem>();
                minds.TransferTo(minds.CreateMind(null, "prey"), hunter);
                mobState.ChangeMobState(hunter, MobState.Dead, origin: second);
                Assert.Multiple(() =>
                {
                    Assert.That(ascension.GetKills(second), Is.Zero, "a Yautja kill does not count");
                    Assert.That(ascension.GetFrontRunner(), Is.EqualTo(first));
                });

                entMan.EnsureComponent<BadBloodKillTallyComponent>(third).Kills = 2;
                entMan.EnsureComponent<BadBloodKillTallyComponent>(second).Kills = 1;
                Assert.That(ascension.GetFrontRunner(), Is.EqualTo(third), "a tie goes to the higher kill count");

                // votes come before kills
                Assert.That(ascension.TryVote(first, second), Is.True);
                Assert.That(ascension.GetFrontRunner(), Is.EqualTo(second));

                // level again on votes: second and third have one each, third has more kills
                Assert.That(ascension.TryVote(second, third), Is.True);
                Assert.That(ascension.GetFrontRunner(), Is.EqualTo(third));

                // let the clock close it
                ascension.GetVote()!.EndsAt = Server.Timing.CurTime;
            });

            await Pair.RunSeconds(1);

            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                var ascension = Server.System<BadBloodAscensionSystem>();
                Assert.Multiple(() =>
                {
                    Assert.That(ascension.IsVoteOpen(), Is.False, "the vote closes by itself when its time is up");
                    Assert.That(rules.GetLeader(), Is.EqualTo(party.Grunts[2]));
                    Assert.That(rules.GetRoundOutcome(), Is.EqualTo(BadBloodRoundOutcome.None));
                });
            });
        }
        finally
        {
            await Cleanup(party, spawned);
        }
    }

    [Test]
    public async Task LosingTheLodgeCancelsTheVoteAndAnEmptyVoteLapses()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map);
        var spawned = new List<EntityUid>();

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var rules = Server.System<BadBloodCampRuleSystem>();
                var ascension = Server.System<BadBloodAscensionSystem>();
                Assert.That(rules.TryGetRule(out _), Is.True);

                var lodge = entMan.SpawnEntity(Lodge, map.GridCoords);
                Server.System<MobStateSystem>().ChangeMobState(party.Leader, MobState.Dead);
                Assert.That(ascension.TryStandAsCandidate(party.Grunts[0]), Is.True);
                Assert.That(ascension.IsVoteOpen(), Is.True);

                entMan.DeleteEntity(lodge);
                Assert.Multiple(() =>
                {
                    Assert.That(ascension.IsVoteOpen(), Is.False, "the vote dies with the Lodge");
                    Assert.That(rules.IsLeaderGone(), Is.True, "nobody was crowned");
                    Assert.That(ascension.TryOpenVote(), Is.False, "no Lodge, no vote");
                });

                // a new Lodge, and a vote nobody stands in
                spawned.Add(entMan.SpawnEntity(Lodge, map.GridCoords));
                Assert.That(ascension.TryOpenVote(), Is.True);
                Assert.That(ascension.ResolveVote(), Is.Null);
                Assert.Multiple(() =>
                {
                    Assert.That(ascension.IsVoteOpen(), Is.False, "a vote with no candidate lapses");
                    Assert.That(rules.IsLeaderGone(), Is.True);
                });

                // the next claim starts a new one
                Assert.That(ascension.TryStandAsCandidate(party.Grunts[1]), Is.True);
                Assert.That(ascension.IsVoteOpen(), Is.True);
                Assert.That(ascension.ResolveVote(), Is.EqualTo(party.Grunts[1]), "a lone candidate wins with no votes");
            });
        }
        finally
        {
            await Cleanup(party, spawned);
        }
    }

    [Test]
    public async Task FoundingGraceRunsTheVoteWithoutALodge()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map);

        try
        {
            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                var ascension = Server.System<BadBloodAscensionSystem>();
                Assert.That(rules.TryGetRule(out _), Is.True);
                Assert.That(rules.InFoundingGrace(), Is.True);

                Server.System<MobStateSystem>().ChangeMobState(party.Leader, MobState.Dead);

                var vote = ascension.GetVote();
                Assert.That(vote, Is.Not.Null, "in founding grace the vote opens with no Lodge");
                Assert.That(vote!.FromBracer, Is.True);

                Assert.That(ascension.TryStandAsCandidate(party.Grunts[0]), Is.True, "there is no Stone to stand on yet");
                Assert.That(ascension.TryVote(party.Grunts[1], party.Grunts[0]), Is.True);
                Assert.That(ascension.ResolveVote(), Is.EqualTo(party.Grunts[0]));
                Assert.That(rules.GetLeader(), Is.EqualTo(party.Grunts[0]));
            });

            await Pair.RunSeconds(2);
            await Server.WaitAssertion(() =>
            {
                Assert.That(Server.System<BadBloodCampRuleSystem>().GetRoundOutcome(),
                    Is.EqualTo(BadBloodRoundOutcome.None), "the clan kept a Leader, the round goes on");
            });
        }
        finally
        {
            await Cleanup(party, new List<EntityUid>());
        }
    }

    private async Task Cleanup(BadBloodTestParty party, List<EntityUid> spawned)
    {
        await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
        await Server.WaitPost(() =>
        {
            foreach (var uid in spawned)
            {
                if (Server.EntMan.EntityExists(uid))
                    Server.EntMan.DeleteEntity(uid);
            }
        });
    }
}
