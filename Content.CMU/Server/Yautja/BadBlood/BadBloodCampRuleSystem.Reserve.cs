using Content.Server.CMU14.Threats;
using Content.Shared._RMC14.Dialog;
using Content.Shared.CMU14.Threats;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.GameTicking;
using Content.Shared.Ghost.Components;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Yautja.BadBlood;

// FLOW-116: the Blood Reserve. Threat voters who got no landing body wait here, as observers, in queue order.
// The squad and hound seat checks read BadBloodCampRuleComponent.ReserveQueue, so keeping that list right is all
// reserve-first seating needs. The first Lodge opens the founding squad for them, free of charge.
//
// Dependency fields taken by this file: _reserveDialog. BadBloodCampSystem is looked up when it is needed instead
// of injected, so this file cannot clash with another partial that injects it.
public sealed partial class BadBloodCampRuleSystem
{
    [Dependency] private DialogSystem _reserveDialog = default!;

    // voters handed over before the rule started: the threat raises the event first and starts its rules after
    private readonly List<NetUserId> _reservePending = new();
    private int _reservePendingRound;

    // the first Lodge was pitched and its founding squad has not been opened yet
    private bool _reserveFoundingDue;

    private readonly List<NetUserId> _reserveScratch = new();

    partial void InitializeReserve()
    {
        SubscribeLocalEvent<ThreatLeftoverVotersEvent>(OnReserveLeftoverVoters);
        SubscribeLocalEvent<BadBloodReserveLobbyEvent>(OnReserveLobbyChosen);
    }

    partial void StartedReserve(Entity<BadBloodCampRuleComponent> rule)
    {
        _reserveFoundingDue = false;

        if (_reservePending.Count > 0 && _reservePendingRound == GameTicker.RoundId)
            AddVotersToReserve(rule, _reservePending);

        _reservePending.Clear();
    }

    partial void EndedReserve(Entity<BadBloodCampRuleComponent> rule)
    {
        rule.Comp.ReserveQueue.Clear();
        rule.Comp.ReserveJoinedAt.Clear();
        _reservePending.Clear();
        _reserveFoundingDue = false;
    }

    partial void TickReserve(Entity<BadBloodCampRuleComponent> rule, TimeSpan now)
    {
        PruneReserve(rule);

        if (!_reserveFoundingDue)
            return;

        _reserveFoundingDue = false;
        TryOpenFoundingSquad(out _);
    }

    partial void LodgePitchedReserve(Entity<BadBloodCampRuleComponent> rule, EntityUid lodge, bool first)
    {
        // opened on the next tick, once the Lodge has its parts
        if (first && !rule.Comp.FoundingSquadOpened)
            _reserveFoundingDue = true;
    }

    #region The queue

    /// <summary>
    /// The Blood Reserve, first in line first. Empty without a rule.
    /// </summary>
    public IReadOnlyList<NetUserId> GetReserve()
    {
        return TryGetRule(out var rule) ? rule.Comp.ReserveQueue : Array.Empty<NetUserId>();
    }

    public bool IsInReserve(NetUserId user)
    {
        return TryGetRule(out var rule) && rule.Comp.ReserveQueue.Contains(user);
    }

    /// <summary>
    /// Puts <paramref name="user"/> at the back of the reserve. With <paramref name="prompt"/> they get the
    /// dialog that lets them return to the lobby instead. Doing nothing keeps them in the reserve.
    /// False without a rule, for a player who is gone or in a body, and for one already queued.
    /// </summary>
    public bool TryJoinReserve(NetUserId user, bool prompt = true)
    {
        if (!TryGetRule(out var rule) ||
            rule.Comp.ReserveQueue.Contains(user) ||
            !_player.TryGetSessionById(user, out var session) ||
            !CanWaitInReserve(session))
        {
            return false;
        }

        rule.Comp.ReserveQueue.Add(user);
        rule.Comp.ReserveJoinedAt[user] = Timing.CurTime; // CMU14: FLOW-602
        ChatManager.DispatchServerMessage(session, Loc.GetString("cmu-bad-blood-reserve-joined",
            ("place", rule.Comp.ReserveQueue.Count)));

        if (prompt)
            OpenReserveDialog(rule, session);

        return true;
    }

