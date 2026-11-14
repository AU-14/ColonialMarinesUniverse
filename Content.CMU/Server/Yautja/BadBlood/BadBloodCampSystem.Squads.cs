using System.Linq;
using Content.Server.Chat.Managers;
using Content.Server.CMU14.Threats;
using Content.Server.GameTicking;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Ghost.Roles.Events;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking;
using Content.Shared.Ghost.Components;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Verbs;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Yautja.BadBlood;

// FLOW-111: reinforcement squads. A call opens seats on a ghost-role entry at the Lodge. Whoever wins the raffle
// holds a seat, and the squad's bodies only spawn, together, once every seat is held or the call times out.
// Seats nobody took are refunded. The Blood Reserve and the clan ship open free seats through TryOpenSquad.
public sealed partial class BadBloodCampSystem
{
    public static readonly EntProtoId SquadSeatsPrototype = "CMUBadBloodSquadSeats";
    public static readonly EntProtoId SquadMemberPrototype = "CMUMobYautjaBadBloodGrunt";

    private static readonly ProtoId<JobPrototype> SquadMemberJob = "AU14JobThreatMember";

    [Dependency] private IChatManager _squadChat = default!;
    [Dependency] private GhostRoleSystem _squadGhostRoles = default!;
    [Dependency] private IPlayerManager _squadPlayers = default!;
    [Dependency] private YautjaPredatorRoundSystem _squadPredatorRound = default!;
    [Dependency] private ThreatSystem _squadThreat = default!;
    [Dependency] private GameTicker _squadTicker = default!;

    private readonly List<ICommonSession> _squadRaffleScratch = new();

    partial void InitializeSquads()
    {
        SubscribeLocalEvent<BadBloodSquadSeatsComponent, GhostRoleRequestAttemptEvent>(OnSquadSeatRequestAttempt);
        SubscribeLocalEvent<BadBloodSquadSeatsComponent, TakeGhostRoleEvent>(OnSquadSeatTake);
        SubscribeLocalEvent<BadBloodSquadCallerComponent, GetVerbsEvent<Verb>>(OnSquadCallerVerbs);
    }

    partial void TickSquads(Entity<BadBloodCampComponent> lodge, Entity<BadBloodCampRuleComponent> rule, TimeSpan now)
    {
        if (rule.Comp.SquadCalls.Count == 0)
            return;

        foreach (var call in rule.Comp.SquadCalls.ToArray())
        {
            if (call.Claimed.Count >= call.Seats || now >= call.SpawnAt)
                ResolveSquadCall(rule, call, GetSquadArrivalPoint(lodge.Owner));
        }
    }

    // the Lodge fell with seats open: whoever already holds one arrives in the ruins, the rest is refunded
    partial void LodgeDestroyedSquads(Entity<BadBloodCampComponent> lodge, Entity<BadBloodCampRuleComponent> rule)
    {
        if (rule.Comp.SquadCalls.Count == 0)
            return;

        var point = GetSquadArrivalPoint(lodge.Owner);
        foreach (var call in rule.Comp.SquadCalls.ToArray())
        {
            ResolveSquadCall(rule, call, point);
        }
    }

    #region Calling a squad

    /// <summary>
    /// The Leader calls squads. While the clan has no living Leader, any Bad Blood can.
    /// </summary>
    public bool CanCallSquad(EntityUid user)
    {
        if (!_badBlood.IsBadBlood(user))
            return false;

        return _badBlood.IsBadBloodLeader(user) || !_rule.HasLivingLeader();
    }

    /// <summary>
    /// What a squad of <paramref name="seats"/> costs: 2 or 3 seats. Null for any other size or without a rule.
    /// </summary>
    public FixedPoint4? GetSquadCost(int seats)
    {
        if (!_rule.TryGetRule(out var rule))
            return null;

        return seats switch
        {
            2 => rule.Comp.SquadOfTwoCost,
            3 => rule.Comp.SquadOfThreeCost,
            _ => null,
        };
    }

    /// <summary>
    /// Seats in calls that have not arrived yet, held or not.
    /// </summary>
    public int GetPendingSquadSeats()
    {
        if (!_rule.TryGetRule(out var rule))
            return 0;

        var seats = 0;
        foreach (var call in rule.Comp.SquadCalls)
        {
            seats += call.Seats;
        }

        return seats;
    }

    /// <summary>
    /// How many more seats the caps allow right now: the living Bad Blood cap, and the threat budget that Bad
    /// Bloods and hounds share. Seats already open count as taken.
    /// </summary>
    public int GetSquadSeatRoom()
    {
        if (!_rule.TryGetRule(out _))
            return 0;

        var bodies = _rule.CountLivingBadBloods() + GetPendingSquadSeats();
        var byCap = _rule.GetLivingBadBloodCap() - bodies;
        return Math.Max(0, Math.Min(byCap, GetSquadBudgetRoom()));
    }

