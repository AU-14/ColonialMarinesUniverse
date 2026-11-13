using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Pair;
using Content.Server.CMU14.Yautja.BadBlood;
using Content.Server.GameTicking;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Destructible;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// FLOW-120: the Ship Call-In. Every timer is a rule field, shortened here. The rule's decision is read off the rule
// entity, the pooled server has no round to end
[TestFixture]
public sealed class ShipCallInTest : GameTest
{
    private const string Lodge = "CMUBadBloodCamp";
    private const string Totem = "CMUBadBloodTotem";

    // the summoning cap is a share of the player count, so one player has to be there
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task HeldRipeTotemsCallTheShipAndLosingTheLodgeTurnsItBack()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var spawned = new List<EntityUid>();
        EntityUid lodge = default;
        var regenBefore = 1f;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);
                Assert.That(Server.System<GameTicker>().RunLevel, Is.Not.EqualTo(GameRunLevel.InRound));
                Assert.That(rule.Comp.ShipInboundTime, Is.EqualTo(TimeSpan.FromMinutes(20)));

                // the lock has lifted and the Totems have been held, but there is no Lodge to call from
                rule.Comp.ShipEarliestCall = TimeSpan.Zero;
                rule.Comp.RipeHoldForCall = TimeSpan.Zero;
                SpawnRipeTotems(map, spawned, 3);
                Assert.That(Server.System<BadBloodTotemSystem>().CountRipeTotems(), Is.EqualTo(3));
                Assert.That(rules.TryCallShip(), Is.False);
                regenBefore = RegenMultiplier(party.Grunts[0]);
            });

            await Pair.RunSeconds(2);
            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(rules.GetShipState(), Is.EqualTo(BadBloodShipState.None), "the call needs a standing Lodge");
                lodge = Server.EntMan.SpawnEntity(Lodge, map.GridCoords);
            });

            await Pair.RunSeconds(2);
            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.Multiple(() =>
                {
                    Assert.That(rules.IsShipInbound(), Is.True, "three ripe Totems held together call the ship");
                    Assert.That(rules.GetShipTimeLeft(), Is.GreaterThan(TimeSpan.FromMinutes(19)), "the ship is 20 minutes out");
                    Assert.That(Server.System<BadBloodCampSystem>().IsLodgeRevealed(), Is.True,
                        "the Lodge is on the marine map while the ship is inbound");
                    Assert.That(RegenMultiplier(party.Grunts[0]), Is.EqualTo(regenBefore * 1.25f).Within(0.001f),
                        "+25% bracer regen while the ship is inbound");
                    Assert.That(rules.GetTier(), Is.LessThan(4));
                });

                // destroying the Lodge turns the ship back
                Server.EntMan.DeleteEntity(lodge);
                Assert.Multiple(() =>
                {
                    Assert.That(rules.GetShipState(), Is.EqualTo(BadBloodShipState.None));
                    Assert.That(rules.GetShipTimeLeft(), Is.Null);
                    Assert.That(RegenMultiplier(party.Grunts[0]), Is.EqualTo(regenBefore).Within(0.001f));
                });
            });

            await Pair.RunSeconds(2);
            await Server.WaitAssertion(() =>
            {
                Assert.That(Server.System<BadBloodCampRuleSystem>().GetShipState(), Is.EqualTo(BadBloodShipState.None),
                    "a turned-back ship stays away while no Lodge stands");
            });
        }
        finally
        {
            spawned.Add(lodge);
            await Cleanup(party, spawned);
        }
    }

    // STATE-304: the hold and the lock. Three ripe Totems must be held for the whole hold, and the call never comes
    // before the lock lifts. A hold that finished early calls the ship the moment the lock lifts
    [Test]
    public async Task TheHoldMustBeCompleteAndTheLockMustHaveLifted()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var spawned = new List<EntityUid>();

        try
        {
            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);

                // "before minute 45", with a hold of three seconds
                rule.Comp.ShipEarliestCall = TimeSpan.FromHours(5);
                rule.Comp.RipeHoldForCall = TimeSpan.FromSeconds(3);

                spawned.Add(Server.EntMan.SpawnEntity(Lodge, map.GridCoords));
                SpawnRipeTotems(map, spawned, 3);
                Assert.That(Server.System<BadBloodTotemSystem>().CountRipeTotems(), Is.EqualTo(3));
            });

            await Pair.RunSeconds(1.5f);
            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);
                Assert.That(rule.Comp.RipeHoldStartedAt, Is.Not.Null);
                Assert.That(rules.GetShipState(), Is.EqualTo(BadBloodShipState.None), "the Totems have to be held first");
            });

            await Pair.RunSeconds(4);
            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);
                Assert.That(rules.GetShipState(), Is.EqualTo(BadBloodShipState.None),
                    "held long enough, but the ship is never called before the lock lifts");

                // the lock lifts three seconds from now
                rule.Comp.ShipEarliestCall = Server.Timing.CurTime - rule.Comp.StartedAt + TimeSpan.FromSeconds(3);
            });

            await Pair.RunSeconds(1.5f);
            await Server.WaitAssertion(() =>
            {
                Assert.That(Server.System<BadBloodCampRuleSystem>().GetShipState(), Is.EqualTo(BadBloodShipState.None),
                    "still locked");
            });

            await Pair.RunSeconds(3);
            await Server.WaitAssertion(() =>
            {
                Assert.That(Server.System<BadBloodCampRuleSystem>().IsShipInbound(), Is.True,
                    "a hold that finished before the lock calls the ship when the lock lifts");
            });
        }
        finally
        {
            await Cleanup(party, spawned);
        }
    }

    // STATE-304: dropping below three ripe Totems resets the hold, two ripe Totems never call the ship however long
    // the clock runs, and a full hold after the lock has lifted does
    [Test]
    public async Task LosingARipeTotemResetsTheHoldAndTwoNeverCall()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var spawned = new List<EntityUid>();
        TimeSpan firstHold = default;
        TimeSpan secondHold = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);

                // "after minute 45", with a hold of four seconds
                rule.Comp.ShipEarliestCall = TimeSpan.Zero;
                rule.Comp.RipeHoldForCall = TimeSpan.FromSeconds(4);

                spawned.Add(Server.EntMan.SpawnEntity(Lodge, map.GridCoords));
                SpawnRipeTotems(map, spawned, 3);
            });

            await Pair.RunSeconds(2);
            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);
                Assert.That(rule.Comp.RipeHoldStartedAt, Is.Not.Null);
                Assert.That(rules.GetShipState(), Is.EqualTo(BadBloodShipState.None), "half a hold does not call the ship");
                firstHold = rule.Comp.RipeHoldStartedAt!.Value;

                // the marines take one down mid-hold
                Server.EntMan.DeleteEntity(spawned[^1]);
                Assert.That(Server.System<BadBloodTotemSystem>().CountRipeTotems(), Is.EqualTo(2));
            });

            await Pair.RunSeconds(1.5f);
            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);
                Assert.That(rule.Comp.RipeHoldStartedAt, Is.Null, "dropping to two ripe Totems resets the hold");
            });

            // far longer than the hold, with two ripe Totems, a Lodge and no lock
            await Pair.RunSeconds(8);
            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(rule.Comp.RipeHoldStartedAt, Is.Null);
                    Assert.That(rules.GetShipState(), Is.EqualTo(BadBloodShipState.None),
                        "fewer than three ripe Totems never call the ship");
                });

                // the clan raises the third again: the hold starts over
                SpawnRipeTotems(map, spawned, 1, firstColumn: 4);
                Assert.That(Server.System<BadBloodTotemSystem>().CountRipeTotems(), Is.EqualTo(3));
            });

            await Pair.RunSeconds(1.5f);
            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);
                Assert.That(rule.Comp.RipeHoldStartedAt, Is.Not.Null);
                secondHold = rule.Comp.RipeHoldStartedAt!.Value;
                Assert.Multiple(() =>
                {
                    Assert.That(secondHold, Is.GreaterThan(firstHold), "the new hold counts from when the third stood again");
                    Assert.That(rules.GetShipState(), Is.EqualTo(BadBloodShipState.None));
                });
            });

            await Pair.RunSeconds(4.5f);
            await Server.WaitAssertion(() =>
            {
                Assert.That(Server.System<BadBloodCampRuleSystem>().IsShipInbound(), Is.True,
                    "three ripe Totems held for the whole hold, after the lock, call the ship");
            });
        }
        finally
        {
            await Cleanup(party, spawned);
        }
    }

    // STATE-304: a hold that finished before the lock is worth nothing if a Totem is lost before the lock lifts
    [Test]
    public async Task LosingATotemBeforeTheLockLiftsMeansNoCall()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var spawned = new List<EntityUid>();

        try
        {
            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);

                // the lock lifts in five seconds, the hold is one second
                rule.Comp.ShipEarliestCall = Server.Timing.CurTime - rule.Comp.StartedAt + TimeSpan.FromSeconds(5);
                rule.Comp.RipeHoldForCall = TimeSpan.FromSeconds(1);

                spawned.Add(Server.EntMan.SpawnEntity(Lodge, map.GridCoords));
                SpawnRipeTotems(map, spawned, 3);
            });

            await Pair.RunSeconds(2.5f);
            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(rule.Comp.RipeHoldStartedAt, Is.Not.Null);
                    Assert.That(Server.Timing.CurTime - rule.Comp.RipeHoldStartedAt!.Value, Is.GreaterThan(rule.Comp.RipeHoldForCall),
                        "the hold is complete");
                    Assert.That(rules.GetShipState(), Is.EqualTo(BadBloodShipState.None), "but the lock has not lifted");
                });

                Server.EntMan.DeleteEntity(spawned[^1]);
            });

            await Pair.RunSeconds(5);
            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(Server.Timing.CurTime - rule.Comp.StartedAt, Is.GreaterThan(rule.Comp.ShipEarliestCall),
                        "the lock has lifted");
                    Assert.That(rules.GetShipState(), Is.EqualTo(BadBloodShipState.None),
                        "a Totem lost before the lock lifts means no call");
                });
            });
        }
        finally
        {
            await Cleanup(party, spawned);
        }
    }

    [Test]
    public async Task ArrivalSummonsUpToTheCapUnlocksTierFourAndLeavesTheLodgeDestructible()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 2);
        var spawned = new List<EntityUid>();
        EntityUid lodge = default;
        var cap = 0;
        var seatsAtDeath = 0;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);
                Assert.That(rules.GetPlayerCount(), Is.GreaterThan(0));

                rule.Comp.ShipInboundTime = TimeSpan.FromSeconds(1);
                rule.Comp.SummonInterval = TimeSpan.FromSeconds(1);
                rule.Comp.SquadSpawnTimeout = TimeSpan.FromHours(1);
                rule.Comp.DepartureTime = TimeSpan.FromHours(1);
                rule.Comp.ReserveQueue.Clear();

                // round(players x fraction) = the three already here + 2
                // the landing hound counts against the same budget as the Bad Bloods
                var hounds = Server.System<BadBloodHoundSystem>().CountHounds();
                rule.Comp.ThreatBudgetFraction = (5f + hounds) / rules.GetPlayerCount();
                cap = rules.GetThreatBudget() - rules.CountLivingBadBloods() - hounds;
                Assert.That(cap, Is.EqualTo(2));

                lodge = Server.EntMan.SpawnEntity(Lodge, map.GridCoords);
                Assert.That(rules.TryCallShip(), Is.True);
                Assert.That(rules.TryCallShip(), Is.False, "the ship is called once");
                Assert.That(rule.Comp.SquadCalls, Is.Empty);
            });

            await Pair.RunSeconds(8);

            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                var camp = Server.System<BadBloodCampSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(rules.IsShipArrived(), Is.True);
                    Assert.That(rule.Comp.TierFourUnlocked, Is.True);
                    Assert.That(rules.GetTier(), Is.EqualTo(4), "tier IV unlocks on arrival");
                    Assert.That(rules.GetDepartureRemaining(), Is.Not.Null);
                    Assert.That(camp.GetPendingSquadSeats(), Is.EqualTo(cap),
                        "a seat opens every interval, up to round(pop x 0.25) Bad Bloods and hounds");
                    Assert.That(rules.GetRoundOutcome(), Is.EqualTo(BadBloodRoundOutcome.None));
                });

                // more room, then the Leader falls: summoning pauses
                rule.Comp.ThreatBudgetFraction = 1000f;
            });

            await BadBloodTestHelpers.Kill(Pair, party.Leader);
            await Server.WaitAssertion(() =>
            {
                seatsAtDeath = Server.System<BadBloodCampSystem>().GetPendingSquadSeats();
            });

            await Pair.RunSeconds(4);

            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                var camp = Server.System<BadBloodCampSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(camp.GetPendingSquadSeats(), Is.EqualTo(seatsAtDeath),
                        "nobody is summoned while the clan has no living Leader");
                    Assert.That(rule.Comp.NextSummonAt, Is.Null);
                });

                // a new Leader is seated, as the ascension vote would
                Assert.That(rules.SetLeader(party.Grunts[0]), Is.True);
            });

            await Pair.RunSeconds(4);

            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                var camp = Server.System<BadBloodCampSystem>();
                Assert.That(camp.GetPendingSquadSeats(), Is.GreaterThan(seatsAtDeath), "summoning resumes with a Leader");

                // the Lodge is as destructible after arrival as before
                Assert.That(Server.System<SharedDestructibleSystem>().DestroyEntity(lodge), Is.True,
                    "the Lodge stays destructible in the ship phase");
                Assert.Multiple(() =>
                {
                    Assert.That(rules.HasLodge(), Is.False);
                    Assert.That(rules.IsShipArrived(), Is.True, "the ship stays in orbit");
                    Assert.That(camp.GetPendingSquadSeats(), Is.Zero);
                });
            });

            await Pair.RunSeconds(3);

            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(rule.Comp.SquadCalls, Is.Empty, "nobody is summoned to a ruin");
                    Assert.That(rule.Comp.NextSummonAt, Is.Null);
                    Assert.That(rules.GetRoundOutcome(), Is.EqualTo(BadBloodRoundOutcome.None),
                        "the Leader lives, the round goes on");
                });
            });

            // the main rule holds in the ship phase too
            await BadBloodTestHelpers.Kill(Pair, party.Grunts[0]);
            await Pair.RunSeconds(2);

            await Server.WaitAssertion(() =>
            {
                Assert.That(Server.System<BadBloodCampRuleSystem>().GetRoundOutcome(),
                    Is.EqualTo(BadBloodRoundOutcome.MarinesLeaderAndLodge),
                    "Leader dead and Lodge destroyed ends the round in the ship phase");
            });
        }
        finally
        {
            spawned.Add(lodge);
            await Cleanup(party, spawned);
        }
    }

    [Test]
    public async Task LodgeStandingThroughTheDepartureTimerWinsForTheClan()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var spawned = new List<EntityUid>();
        EntityUid lodge = default;
        TimeSpan paused = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);
                rule.Comp.ShipInboundTime = TimeSpan.FromSeconds(1);
                rule.Comp.SummonInterval = TimeSpan.FromHours(1);
                rule.Comp.DepartureTime = TimeSpan.FromSeconds(12);
                rule.Comp.ClanWipeDelay = TimeSpan.FromSeconds(1);

                lodge = Server.EntMan.SpawnEntity(Lodge, map.GridCoords);
                Assert.That(rules.GetDepartureRemaining(), Is.Null, "no departure timer before the ship is here");
                Assert.That(rules.TryCallShip(), Is.True);
            });

            await Pair.RunSeconds(5);

            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(rules.IsShipArrived(), Is.True);
                var left = rules.GetDepartureRemaining();
                Assert.That(left, Is.Not.Null);
                Assert.That(left!.Value, Is.LessThan(TimeSpan.FromSeconds(12)), "the timer runs while the Lodge stands");
                Assert.That(left.Value, Is.GreaterThan(TimeSpan.FromSeconds(5)));

                // the Lodge falls: the timer stops where it is
                Server.EntMan.DeleteEntity(lodge);
                paused = rules.GetDepartureRemaining()!.Value;
            });

            await Pair.RunSeconds(4);

            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.Multiple(() =>
                {
                    Assert.That(rules.GetDepartureRemaining(), Is.EqualTo(paused), "no Lodge, no countdown");
                    Assert.That(rules.GetRoundOutcome(), Is.EqualTo(BadBloodRoundOutcome.None));
                    Assert.That(rules.IsShipArrived(), Is.True);
                });

                // re-pitched: it resumes from what was left
                lodge = Server.EntMan.SpawnEntity(Lodge, map.GridCoords);
            });

            // the whole clan dies with the Lodge standing: Clan Wipe is suspended once the ship is here
            foreach (var body in party.All)
            {
                await BadBloodTestHelpers.Kill(Pair, body);
            }

            await Pair.RunSeconds(3);

            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);
                var left = rules.GetDepartureRemaining();
                Assert.Multiple(() =>
                {
                    Assert.That(rules.GetRoundOutcome(), Is.EqualTo(BadBloodRoundOutcome.None),
                        "no Clan Wipe in the ship phase: marines have to take the Lodge");
                    Assert.That(rule.Comp.WipeCollapseAt, Is.Null);
                    Assert.That(left, Is.Not.Null);
                    Assert.That(left!.Value, Is.LessThan(paused), "the timer runs again");
                    Assert.That(left.Value, Is.GreaterThan(TimeSpan.Zero));
                });
            });

            await Pair.RunSeconds((float) paused.TotalSeconds + 1f);

            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.Multiple(() =>
                {
                    Assert.That(rules.HasLodge(), Is.True);
                    Assert.That(rules.GetRoundOutcome(), Is.EqualTo(BadBloodRoundOutcome.BadBloodDeparture),
                        "the Lodge stood until the ship left: the Bad Bloods win");
                });
            });
        }
        finally
        {
            spawned.Add(lodge);
            await Cleanup(party, spawned);
        }
    }

    // Totems that are ripe the moment they stand and pay nothing, 30 tiles apart. call on the server thread
    private void SpawnRipeTotems(TestMapData map, List<EntityUid> spawned, int count, int firstColumn = 1)
    {
        for (var i = 0; i < count; i++)
        {
            var totem = Server.EntMan.SpawnEntity(Totem, map.GridCoords.Offset(new Vector2(30 * (firstColumn + i), 0)));
            Server.EntMan.GetComponent<BadBloodTotemComponent>(totem).RipeAfter = TimeSpan.Zero;
            spawned.Add(totem);
        }
    }

    // what the bracer regen is scaled by for this wearer right now. call on the server thread
    private float RegenMultiplier(EntityUid wearer)
    {
        var ev = new YautjaBracerRegenModifyEvent(wearer);
        Server.EntMan.EventBus.RaiseLocalEvent(wearer, ref ev);
        return ev.Multiplier;
    }

    private async Task Cleanup(BadBloodTestParty party, List<EntityUid> spawned)
    {
        // the Lodge goes first: it closes any open seats with it
        await Server.WaitPost(() =>
        {
            for (var i = spawned.Count - 1; i >= 0; i--)
            {
                if (Server.EntMan.EntityExists(spawned[i]))
                    Server.EntMan.DeleteEntity(spawned[i]);
            }
        });

        await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
    }
}