    /// <summary>
    /// Takes <paramref name="user"/> out of the reserve. With <paramref name="toLobby"/> they are also sent back
    /// to the lobby, to join the round as someone else.
    /// </summary>
    public bool LeaveReserve(NetUserId user, bool toLobby = false)
    {
        if (!TryGetRule(out var rule) || !rule.Comp.ReserveQueue.Remove(user))
            return false;

        rule.Comp.ReserveJoinedAt.Remove(user); // CMU14: FLOW-602

        if (toLobby && _player.TryGetSessionById(user, out var session) && CanWaitInReserve(session))
            GameTicker.Respawn(session);

        return true;
    }

    // a reserve member is an observer. anyone in a body, back in the lobby or gone has left the reserve
    private bool CanWaitInReserve(ICommonSession session)
    {
        if (session.Status is SessionStatus.Disconnected or SessionStatus.Zombie)
            return false;

        if (session.AttachedEntity is { } attached)
            return HasComp<GhostComponent>(attached);

        return !GameTicker.PlayerGameStatuses.TryGetValue(session.UserId, out var status) ||
               status == PlayerGameStatus.JoinedGame;
    }

    private void PruneReserve(Entity<BadBloodCampRuleComponent> rule)
    {
        var queue = rule.Comp.ReserveQueue;
        var joined = rule.Comp.ReserveJoinedAt;
        if (queue.Count == 0)
        {
            joined.Clear();
            return;
        }

        var now = Timing.CurTime;
        for (var i = queue.Count - 1; i >= 0; i--)
        {
            var user = queue[i];
            if (!_player.TryGetSessionById(user, out var session) || !CanWaitInReserve(session))
            {
                queue.RemoveAt(i);
                joined.Remove(user);
                continue;
            }

            // CMU14: FLOW-602, nobody found a body for them in time: back to the ghost pool, still an observer.
            // a member queued without a join time (tests add to the list directly) counts from now, and so does
            // everyone until the founding squad opens: the reserve was promised a seat in it, so the clock only starts
            // once the first Lodge stands
            if (!rule.Comp.FoundingSquadOpened || !joined.TryGetValue(user, out var joinedAt))
            {
                joined[user] = now;
                continue;
            }

            // a member holding a squad seat has found a body, it just has not arrived yet
            if (rule.Comp.ReserveExpiry <= TimeSpan.Zero ||
                now - joinedAt < rule.Comp.ReserveExpiry ||
                HoldsReserveSeat(rule, user))
            {
                continue;
            }

            queue.RemoveAt(i);
            joined.Remove(user);
            ChatManager.DispatchServerMessage(session, Loc.GetString("cmu-bad-blood-reserve-expired"));
        }
    }

    private static bool HoldsReserveSeat(Entity<BadBloodCampRuleComponent> rule, NetUserId user)
    {
        foreach (var call in rule.Comp.SquadCalls)
        {
            if (call.Claimed.Contains(user))
                return true;
        }

        return false;
    }

    private void AddVotersToReserve(Entity<BadBloodCampRuleComponent> rule, IReadOnlyList<NetUserId> voters)
    {
        // the vote held a seat for every one of them: the living cap must have room for the whole reserve
        rule.Comp.HeldVoteSeats = Math.Max(rule.Comp.HeldVoteSeats, rule.Comp.LandingParty + voters.Count);

        foreach (var voter in voters)
        {
            TryJoinReserve(voter);
        }
    }

    private void OpenReserveDialog(Entity<BadBloodCampRuleComponent> rule, ICommonSession session)
    {
        // only an observer has the dialog window. a player without one simply stays in the reserve
        if (session.AttachedEntity is not { } ghost || !HasComp<GhostComponent>(ghost))
            return;

        var seconds = (int) Math.Ceiling(rule.Comp.ReserveChoiceTime.TotalSeconds);
        var minutes = (int) Math.Ceiling(rule.Comp.ReserveExpiry.TotalMinutes);
        var options = new List<DialogOption>
        {
            new(Loc.GetString("cmu-bad-blood-reserve-dialog-stay")),
            new(Loc.GetString("cmu-bad-blood-reserve-dialog-lobby"), new BadBloodReserveLobbyEvent(session.UserId)),
        };

        // closing the window or letting it run out keeps the default: the reserve
        _reserveDialog.OpenOptions(
            ghost,
            ghost,
            Loc.GetString("cmu-bad-blood-reserve-dialog-title"),
            options,
            Loc.GetString(rule.Comp.ReserveExpiry > TimeSpan.Zero
                    ? "cmu-bad-blood-reserve-dialog-message"
                    : "cmu-bad-blood-reserve-dialog-message-no-expiry",
                ("seconds", seconds), ("minutes", minutes)),
            timeout: rule.Comp.ReserveChoiceTime);
    }

