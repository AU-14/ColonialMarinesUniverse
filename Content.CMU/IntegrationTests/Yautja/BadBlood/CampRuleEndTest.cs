using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Yautja.BadBlood;
using Content.Server.GameTicking;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// FLOW-114: the end rule. The rule's decision is read off the rule entity, the pooled server has no round to end
[TestFixture]
public sealed class CampRuleEndTest : GameTest
{
    private const string Lodge = "CMUBadBloodCamp";

    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task LeaderGoneAndNoLodgeTogetherEndTheRound()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map);
        EntityUid lodge = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                Assert.That(Server.System<GameTicker>().RunLevel, Is.Not.EqualTo(GameRunLevel.InRound),
                    "this test reads the decision, it must not end a real round");
                lodge = Server.EntMan.SpawnEntity(Lodge, map.GridCoords);
            });

            await BadBloodTestHelpers.Kill(Pair, party.Leader);
            await Pair.RunSeconds(3);

            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.Multiple(() =>
                {
                    Assert.That(rules.IsLeaderGone(), Is.True);
                    Assert.That(rules.IsMarineVictoryDue(), Is.False);
                    Assert.That(rules.GetRoundOutcome(), Is.EqualTo(BadBloodRoundOutcome.None),
                        "a dead Leader does not end the round while the Lodge stands");
                });

                Server.EntMan.DeleteEntity(lodge);
                Assert.That(rules.IsMarineVictoryDue(), Is.True);
            });

            await Pair.RunSeconds(3);

            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(rules.GetRoundOutcome(), Is.EqualTo(BadBloodRoundOutcome.MarinesLeaderAndLodge),
                    "Leader gone and no Lodge: marines win");
                Assert.That(rules.TryDecideRound(BadBloodRoundOutcome.BadBloodDeparture), Is.False,
                    "a round is decided once");
            });
        }
        finally
        {
            await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
        }
    }

    [Test]
    public async Task FoundingGraceProtectsAnEarlyDeath()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map);

        try
        {
            await BadBloodTestHelpers.Kill(Pair, party.Leader);
            await Pair.RunSeconds(3);

            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(rules.InFoundingGrace(), Is.True);
                    Assert.That(rules.GetRoundOutcome(), Is.EqualTo(BadBloodRoundOutcome.None),
                        "a Leader lost before the first Lodge does not end the round during founding grace");
                    Assert.That(rules.TryDecideRound(BadBloodRoundOutcome.MarinesLeaderAndLodge), Is.False,
                        "the decision is checked again before it is taken");
                });

                // grace runs out with no Lodge and no Leader
                rule.Comp.FoundingGrace = TimeSpan.Zero;
            });

            await Pair.RunSeconds(3);

            await Server.WaitAssertion(() =>
            {
                Assert.That(Server.System<BadBloodCampRuleSystem>().GetRoundOutcome(),
                    Is.EqualTo(BadBloodRoundOutcome.MarinesLeaderAndLodge));
            });
        }
        finally
        {
            await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
        }
    }

    [Test]
    public async Task DisconnectedLeaderCountsAsGoneAfterTheTimeout()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map);

        try
        {
            // nobody drives the test bodies, so the Leader is disconnected from the start
            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);
                rule.Comp.FoundingGrace = TimeSpan.Zero;
                rule.Comp.LeaderSsdTimeout = TimeSpan.FromSeconds(6);
            });

            await Pair.RunSeconds(2.5f);

            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.Multiple(() =>
                {
                    Assert.That(rules.IsLeaderGone(), Is.False, "a short disconnect is not a lost Leader");
                    Assert.That(rules.GetRoundOutcome(), Is.EqualTo(BadBloodRoundOutcome.None));
                });
            });

            await Pair.RunSeconds(7);

            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.Multiple(() =>
                {
                    Assert.That(rules.IsLeaderGone(), Is.True);
                    Assert.That(rules.GetRoundOutcome(), Is.EqualTo(BadBloodRoundOutcome.MarinesLeaderAndLodge),
                        "a Leader disconnected past the timeout counts as gone");
                });
            });
        }
        finally
        {
            await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
        }
    }

    [Test]
    public async Task ClanWipeCallsOneLastStandSquadThenEndsTheRound()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        EntityUid lodge = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);
                rule.Comp.ClanWipeDelay = TimeSpan.FromSeconds(5);
                rule.Comp.SquadSpawnTimeout = TimeSpan.FromSeconds(2);

                lodge = Server.EntMan.SpawnEntity(Lodge, map.GridCoords);
                // a squad of two and 2 SP over
                Assert.That(rules.AddSp(rule.Comp.SquadOfTwoCost + FixedPoint4.New(2)), Is.True);
                Assert.That(rules.IsClanWiped(), Is.False);
            });

            await Pair.RunSeconds(2);
            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(rule.Comp.WipeCollapseAt, Is.Null, "the clan is alive, no timer runs");
                    Assert.That(rules.GetSp(), Is.EqualTo(rule.Comp.SquadOfTwoCost + FixedPoint4.New(2)));
                });
            });

            foreach (var body in party.All)
            {
                await BadBloodTestHelpers.Kill(Pair, body);
            }

            await Pair.RunSeconds(1.5f);

            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(rules.IsClanWiped(), Is.True);
                    Assert.That(rule.Comp.LastStandSquadOpened, Is.True, "the clan can pay: one last squad is called");
                    Assert.That(rules.GetSp(), Is.EqualTo(FixedPoint4.New(2)), "the last stand costs a squad of two");
                    Assert.That(rule.Comp.WipeCollapseAt, Is.Not.Null);
                    Assert.That(rules.GetRoundOutcome(), Is.EqualTo(BadBloodRoundOutcome.None));
                });
            });

            // nobody takes the seats: they are refunded, and no second squad is called
            await Pair.RunSeconds(7);

            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(rules.GetRoundOutcome(), Is.EqualTo(BadBloodRoundOutcome.MarinesClanWipe));
                    Assert.That(rules.GetSp(), Is.EqualTo(rule.Comp.SquadOfTwoCost + FixedPoint4.New(2)), "one last stand, not one after another");
                    Assert.That(rule.Comp.SquadCalls, Is.Empty);
                });
            });
        }
        finally
        {
            await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
            await Server.WaitPost(() =>
            {
                if (Server.EntMan.EntityExists(lodge))
                    Server.EntMan.DeleteEntity(lodge);
            });
        }
    }

    [Test]
    public async Task ClanWipeWithoutPointsEndsAfterTheDelay()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        EntityUid lodge = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);
                rule.Comp.ClanWipeDelay = TimeSpan.FromSeconds(4);
                lodge = Server.EntMan.SpawnEntity(Lodge, map.GridCoords);
            });

            foreach (var body in party.All)
            {
                await BadBloodTestHelpers.Kill(Pair, body);
            }

            await Pair.RunSeconds(1.5f);

            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(rule.Comp.LastStandSquadOpened, Is.False, "below the price of a squad nobody is called");
                    Assert.That(rule.Comp.WipeCollapseAt, Is.Not.Null);
                    Assert.That(rules.GetRoundOutcome(), Is.EqualTo(BadBloodRoundOutcome.None),
                        "the Lodge stands, so only the timer can end it");
                });
            });

            await Pair.RunSeconds(5);

            await Server.WaitAssertion(() =>
            {
                Assert.That(Server.System<BadBloodCampRuleSystem>().GetRoundOutcome(),
                    Is.EqualTo(BadBloodRoundOutcome.MarinesClanWipe));
            });
        }
        finally
        {
            await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
            await Server.WaitPost(() =>
            {
                if (Server.EntMan.EntityExists(lodge))
                    Server.EntMan.DeleteEntity(lodge);
            });
        }
    }
}
