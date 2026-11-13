using System.Linq;
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Round;
using Content.Server.CMU14.Threats;
using Content.Server.CMU14.Yautja;
using Content.Server.GameTicking;
using Content.Shared.CMU14.Threats;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

/// <summary>
/// FLOW-121: a Bad Blood round and an honourable predator hunt exclude each other.
/// </summary>
public sealed class BadBloodPredatorRoundBlockTest : GameTest
{
    private static readonly ProtoId<ThreatPrototype> BadBloodThreat = "BadBloodClanThreat";
    private static readonly EntProtoId HuntRule = "CMUYautjaPredatorRound";

    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task BlockedGameRulesResolveAndKeepBadBloodOffAHuntBallot()
    {
        await Server.WaitAssertion(() =>
        {
            foreach (var threat in Server.ProtoMan.EnumeratePrototypes<ThreatPrototype>())
            {
                foreach (var rule in threat.BlockedGameRules)
                {
                    Assert.That(Server.ProtoMan.HasIndex<EntityPrototype>(rule.Id), Is.True,
                        $"{threat.ID} blocks unknown game rule {rule}");
                }
            }

            var badBlood = Server.ProtoMan.Index(BadBloodThreat);
            Assert.That(badBlood.BlockedGameRules, Does.Contain(HuntRule));
            Assert.That(ThreatVoteSelection.IsBlockedByRules(badBlood.BlockedGameRules, new[] { HuntRule.Id }),
                Is.True);
            Assert.That(ThreatVoteSelection.IsBlockedByRules(badBlood.BlockedGameRules, Array.Empty<string>()),
                Is.False);
        });
    }

    [Test]
    public async Task HuntCannotBeStartedWhileBadBloodIsSelected()
    {
        await Server.WaitAssertion(() =>
        {
            var round = SEntMan.System<YautjaPredatorRoundSystem>();
            var auRound = SEntMan.System<AuRoundSystem>();
            Assert.That(round.HuntActive, Is.False);

            auRound.SetSelectedThreat(Server.ProtoMan.Index(BadBloodThreat));
            Assert.That(round.TryInitializePredatorRound(out _), Is.False);
            Assert.That(round.HuntActive, Is.False);

            auRound.SetSelectedThreat(null);
            Assert.That(round.TryInitializePredatorRound(out _), Is.True, "other rounds must still get their hunt");
            Assert.That(round.HuntActive, Is.True);
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task SelectingBadBloodEndsARolledHuntButLeavesAStaffHunt(bool rolled)
    {
        await Server.WaitAssertion(() =>
        {
            var round = SEntMan.System<YautjaPredatorRoundSystem>();
            var auRound = SEntMan.System<AuRoundSystem>();
            auRound.SetSelectedThreat(null);
            Assert.That(round.HuntActive, Is.False);
            Assert.That(round.TryInitializePredatorRound(out _), Is.True);
            Assert.That(round.HuntActive, Is.True);

            // The dice remember the hunt they queued; a staff-made hunt is never recorded there.
            var field = typeof(YautjaPredatorRoundSystem).GetField("_rolledHunt",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            if (rolled)
            {
                var hunt = SEntMan.System<GameTicker>().GetAddedGameRules()
                    .Single(uid => SEntMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID == HuntRule.Id);
                field.SetValue(round, (EntityUid?) hunt);
            }
            else
            {
                field.SetValue(round, null);
            }

            try
            {
                auRound.SetSelectedThreat(Server.ProtoMan.Index(BadBloodThreat));
                Assert.That(round.HuntActive, Is.EqualTo(!rolled));
            }
            finally
            {
                // pooled servers are reused: a selected threat left behind blocks hunts in later tests
                auRound.SetSelectedThreat(null);
            }
        });
    }
}