    private void OnReserveLeftoverVoters(ref ThreatLeftoverVotersEvent ev)
    {
        if (ev.Voters.Count == 0 || !IsBadBloodThreat(ev.ThreatId))
            return;

        if (TryGetRule(out var rule))
        {
            AddVotersToReserve(rule, ev.Voters);
            return;
        }

        _reservePending.Clear();
        _reservePending.AddRange(ev.Voters);
        _reservePendingRound = GameTicker.RoundId;
    }

    // a threat is ours when it starts the Bad Blood rule
    private bool IsBadBloodThreat(ProtoId<ThreatPrototype> threatId)
    {
        if (!ProtoMan.TryIndex(threatId, out var threat))
            return false;

        foreach (var condition in threat.WinConditions)
        {
            if (ProtoMan.TryIndex<EntityPrototype>(condition, out var proto) &&
                proto.TryComp<BadBloodCampRuleComponent>(out _, Factory))
            {
                return true;
            }
        }

        return false;
    }

    private void OnReserveLobbyChosen(BadBloodReserveLobbyEvent ev)
    {
        if (!LeaveReserve(ev.UserId, toLobby: true))
            return;

        if (_player.TryGetSessionById(ev.UserId, out var session))
            ChatManager.DispatchServerMessage(session, Loc.GetString("cmu-bad-blood-reserve-left"));
    }

    #endregion

    #region The founding squad

    /// <summary>
    /// Seats of the founding squad as it would open now: one per reserve member, at most
    /// FoundingSquadBase + LandingParty / FoundingSquadDivisor (1 + half the landing party by default), and never
    /// more than the threat budget leaves room for. The living cap does not bound it: the founding squad raises
    /// that cap by its own seats (see <see cref="GetLivingBadBloodCap"/>).
    /// </summary>
    public int GetFoundingSquadSeats()
    {
        if (!TryGetRule(out var rule))
            return 0;

        PruneReserve(rule);
        var divisor = rule.Comp.FoundingSquadDivisor;
        var max = rule.Comp.FoundingSquadBase + (divisor > 0 ? rule.Comp.LandingParty / divisor : 0);
        var seats = Math.Min(rule.Comp.ReserveQueue.Count, max);
        return Math.Max(0, Math.Min(seats, EntityManager.System<BadBloodCampSystem>().GetSquadBudgetRoom()));
    }

    /// <summary>
    /// Opens the founding squad at the standing Lodge: free, reserve-only seats that go to the front of the
    /// reserve without a raffle. It opens once a round, on the first Lodge, even if nobody is waiting by then.
    /// The rule calls this itself, one tick after the first Lodge is pitched.
    /// </summary>
    public bool TryOpenFoundingSquad(out BadBloodSquadCall? call)
    {
        call = null;
        if (!TryGetRule(out var rule) || rule.Comp.FoundingSquadOpened || !HasLodge())
            return false;

        rule.Comp.FoundingSquadOpened = true;

        var seats = GetFoundingSquadSeats();
        if (seats <= 0)
            return false;

        // the founding seats lift the living cap first, so they never eat the +2 a bought squad needs
        rule.Comp.FoundingSeats = seats;
        var camp = EntityManager.System<BadBloodCampSystem>();
        if (!camp.TryOpenSquad(seats, out call, reserveOnly: true) || call == null)
        {
            rule.Comp.FoundingSeats = 0;
            return false;
        }

        // queue order, no raffle. a seat nobody could be given stays open to the rest of the reserve
        _reserveScratch.Clear();
        _reserveScratch.AddRange(rule.Comp.ReserveQueue);
        foreach (var user in _reserveScratch)
        {
            if (call.Claimed.Count >= call.Seats)
                break;

            if (!_player.TryGetSessionById(user, out var session) || !camp.CanTakeSquadSeat(call, session, out _))
                continue;

            call.Claimed.Add(user);
            ChatManager.DispatchServerMessage(session, Loc.GetString("cmu-bad-blood-reserve-founding-seat"));
        }

        _reserveScratch.Clear();

        // a full squad arrives on the Lodge's next tick
        if (call.Claimed.Count >= call.Seats)
            call.SpawnAt = Timing.CurTime;

        return true;
    }

    #endregion
}
