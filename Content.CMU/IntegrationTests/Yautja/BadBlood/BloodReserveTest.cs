using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Threats;
using Content.Server.CMU14.Yautja.BadBlood;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Ghost.Roles.Events;
using Content.Shared.CMU14.Threats;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Ghost.Components;
using Content.Shared.Mind;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.UnitTesting.Pool;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// FLOW-116: leftover Bad Blood voters wait in the Blood Reserve instead of the lobby, in the order they were handed
// over. taking another body leaves it, the first Lodge opens the founding squad for the front of the queue once,
// and a reserve member gets a seat before an ordinary ghost. FLOW-602: a member nobody seats returns to the ghost
// pool after ReserveExpiry. FLOW-603: a freed Bad Blood body is the reserve's first for ReserveBodyPriority
[TestFixture]
public sealed class BloodReserveTest : GameTest
{
    private const string Lodge = "CMUBadBloodCamp";
    private const string Observer = "MobObserver";

    private static readonly ProtoId<ThreatPrototype> BadBloodThreat = "BadBloodClanThreat";
    // a plain string: a static ProtoId field would be validated by the YAML linter, and this id must not exist
    private const string UnknownThreat = "CMUTestNoSuchThreat";

    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task LeftoverVotersWaitInTheReserveInOrder()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var dummies = await Server.AddDummySessions(2);
        var voters = dummies.Select(dummy => dummy.UserId).ToList();
        var ghosts = new List<EntityUid>();
        EntityUid body = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);
                Assert.That(Server.ProtoMan.Index(BadBloodThreat).ReserveLeftoverVoters, Is.True,
                    "the Bad Blood threat must hand its leftover voters over instead of respawning them");

                foreach (var dummy in dummies)
                {
                    ghosts.Add(MakeObserver(dummy, map));
                }

                // somebody else's leftovers are not ours
                RaiseLeftovers(UnknownThreat, voters);
                Assert.That(rules.GetReserve(), Is.Empty);

                RaiseLeftovers(BadBloodThreat, voters);
                Assert.That(rules.GetReserve(), Is.EqualTo(voters), "the reserve keeps the order it was handed");
                Assert.That(rules.IsInReserve(voters[0]), Is.True);
                Assert.That(rule.Comp.HeldVoteSeats, Is.EqualTo(rule.Comp.LandingParty + voters.Count),
                    "the living cap has room for every voter the vote held a seat for");

                for (var i = 0; i < dummies.Length; i++)
                {
                    Assert.That(dummies[i].AttachedEntity, Is.EqualTo(ghosts[i]),
                        "a reserve member stays an observer, nobody is sent back to the lobby");
                }

                // handing the same voters over twice does not queue them twice
                RaiseLeftovers(BadBloodThreat, voters);
                Assert.That(rules.GetReserve(), Has.Count.EqualTo(voters.Count));

                // late-joining as a marine: any body that is not a ghost gives the place up
                body = entMan.SpawnEntity(null, map.GridCoords);
                Server.PlayerMan.SetAttachedEntity(dummies[0], body);
            });

            await BadBloodTestHelpers.AdvanceClock(Pair, 3);

            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(rules.IsInReserve(voters[0]), Is.False, "a player who took a body has left the reserve");
                Assert.That(rules.GetReserve(), Is.EqualTo(new[] { voters[1] }));

                Assert.That(rules.LeaveReserve(voters[1]), Is.True);
                Assert.That(rules.GetReserve(), Is.Empty);
                Assert.That(rules.TryJoinReserve(voters[0], prompt: false), Is.False,
                    "a player in a body cannot wait in the reserve");
            });

            // the threat hands its leftovers over before it starts its rules: they wait for the rule
            await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
            await Server.WaitAssertion(() =>
            {
                Assert.That(Server.System<BadBloodCampRuleSystem>().IsRuleActive(), Is.False);
                RaiseLeftovers(BadBloodThreat, new List<NetUserId> { voters[1] });
            });

            await BadBloodTestHelpers.StartBadBloodRule(Pair, party);
            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(rules.GetReserve(), Is.EqualTo(new[] { voters[1] }),
                    "voters handed over before the rule started join the reserve when it starts");
            });
        }
        finally
        {
            await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
            await Cleanup(party, ghosts.Append(body));
        }
    }

    [Test]
    public async Task FoundingSquadOpensOnceForTheReserveOnly()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var dummies = await Server.AddDummySessions(4);
        var reserve = dummies.Take(3).ToArray();
        var outsider = dummies[3];
        var voters = reserve.Select(dummy => dummy.UserId).ToList();
        var ghosts = new List<EntityUid>();
        var spawned = new List<EntityUid>();

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);

                // the test server has next to no players: lift the budget so the reserve is the thing tested
                rule.Comp.ThreatBudgetFraction = 1000f;
                rule.Comp.SquadSpawnTimeout = TimeSpan.FromSeconds(6);
                rule.Comp.DeadBadBloods.Clear();
                Assert.That(rule.Comp.LandingParty, Is.EqualTo(2));

                foreach (var dummy in dummies)
                {
                    ghosts.Add(MakeObserver(dummy, map));
                }

                RaiseLeftovers(BadBloodThreat, voters);
                Assert.That(rules.GetReserve(), Is.EqualTo(voters));

                Assert.That(rules.TryOpenFoundingSquad(out _), Is.False, "there is no Lodge yet");
                Assert.That(rule.Comp.FoundingSquadOpened, Is.False);

                // min(reserve 3, 1 + floor(2 / 2)) = 2
                Assert.That(rules.GetFoundingSquadSeats(), Is.EqualTo(2));

                spawned.Add(entMan.SpawnEntity(Lodge, map.GridCoords));
            });

            await BadBloodTestHelpers.AdvanceClock(Pair, 4);

            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);
                Assert.That(rule.Comp.FoundingSquadOpened, Is.True, "the first Lodge opens the founding squad");

                // the front of the queue arrives together, without a raffle
                for (var i = 0; i < 2; i++)
                {
                    var arrival = reserve[i].AttachedEntity;
                    Assert.That(arrival, Is.Not.Null);
                    spawned.Add(arrival!.Value);
                    Assert.That(entMan.HasComponent<YautjaBadBloodComponent>(arrival.Value), Is.True,
                        $"reserve member {i} must arrive as a Bad Blood");
                    Assert.That(rules.IsInReserve(voters[i]), Is.False, "an arrival has left the reserve");
                }

                Assert.That(reserve[2].AttachedEntity, Is.EqualTo(ghosts[2]), "the third in line has no founding seat");
                Assert.That(rules.GetReserve(), Is.EqualTo(new[] { voters[2] }));
                Assert.That(outsider.AttachedEntity, Is.EqualTo(ghosts[3]));
                Assert.That(rule.Comp.SquadCalls, Is.Empty);

                Assert.That(rules.TryOpenFoundingSquad(out _), Is.False, "the founding squad opens once");

                // the founding seats raise the living cap instead of using up its +2, so a squad can still be bought
                Assert.That(rule.Comp.FoundingSeats, Is.EqualTo(2));
                rule.Comp.HeldVoteSeats = 0;
                Assert.That(rules.GetLivingBadBloodCap(),
                    Is.EqualTo(rule.Comp.LandingParty + rule.Comp.FoundingSeats + rule.Comp.LivingCapBonus));
                Assert.That(Server.System<BadBloodCampSystem>().GetSquadSeatRoom(), Is.GreaterThanOrEqualTo(2),
                    "a full founding squad still leaves room for a squad of 2");

                // a later Lodge does not get another one
                entMan.DeleteEntity(spawned[0]);
                Assert.That(rules.HasLodge(), Is.False);
                spawned.Add(entMan.SpawnEntity(Lodge, map.GridCoords));
                Assert.That(rules.HasLodge(), Is.True);
            });

            await BadBloodTestHelpers.AdvanceClock(Pair, 3);

            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                var camp = Server.System<BadBloodCampSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);
                Assert.That(rule.Comp.SquadCalls, Is.Empty, "only the first Lodge opens a founding squad");
                Assert.That(reserve[2].AttachedEntity, Is.EqualTo(ghosts[2]));

                // every other seat: the reserve goes before an ordinary ghost
                Assert.That(camp.TryOpenSquad(1, out var call, ignoreCaps: true), Is.True);
                Assert.That(call, Is.Not.Null);
                Assert.That(camp.CanTakeSquadSeat(call!, reserve[2], out _), Is.True);
                Assert.That(camp.CanTakeSquadSeat(call!, outsider, out var reason), Is.False,
                    "an ordinary ghost waits while the reserve has someone in it");
                Assert.That(reason, Is.EqualTo("cmu-bad-blood-squad-seat-priority"));

                call!.ReserveOnly = true;
                Assert.That(camp.CanTakeSquadSeat(call, outsider, out reason), Is.False);
                Assert.That(reason, Is.EqualTo("cmu-bad-blood-squad-seat-reserve-only"));
                Assert.That(camp.CanTakeSquadSeat(call, reserve[2], out _), Is.True);
            });
        }
        finally
        {
            await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
            await Cleanup(party, ghosts.Concat(spawned));
        }
    }

    [Test]
    public async Task AnUnseatedReserveMemberReturnsToTheGhostPool()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var dummies = await Server.AddDummySessions(2);
        var member = dummies[0];
        var outsider = dummies[1];
        var ghosts = new List<EntityUid>();
        var spawned = new List<EntityUid>();
        BadBloodSquadCall? call = null;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);
                Assert.That(rule.Comp.ReserveExpiry, Is.EqualTo(TimeSpan.FromMinutes(5)),
                    "the reserve waits five minutes for a body");

                rule.Comp.ReserveExpiry = TimeSpan.FromSeconds(2);
                rule.Comp.ThreatBudgetFraction = 1000f;
                rule.Comp.DeadBadBloods.Clear();

                foreach (var dummy in dummies)
                {
                    ghosts.Add(MakeObserver(dummy, map));
                }

                RaiseLeftovers(BadBloodThreat, new List<NetUserId> { member.UserId });
                Assert.That(rules.GetReserve(), Is.EqualTo(new[] { member.UserId }));
                Assert.That(rule.Comp.ReserveJoinedAt.ContainsKey(member.UserId), Is.True);
                Assert.That(rule.Comp.FoundingSquadOpened, Is.False, "Test setup: no Lodge yet");
            });

            // the reserve was promised a founding seat: its clock does not run before the first Lodge
            await BadBloodTestHelpers.AdvanceClock(Pair, 4);

            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var rules = Server.System<BadBloodCampRuleSystem>();
                var camp = Server.System<BadBloodCampSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);
                Assert.That(rules.IsInReserve(member.UserId), Is.True,
                    "nobody expires before the founding squad has had its chance");

                // the founding squad would seat the member: the rest of this test is about one nobody seats
                rule.Comp.FoundingSquadOpened = true;

                spawned.Add(entMan.SpawnEntity(Lodge, map.GridCoords));
                Assert.That(camp.TryOpenSquad(1, out call, ignoreCaps: true), Is.True);
                Assert.That(camp.CanTakeSquadSeat(call!, outsider, out var reason), Is.False,
                    "Test setup: the waiting member holds an ordinary ghost back");
                Assert.That(reason, Is.EqualTo("cmu-bad-blood-squad-seat-priority"));
            });

            await BadBloodTestHelpers.AdvanceClock(Pair, 4);

            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                var camp = Server.System<BadBloodCampSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);
                Assert.That(rules.IsInReserve(member.UserId), Is.False, "nobody found a body for them in time");
                Assert.That(rule.Comp.ReserveJoinedAt.ContainsKey(member.UserId), Is.False);
                Assert.That(member.AttachedEntity, Is.EqualTo(ghosts[0]),
                    "an expired member goes back to the ghosts, not to the lobby");
                Assert.That(camp.CanTakeSquadSeat(call!, outsider, out _), Is.True,
                    "an expired reserve no longer holds ordinary ghosts back");
            });
        }
        finally
        {
            await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
            await Cleanup(party, ghosts.Concat(spawned));
        }
    }

    [Test]
    public async Task AFreedBodyGoesToTheReserveFirst()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var dummies = await Server.AddDummySessions(2);
        var member = dummies[0];
        var outsider = dummies[1];
        var ghosts = new List<EntityUid>();
        var freed = party.Grunts[0];

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);
                Assert.That(rule.Comp.ReserveBodyPriority, Is.EqualTo(TimeSpan.FromSeconds(30)),
                    "a freed body is the reserve's for thirty seconds");
                rule.Comp.ReserveBodyPriority = TimeSpan.FromSeconds(2);

                foreach (var dummy in dummies)
                {
                    ghosts.Add(MakeObserver(dummy, map));
                }

                RaiseLeftovers(BadBloodThreat, new List<NetUserId> { member.UserId });
                Assert.That(rules.GetReserve(), Is.EqualTo(new[] { member.UserId }));

                // the grunt's player uses /ghost: the body is offered to ghosts again
                var minds = Server.System<SharedMindSystem>();
                Assert.That(minds.TryGetMind(freed, out var mind, out _), Is.True);
                var left = entMan.SpawnEntity(Observer, map.GridCoords);
                ghosts.Add(left);
                minds.TransferTo(mind, left);
                Assert.That(entMan.HasComponent<GhostRoleComponent>(freed), Is.True,
                    "Test setup: the body left with /ghost is a ghost role");

                Assert.That(RequestAllowed(freed, outsider), Is.False, "a freed body is the reserve's first");
                Assert.That(RequestAllowed(freed, member), Is.True, "a reserve member may ask for it at once");

                // the window only holds while somebody waits in the reserve
                rule.Comp.ReserveQueue.Remove(member.UserId);
                Assert.That(RequestAllowed(freed, outsider), Is.True, "with nobody in the reserve the body is open");
                rule.Comp.ReserveQueue.Add(member.UserId);
            });

            await BadBloodTestHelpers.AdvanceClock(Pair, 3);

            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(rules.IsInReserve(member.UserId), Is.True, "Test setup: the member still waits");
                Assert.That(RequestAllowed(freed, outsider), Is.True, "after the window anyone may ask");
            });
        }
        finally
        {
            await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
            await Cleanup(party, ghosts);
        }
    }

    // what the ghost role system asks every tick for each player in the raffle. call on the server thread
    private bool RequestAllowed(EntityUid body, ICommonSession session)
    {
        var role = Server.EntMan.GetComponent<GhostRoleComponent>(body);
        var attempt = new GhostRoleRequestAttemptEvent(session, body, role);
        Server.EntMan.EventBus.RaiseLocalEvent(body, ref attempt);
        return !attempt.Cancelled;
    }

    // call on the server thread
    private EntityUid MakeObserver(ICommonSession session, TestMapData map)
    {
        var ghost = Server.EntMan.SpawnEntity(Observer, map.GridCoords);
        Assert.That(Server.EntMan.HasComponent<GhostComponent>(ghost), Is.True);
        Server.PlayerMan.SetAttachedEntity(session, ghost);
        return ghost;
    }

    // what ThreatSystem raises once a voted threat has seated everyone it can. call on the server thread
    private void RaiseLeftovers(ProtoId<ThreatPrototype> threat, List<NetUserId> voters)
    {
        var ev = new ThreatLeftoverVotersEvent(threat, voters);
        Server.EntMan.EventBus.RaiseEvent(EventSource.Local, ref ev);
    }

    private async Task Cleanup(BadBloodTestParty party, IEnumerable<EntityUid> extra)
    {
        await Server.WaitPost(() =>
        {
            var entMan = Server.EntMan;
            var doomed = extra.Concat(party.All).ToList();
            doomed.AddRange(entMan.EntityQuery<BadBloodSquadSeatsComponent>().Select(comp => comp.Owner));
            doomed.AddRange(entMan.EntityQuery<BadBloodSleepingHoundComponent>().Select(comp => comp.Owner));

            foreach (var session in Server.PlayerMan.Sessions)
            {
                if (session.AttachedEntity is { } attached && doomed.Contains(attached))
                    Server.PlayerMan.SetAttachedEntity(session, null);
            }

            foreach (var uid in doomed)
            {
                if (uid.IsValid() && entMan.EntityExists(uid))
                    entMan.DeleteEntity(uid);
            }
        });
    }
}
