using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Yautja.BadBlood;
using Content.Shared.CMU14.Threats;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// ARCH-102: the rule entity starts with the threat, picks the Leader and keeps the Superiority Point ledgers
[TestFixture]
public sealed class BadBloodCampRuleTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task ThreatStartsTheRuleAndTheRulePicksTheLeader()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map);

        try
        {
            await Server.WaitAssertion(() =>
            {
                var threat = Server.ProtoMan.Index<ThreatPrototype>("BadBloodClanThreat");
                Assert.That(threat.WinConditions, Does.Contain(BadBloodTestHelpers.RulePrototype.Id),
                    "the threat must start the Bad Blood rule with its win conditions");

                var rules = Server.System<BadBloodCampRuleSystem>();
                var badBlood = Server.System<BadBloodSystem>();

                Assert.That(party.Rule, Is.Not.Null, "the helper starts the rule");
                Assert.That(rules.TryGetRule(out var rule), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(rule.Owner, Is.EqualTo(party.Rule!.Value));
                    Assert.That(rule.Comp.LandingParty, Is.EqualTo(4));
                    Assert.That(rules.GetLeader(), Is.EqualTo(party.Leader));
                    Assert.That(rules.HasLivingLeader(), Is.True);
                    Assert.That(Server.EntMan.HasComponent<BadBloodLeaderComponent>(party.Leader), Is.True);
                    Assert.That(badBlood.IsBadBloodLeader(party.Leader), Is.True);
                    Assert.That(badBlood.IsBadBloodLeader(party.Grunts[0]), Is.False);
                    Assert.That(rules.GetSp(), Is.EqualTo(FixedPoint4.Zero));
                    Assert.That(rules.GetTier(), Is.EqualTo(1));
                    Assert.That(rules.HasLodge(), Is.False);
                    Assert.That(rules.GetShipState(), Is.EqualTo(BadBloodShipState.None));
                    Assert.That(rules.InFoundingGrace(), Is.True);
                });

                // leadership moves as one piece
                Assert.That(rules.SetLeader(party.Grunts[0]), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(rules.GetLeader(), Is.EqualTo(party.Grunts[0]));
                    Assert.That(badBlood.IsBadBloodLeader(party.Grunts[0]), Is.True);
                    Assert.That(badBlood.IsBadBloodLeader(party.Leader), Is.False,
                        "the old Leader's objective job must not make them Leader again");
                    Assert.That(Server.EntMan.HasComponent<BadBloodLeaderComponent>(party.Leader), Is.False);
                });

                var hunter = Server.EntMan.SpawnEntity("CMUMobYautja", map.GridCoords);
                Assert.That(rules.SetLeader(hunter), Is.False, "an honourable hunter cannot lead the clan");
                Assert.That(rules.SetLeader(party.Leader), Is.True);
            });

            // a dead Leader is gone at once
            await BadBloodTestHelpers.Kill(Pair, party.Leader);
            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(rules.IsLeaderGone(), Is.True);
                Assert.That(rules.HasLivingLeader(), Is.False);
            });
        }
        finally
        {
            await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
        }

        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<BadBloodCampRuleSystem>().IsRuleActive(), Is.False);
            Assert.That(Server.EntMan.HasComponent<BadBloodLeaderComponent>(party.Leader), Is.False,
                "ending the rule clears the Leader");
        });
    }

    [Test]
    public async Task LedgersCreditEachBodyOnlyUpToItsCap()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);

        try
        {
            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);

                var first = Server.EntMan.SpawnEntity(null, map.GridCoords);
                var second = Server.EntMan.SpawnEntity(null, map.GridCoords);

                // trophies: two per body
                Assert.Multiple(() =>
                {
                    Assert.That(rules.TryCreditOffering(first), Is.True);
                    Assert.That(rules.TryCreditOffering(first), Is.True);
                    Assert.That(rules.TryCreditOffering(first), Is.False, "a third trophy from one body pays nothing");
                    Assert.That(rules.CanOffer(first), Is.False);
                    Assert.That(rules.CanOffer(second), Is.True);
                    Assert.That(rules.GetSp(), Is.EqualTo(FixedPoint4.New(2)));
                });

                // the spit: loose meat and the corpse share one cap of 3
                Assert.Multiple(() =>
                {
                    Assert.That(rules.TryCreditCook(second, rule.Comp.MeatCookSp), Is.EqualTo(FixedPoint4.New(0.5)));
                    Assert.That(rules.TryCreditCook(second, rule.Comp.CorpseCookSp), Is.EqualTo(FixedPoint4.New(2.5)));
                    Assert.That(rules.TryCreditCook(second, rule.Comp.CorpseCookSp), Is.EqualTo(FixedPoint4.Zero),
                        "a body is cooked for points once");
                    Assert.That(rules.GetCookRemaining(second), Is.EqualTo(FixedPoint4.Zero));
                    Assert.That(rules.GetSp(), Is.EqualTo(FixedPoint4.New(5)));
                    Assert.That(rules.GetLifetimeSp(), Is.EqualTo(FixedPoint4.New(5)));
                });

                // Totem income pays in full and counts half towards tiers
                Assert.That(rules.AddTotemIncome(FixedPoint4.New(2)), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(rules.GetSp(), Is.EqualTo(FixedPoint4.New(7)));
                    Assert.That(rules.GetLifetimeSp(), Is.EqualTo(FixedPoint4.New(6)));
                    Assert.That(rules.GetTier(), Is.EqualTo(1));
                });

                Assert.That(rules.AddSp(FixedPoint4.New(5)), Is.True);
                Assert.That(rules.GetTier(), Is.EqualTo(1), "11 lifetime points are still tier I");
                Assert.That(rules.AddSp(FixedPoint4.New(1)), Is.True);
                Assert.That(rules.GetTier(), Is.EqualTo(2), "12 lifetime points unlock tier II");

                // spending never lowers the tier, a refund never raises it
                Assert.Multiple(() =>
                {
                    Assert.That(rules.TrySpendSp(FixedPoint4.New(100)), Is.False);
                    Assert.That(rules.GetSp(), Is.EqualTo(FixedPoint4.New(13)));
                    Assert.That(rules.TrySpendSp(FixedPoint4.New(4)), Is.True);
                    Assert.That(rules.GetSp(), Is.EqualTo(FixedPoint4.New(9)));
                    Assert.That(rules.RefundSp(FixedPoint4.New(1)), Is.True);
                    Assert.That(rules.GetSp(), Is.EqualTo(FixedPoint4.New(10)));
                    Assert.That(rules.GetLifetimeSp(), Is.EqualTo(FixedPoint4.New(12)));
                    Assert.That(rules.IsTierUnlocked(2), Is.True);
                    Assert.That(rules.IsTierUnlocked(3), Is.False);
                });

                // Totems: the limit holds, and there is no free one before the first Lodge
                Assert.That(rules.TryTakeFreeTotem(), Is.False);
                for (var i = 0; i < rules.GetTotemLimit(); i++)
                {
                    Assert.That(rules.TryRegisterTotem(Server.EntMan.SpawnEntity(null, map.GridCoords)), Is.True);
                }

                Assert.That(rules.TryRegisterTotem(Server.EntMan.SpawnEntity(null, map.GridCoords)), Is.False);
            });
        }
        finally
        {
            await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
        }
    }
}