    /// <summary>
    /// How many more seats the threat budget alone allows: Bad Bloods, open seats and hounds against
    /// round(pop x 0.25). The living cap is not counted.
    /// </summary>
    public int GetSquadBudgetRoom()
    {
        if (!_rule.TryGetRule(out _))
            return 0;

        var bodies = _rule.CountLivingBadBloods() + GetPendingSquadSeats();
        return Math.Max(0, _rule.GetThreatBudget() - bodies - _kennelHounds.CountHounds());
    }

    /// <summary>
    /// <paramref name="buyer"/> pays for a squad of 2 or 3 and its seats open at the Lodge. Refused, with a popup
    /// and nothing spent, for the wrong buyer, without a Lodge, over the caps or without the SP.
    /// </summary>
    public bool TryBuySquad(EntityUid buyer, int seats)
    {
        if (GetSquadCost(seats) is not { } cost)
            return false;

        string? reason = null;
        var room = GetSquadSeatRoom();
        if (!CanCallSquad(buyer))
            reason = "cmu-bad-blood-squad-leader-only";
        else if (!TryGetLodge(out _))
            reason = "cmu-bad-blood-squad-no-lodge";
        else if (room < seats)
            reason = "cmu-bad-blood-squad-cap";
        else if (!_rule.TrySpendSp(cost))
            reason = "cmu-bad-blood-squad-cannot-afford";

        if (reason != null)
        {
            var living = _rule.CountLivingBadBloods();
            var pending = GetPendingSquadSeats();
            _popup.PopupEntity(Loc.GetString(reason,
                    ("cost", cost.ToString()),
                    ("seats", seats),
                    ("living", living),
                    ("pending", pending),
                    ("limit", living + pending + room)),
                buyer,
                buyer,
                PopupType.SmallCaution);
            return false;
        }

        if (TryOpenSquad(seats, out _, seatCost: cost / (float) seats, buyer: buyer, ignoreCaps: true))
            return true;

        _rule.RefundSp(cost);
        return false;
    }

    /// <summary>
    /// The seat API: opens <paramref name="seats"/> seats at the standing Lodge without touching the clan's SP.
    /// The Blood Reserve's founding squad, the clan ship's summoning and the last-stand squad call this.
    /// <paramref name="seatCost"/> is only what an unclaimed seat gives back, leave it at zero for free seats.
    /// False without a rule or a Lodge, and when the caps leave no room unless <paramref name="ignoreCaps"/>.
    /// </summary>
    public bool TryOpenSquad(
        int seats,
        out BadBloodSquadCall? call,
        bool reserveOnly = false,
        bool ignoreCaps = false,
        FixedPoint4 seatCost = default,
        EntityUid? buyer = null)
    {
        call = null;
        if (seats <= 0 || !_rule.TryGetRule(out var rule) || !TryGetLodge(out var lodge))
            return false;

        if (!ignoreCaps && GetSquadSeatRoom() < seats)
            return false;

        var now = _timing.CurTime;
        call = new BadBloodSquadCall
        {
            Seats = seats,
            OpenedAt = now,
            SpawnAt = now + rule.Comp.SquadSpawnTimeout,
            SeatCost = seatCost,
            ReserveOnly = reserveOnly,
            Buyer = buyer,
        };

        rule.Comp.SquadCalls.Add(call);

        var entry = Spawn(SquadSeatsPrototype, Transform(lodge.Owner).Coordinates);
        EnsureComp<BadBloodSquadSeatsComponent>(entry).Call = call;

        // the ship opens a single free seat every minute: announcing each one would flood the clan
        if (seats > 1 || seatCost > FixedPoint4.Zero)
            _rule.AnnounceToClan(Loc.GetString("cmu-bad-blood-squad-called", ("seats", seats)));
        return true;
    }

    /// <summary>
    /// One free seat, e.g. the clan ship's summoning tick. See <see cref="TryOpenSquad"/>.
    /// </summary>
    public bool TryOpenFreeSeat(bool reserveOnly = false, bool ignoreCaps = false)
    {
        return TryOpenSquad(1, out _, reserveOnly, ignoreCaps);
    }

    private void OnSquadCallerVerbs(Entity<BadBloodSquadCallerComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !_badBlood.IsBadBlood(args.User))
            return;

