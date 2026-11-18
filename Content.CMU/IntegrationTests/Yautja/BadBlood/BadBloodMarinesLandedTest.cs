using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Yautja.BadBlood;
using Content.Shared._RMC14.Dropship;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// ARCH-601: the rule latches the first dropship landing once and tells the clan's systems about it once
[TestFixture]
public sealed class BadBloodMarinesLandedTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task FirstLandingLatchesOnceAndRaisesOnce()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, startLanded: false);
        EntityUid grid = default;
        TimeSpan? at = null;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);
                Assert.That(rule.Comp.MarinesLanded, Is.False, "the rule starts before the drop");
                Assert.That(rules.HaveMarinesLanded(), Is.False);
                Assert.That(rules.MarinesLandedAt(), Is.Null);

                grid = Server.System<SharedMapSystem>().CreateGridEntity(map.MapId).Owner;
                Land(grid);

                Assert.Multiple(() =>
                {
                    Assert.That(rule.Comp.MarinesLanded, Is.True);
                    Assert.That(rule.Comp.MarinesLandedAt, Is.Not.Null);
                    Assert.That(rules.HaveMarinesLanded(), Is.True);
                });

                at = rule.Comp.MarinesLandedAt;
            });

            // a later landing, on a later tick: runtime bus subscriptions are refused, so "raised once" is proven
            // through the latch (the event is raised only on the false -> true edge)
            await Server.WaitRunTicks(5);

            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);
                Land(grid);

                Assert.Multiple(() =>
                {
                    Assert.That(rule.Comp.MarinesLandedAt, Is.EqualTo(at), "the landing time stays the first one");
                    Assert.That(rules.MarkMarinesLanded(), Is.False, "a latched rule must not raise the event again");
                });
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                if (Server.EntMan.EntityExists(grid))
                    Server.EntMan.DeleteEntity(grid);
            });
            await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
        }
    }

    private void Land(EntityUid grid)
    {
        var ship = Server.EntMan.EnsureComponent<DropshipComponent>(grid);
        var ev = new DropshipLandedOnPlanetEvent((grid, ship));
        Server.EntMan.EventBus.RaiseEvent(EventSource.Local, ref ev);
    }

    [Test]
    public async Task TheHelperStartsLandedByDefault()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map);

        try
        {
            await Server.WaitAssertion(() =>
            {
                Assert.That(Server.System<BadBloodCampRuleSystem>().HaveMarinesLanded(), Is.True,
                    "tests that predate the landing gate start with marines already down");
            });
        }
        finally
        {
            await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
        }
    }
}
