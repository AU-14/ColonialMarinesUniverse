using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Yautja.BadBlood;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Ghost.Roles.Events;
using Content.Shared.CMU14.Threats;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.UnitTesting.Pool;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// FLOW-111: a squad call opens seats at the Lodge, the squad arrives together, unclaimed seats are refunded,
// the reserve and the fallen go first, arrivals count for marine objectives and the caps hold
[TestFixture]
public sealed class SquadCallTest : GameTest
{
    private const string Lodge = "CMUBadBloodCamp";

    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task SquadArrivesTogetherAndUnclaimedSeatsAreRefunded()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var grunt = party.Grunts[0];
        var session = Pair.Player!;
        EntityUid lodge = default;
        EntityUid entry = default;
        EntityUid? arrival = null;
        BadBloodSquadCall? call = null;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var rules = Server.System<BadBloodCampRuleSystem>();
                var camp = Server.System<BadBloodCampSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);

                // the test server has next to no players: lift the population caps so the purchase is the thing tested
                rule.Comp.ThreatBudgetFraction = 1000f;
                rule.Comp.HeldVoteSeats = 10;
                rule.Comp.SquadSpawnTimeout = TimeSpan.FromSeconds(6);
                rule.Comp.ReserveQueue.Clear();
                rule.Comp.DeadBadBloods.Clear();

                Server.PlayerMan.SetAttachedEntity(session, null);

                Assert.That(camp.TryBuySquad(party.Leader, 2), Is.False, "there is no Lodge to call a squad to");

                lodge = entMan.SpawnEntity(Lodge, map.GridCoords);
                Assert.That(rules.GetLodge(), Is.EqualTo(lodge));

                Assert.That(rules.AddSp(FixedPoint4.New(20)), Is.True);
                Assert.That(camp.TryBuySquad(grunt, 2), Is.False, "a grunt cannot call a squad while the Leader lives");
                Assert.That(camp.TryBuySquad(party.Leader, 4), Is.False, "squads come in twos and threes");
                Assert.That(rules.GetSp(), Is.EqualTo(FixedPoint4.New(20)), "a refused call costs nothing");

                Assert.That(camp.TryBuySquad(party.Leader, 2), Is.True);
                Assert.That(rule.Comp.SquadOfTwoCost, Is.EqualTo(FixedPoint4.New(12)));
                Assert.That(rules.GetSp(), Is.EqualTo(FixedPoint4.New(20) - rule.Comp.SquadOfTwoCost), "a squad of two costs 12 SP");
                Assert.That(rule.Comp.SquadCalls, Has.Count.EqualTo(1));
                call = rule.Comp.SquadCalls[0];
                Assert.That(camp.GetPendingSquadSeats(), Is.EqualTo(2));
                Assert.That(camp.TryGetSquadSeats(call, out entry), Is.True);
                Assert.That(entMan.HasComponent<GhostRoleComponent>(entry), Is.True, "the seats are a ghost role");

                // priority: with nobody waiting an ordinary ghost may ask at once
                Assert.That(RequestAllowed(entry, session), Is.True);

                // a fallen Bad Blood is waiting: the ordinary ghost has to let them go first
                var fallen = new NetUserId(Guid.NewGuid());
                rule.Comp.DeadBadBloods[fallen] = Server.Timing.CurTime;
                Assert.That(RequestAllowed(entry, session), Is.False, "the clan's fallen go before ordinary ghosts");
                Assert.That(camp.CanTakeSquadSeat(call, session, out var reason), Is.False);
                Assert.That(reason, Is.EqualTo("cmu-bad-blood-squad-seat-priority"));

                // the fallen and the reserve do not wait
                rule.Comp.DeadBadBloods[session.UserId] = Server.Timing.CurTime;
                Assert.That(RequestAllowed(entry, session), Is.True);
                rule.Comp.DeadBadBloods.Remove(session.UserId);
                rule.Comp.ReserveQueue.Add(session.UserId);
                Assert.That(RequestAllowed(entry, session), Is.True);

                // a reserve-only call is closed to everyone else
                call.ReserveOnly = true;
                rule.Comp.ReserveQueue.Clear();
                Assert.That(RequestAllowed(entry, session), Is.False, "reserve-only seats are for the reserve");
                call.ReserveOnly = false;
                rule.Comp.ReserveQueue.Add(session.UserId);

                // the raffle draws the player: they hold a seat, but nothing spawns yet
                var take = new TakeGhostRoleEvent(session);
                entMan.EventBus.RaiseLocalEvent(entry, ref take);
                Assert.That(take.TookRole, Is.True);
                Assert.That(call.Claimed, Is.EquivalentTo(new[] { session.UserId }));
                Assert.That(CountClan(map), Is.EqualTo(2), "no body exists until the squad arrives");

                var again = new TakeGhostRoleEvent(session);
                entMan.EventBus.RaiseLocalEvent(entry, ref again);
                Assert.That(again.TookRole, Is.False, "one player holds one seat");
            });

            await BadBloodTestHelpers.AdvanceClock(Pair, 2);

            await Server.WaitAssertion(() =>
            {
                Assert.That(CountClan(map), Is.EqualTo(2), "a squad with an open seat waits for its timeout");
                Assert.That(session.AttachedEntity, Is.Null);
            });

            await BadBloodTestHelpers.AdvanceClock(Pair, 7);

            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);

                arrival = session.AttachedEntity;
                Assert.That(arrival, Is.Not.Null, "the seat holder arrives when the call times out");
                Assert.Multiple(() =>
                {
                    Assert.That(entMan.GetComponent<MetaDataComponent>(arrival!.Value).EntityPrototype?.ID,
                        Is.EqualTo(BadBloodTestHelpers.GruntPrototype), "arrivals are always grunts");
                    Assert.That(entMan.HasComponent<YautjaBadBloodComponent>(arrival.Value), Is.True);
                    Assert.That(entMan.TryGetComponent(arrival.Value, out ThreatComponent? threat), Is.True,
                        "an arrival gets the threat role, so marine objectives see it");
                    Assert.That(threat?.ObjectiveJob, Is.EqualTo(BadBloodTestHelpers.MemberJob));
                    Assert.That(rule.Comp.SquadCalls, Is.Empty);
                    Assert.That(entMan.Deleted(entry), Is.True,
                        "the ghost role closes with the call");
                    Assert.That(rules.GetSp(), Is.EqualTo(FixedPoint4.New(14)), "the unclaimed seat gives its 6 SP back");
                });
            });

            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                var camp = Server.System<BadBloodCampSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);

                // three living Bad Bloods under a cap of four: one more seat, not two
                rule.Comp.HeldVoteSeats = 0;
                rule.Comp.FoundingSeats = 0;
                rule.Comp.LandingParty = 2;
                Assert.That(rules.GetLivingBadBloodCap(), Is.EqualTo(4));
                Assert.That(camp.GetSquadSeatRoom(), Is.EqualTo(1));
                Assert.That(camp.TryBuySquad(party.Leader, 2), Is.False, "the living cap holds");
                Assert.That(rules.GetSp(), Is.EqualTo(FixedPoint4.New(14)));

                // the free seat API obeys the caps too, unless it is told not to
                Assert.That(camp.TryOpenSquad(2, out _), Is.False);
                Assert.That(camp.TryOpenFreeSeat(), Is.True);
                Assert.That(rules.GetSp(), Is.EqualTo(FixedPoint4.New(14)), "a free seat costs nothing");
                Assert.That(camp.GetSquadSeatRoom(), Is.EqualTo(0));
            });

            // a free seat that nobody takes closes without a refund
            await BadBloodTestHelpers.AdvanceClock(Pair, 8);

            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);
                Assert.That(rule.Comp.SquadCalls, Is.Empty);
                Assert.That(rules.GetSp(), Is.EqualTo(FixedPoint4.New(14)));
            });
        }
        finally
        {
            await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
            await Server.WaitPost(() =>
            {
                var entMan = Server.EntMan;
                var entries = entMan.EntityQuery<BadBloodSquadSeatsComponent>().Select(comp => comp.Owner).ToList();
                foreach (var uid in entries)
                {
                    entMan.DeleteEntity(uid);
                }

                if (arrival is { } body && entMan.EntityExists(body))
                    entMan.DeleteEntity(body);

                if (entMan.EntityExists(lodge))
                    entMan.DeleteEntity(lodge);

                foreach (var member in party.All)
                {
                    if (entMan.EntityExists(member))
                        entMan.DeleteEntity(member);
                }
            });
        }
    }

    // what the ghost role system asks every tick for each player in the raffle
    private bool RequestAllowed(EntityUid entry, ICommonSession session)
    {
        var role = Server.EntMan.GetComponent<GhostRoleComponent>(entry);
        var attempt = new GhostRoleRequestAttemptEvent(session, entry, role);
        Server.EntMan.EventBus.RaiseLocalEvent(entry, ref attempt);
        return !attempt.Cancelled;
    }

    private int CountClan(TestMapData map)
    {
        var entMan = Server.EntMan;
        return entMan.EntityQuery<YautjaBadBloodComponent>()
            .Count(comp => entMan.GetComponent<TransformComponent>(comp.Owner).MapID == map.MapId);
    }
}