        var user = args.User;
        for (var seats = 2; seats <= 3; seats++)
        {
            if (GetSquadCost(seats) is not { } cost)
                continue;

            var size = seats;
            args.Verbs.Add(new Verb
            {
                Text = Loc.GetString("cmu-bad-blood-squad-verb", ("seats", size), ("cost", cost.ToString())),
                Message = Loc.GetString("cmu-bad-blood-squad-verb-message"),
                Priority = -size,
                Act = () => TryBuySquad(user, size),
            });
        }
    }

    #endregion

    #region Seats

    /// <summary>
    /// The ghost-role entry of <paramref name="call"/>, while its seats are open.
    /// </summary>
    public bool TryGetSquadSeats(BadBloodSquadCall call, out EntityUid entry)
    {
        var query = EntityQueryEnumerator<BadBloodSquadSeatsComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Call != call || TerminatingOrDeleted(uid))
                continue;

            entry = uid;
            return true;
        }

        entry = default;
        return false;
    }

    /// <summary>
    /// Whether <paramref name="session"/> may ask for a seat of <paramref name="call"/> right now.
    /// The order is the reserve, then this round's dead Bad Bloods, then anyone: while the clan has either, an
    /// ordinary ghost waits out <see cref="BadBloodCampRuleComponent.DeadPriorityWindow"/> from the call.
    /// This round's honourable hunters never get a seat. <paramref name="reason"/> is a locale id.
    /// </summary>
    public bool CanTakeSquadSeat(BadBloodSquadCall call, ICommonSession session, out string reason)
    {
        reason = "cmu-bad-blood-squad-seat-closed";
        if (!_rule.TryGetRule(out var rule) || !rule.Comp.SquadCalls.Contains(call))
            return false;

        var user = session.UserId;
        if (_squadPredatorRound.WasHunterThisRound(user))
        {
            reason = "cmu-bad-blood-squad-seat-hunter";
            return false;
        }

        if (HoldsSquadSeat(rule, user))
        {
            reason = "cmu-bad-blood-squad-seat-held";
            return false;
        }

        if (call.Claimed.Count >= call.Seats)
            return false;

        var inReserve = rule.Comp.ReserveQueue.Contains(user);
        if (call.ReserveOnly && !inReserve)
        {
            reason = "cmu-bad-blood-squad-seat-reserve-only";
            return false;
        }

        if (inReserve || rule.Comp.DeadBadBloods.ContainsKey(user))
            return true;

        if ((rule.Comp.ReserveQueue.Count > 0 || rule.Comp.DeadBadBloods.Count > 0) &&
            _timing.CurTime < call.OpenedAt + rule.Comp.DeadPriorityWindow)
        {
            reason = "cmu-bad-blood-squad-seat-priority";
            return false;
        }

        return true;
    }

    private static bool HoldsSquadSeat(Entity<BadBloodCampRuleComponent> rule, NetUserId user)
    {
        foreach (var call in rule.Comp.SquadCalls)
        {
            if (call.Claimed.Contains(user))
                return true;
        }

        return false;
    }

    // lower goes first: the reserve in queue order, then dead Bad Bloods, then everyone else
    private static int GetSquadSeatRank(Entity<BadBloodCampRuleComponent> rule, NetUserId user)
    {
        var queued = rule.Comp.ReserveQueue.IndexOf(user);
        if (queued >= 0)
            return queued;

        return rule.Comp.DeadBadBloods.ContainsKey(user) ? int.MaxValue - 1 : int.MaxValue;
    }

    private void OnSquadSeatRequestAttempt(Entity<BadBloodSquadSeatsComponent> ent, ref GhostRoleRequestAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        var reason = "cmu-bad-blood-squad-seat-closed";
        if (ent.Comp.Call is { } call && CanTakeSquadSeat(call, args.Player, out reason))
            return;

        args.Cancelled = true;
        if (args.ExplicitRequest)
            _squadChat.DispatchServerMessage(args.Player, Loc.GetString(reason));
    }

    // the raffle is over and this player was drawn. nothing spawns here: they hold a seat until the squad arrives
    private void OnSquadSeatTake(Entity<BadBloodSquadSeatsComponent> ent, ref TakeGhostRoleEvent args)
    {
        args.TookRole = false;
        if (ent.Comp.Call is not { } call ||
            !_rule.TryGetRule(out var rule) ||
            !CanTakeSquadSeat(call, args.Player, out _))
        {
            return;
        }

        var winner = args.Player;
        var rank = GetSquadSeatRank(rule, winner.UserId);

        _squadRaffleScratch.Clear();
        if (TryComp(ent, out GhostRoleRaffleComponent? raffle))
        {
            foreach (var member in raffle.CurrentMembers)
            {
                if (member != winner && CanTakeSquadSeat(call, member, out _))
                    _squadRaffleScratch.Add(member);
            }
        }

        // someone better placed is in the same raffle: refuse, and the decider moves on until it reaches them
        foreach (var member in _squadRaffleScratch)
        {
            if (GetSquadSeatRank(rule, member.UserId) < rank)
            {
                _squadRaffleScratch.Clear();
                return;
            }
        }

        HoldSquadSeat(call, winner);
        args.TookRole = true;

        // one raffle fills every open seat, best placed first, so a squad does not need a raffle per seat in a row
        _spitRandom.Shuffle(_squadRaffleScratch);
        foreach (var member in _squadRaffleScratch.OrderBy(member => GetSquadSeatRank(rule, member.UserId)).ToArray())
        {
            if (call.Claimed.Count >= call.Seats)
                break;

            HoldSquadSeat(call, member);
        }

        _squadRaffleScratch.Clear();

        // the next tick spawns a full squad
        if (call.Claimed.Count >= call.Seats)
            call.SpawnAt = _timing.CurTime;
    }

    private void HoldSquadSeat(BadBloodSquadCall call, ICommonSession session)
    {
        call.Claimed.Add(session.UserId);

        var seconds = (int) Math.Ceiling(Math.Max(0, (call.SpawnAt - _timing.CurTime).TotalSeconds));
        _squadChat.DispatchServerMessage(session, Loc.GetString("cmu-bad-blood-squad-seat-won", ("seconds", seconds)));
    }

    #endregion

    #region Arrival

    // beside the Ascension Stone if the Lodge has one, so nobody spawns inside the throne. null if nothing can
    // spawn there any more, e.g. the map is going away
    private EntityCoordinates? GetSquadArrivalPoint(EntityUid lodge)
    {
        var at = lodge;
        var stones = EntityQueryEnumerator<BadBloodCampPartComponent>();
        while (stones.MoveNext(out var uid, out var part))
        {
            if (part.Kind != BadBloodCampPartKind.AscensionStone || TerminatingOrDeleted(uid))
                continue;

            at = uid;
            break;
        }

        var xform = Transform(at);
        if (xform.MapUid is not { } map || TerminatingOrDeleted(map))
            return null;

        if (xform.GridUid is { } grid && TerminatingOrDeleted(grid))
            return null;

        return _transform.GetMoverCoordinates(at, xform);
    }

    // a player who took another body, or left, since they won the seat does not arrive
    private bool IsSquadSessionFree(ICommonSession session)
    {
        if (session.Status is SessionStatus.Disconnected or SessionStatus.Zombie)
            return false;

        return session.AttachedEntity is not { } attached || HasComp<GhostComponent>(attached);
    }

    private void ResolveSquadCall(Entity<BadBloodCampRuleComponent> rule, BadBloodSquadCall call, EntityCoordinates? point)
    {
        rule.Comp.SquadCalls.Remove(call);

        var arrived = 0;
        var hasEntry = TryGetSquadSeats(call, out var entry);
        if (point is { } at && hasEntry && TryComp(entry, out GhostRoleComponent? role))
        {
            foreach (var user in call.Claimed)
            {
                if (!_squadPlayers.TryGetSessionById(user, out var session) || !IsSquadSessionFree(session))
                    continue;

                var body = Spawn(SquadMemberPrototype, at);
                _squadGhostRoles.GhostRoleInternalCreateMindAndTransfer(session, entry, body, role);

                // the threat role makes marine objectives see the arrival
                _squadThreat.ApplyThreatRole(body, SquadMemberJob);

                if (_squadTicker.PlayerGameStatuses.TryGetValue(user, out var status) &&
                    status != PlayerGameStatus.JoinedGame)
                {
                    _squadTicker.PlayerJoinGame(session);
                }

                arrived++;
            }
        }

        if (hasEntry)
            QueueDel(entry);

        var unclaimed = call.Seats - arrived;
        var refund = FixedPoint4.Zero;
        if (unclaimed > 0 && call.SeatCost > FixedPoint4.Zero)
        {
            // seat costs are a squad price split by its seats, round the sum back to whole hundredths
            refund = FixedPoint4.New(Math.Round(call.SeatCost.Double() * unclaimed, 2));
            _rule.RefundSp(refund);
        }

        if (arrived > 0)
            _rule.AnnounceToClan(Loc.GetString("cmu-bad-blood-squad-arrived", ("count", arrived)));

        if (unclaimed > 0 && (unclaimed > 1 || call.SeatCost > FixedPoint4.Zero))
        {
            _rule.AnnounceToClan(Loc.GetString(
                refund > FixedPoint4.Zero ? "cmu-bad-blood-squad-unclaimed-refund" : "cmu-bad-blood-squad-unclaimed",
                ("count", unclaimed),
                ("refund", refund.ToString())));
        }
    }

    #endregion
}
