using Content.Server.CMU14.RoundStatistics;
using Content.Server.GameTicking;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Shared.Player;

namespace Content.Server.CMU14.Yautja.BadBlood;

// FLOW-114, the end rule. It replaces KillAllYautjaRule on Bad Blood rounds.
//   Marines win when the Leader is gone and no Lodge stands, in every phase. Founding grace covers an early death.
//   Clan Wipe: nobody is left in the clan. It calls one last-stand squad if it can pay for it, and the round ends
//   if nobody is back when the timer runs out. Suspended once the clan ship has arrived.
// Every decision is checked again right before the round ends and is recorded on BadBloodCampEndComponent.
// No dependency fields here: other systems are looked up when they are needed, so this file cannot collide with
// the fields of the other partial files.
public sealed partial class BadBloodCampRuleSystem
{
    private const string LeaderAndLodgeSource = "BadBloodCampRule";
    private const string ClanWipeSource = "BadBloodClanWipe";
    private const int LastStandSeats = 2;

    private readonly List<EntityUid> _endScratch = new();

    partial void StartedEnd(Entity<BadBloodCampRuleComponent> rule)
    {
        var end = EnsureComp<BadBloodCampEndComponent>(rule.Owner);
        end.Outcome = BadBloodRoundOutcome.None;
        end.DecidedAt = null;
        end.AbsentSince.Clear();

        rule.Comp.WipeCollapseAt = null;
        rule.Comp.LastStandSquadOpened = false;
    }

    partial void EndedEnd(Entity<BadBloodCampRuleComponent> rule)
    {
        rule.Comp.WipeCollapseAt = null;
    }

    partial void TickEnd(Entity<BadBloodCampRuleComponent> rule, TimeSpan now)
    {
        var end = EnsureComp<BadBloodCampEndComponent>(rule.Owner);
        if (end.Outcome != BadBloodRoundOutcome.None)
            return;

        if (IsMarineVictoryDue())
        {
            TryDecideRound(BadBloodRoundOutcome.MarinesLeaderAndLodge);
            return;
        }

        TickClanWipe(rule, end, now);
    }

    #region Queries

    /// <summary>
    /// What the rule has decided for this round. None while it is undecided and on rounds without the rule.
    /// </summary>
    public BadBloodRoundOutcome GetRoundOutcome()
    {
        return TryGetRule(out var rule) && TryComp(rule.Owner, out BadBloodCampEndComponent? end)
            ? end.Outcome
            : BadBloodRoundOutcome.None;
    }

    /// <summary>
    /// The main rule, true in every phase: the Leader is gone and no Lodge stands. Founding grace covers a Leader
    /// lost before the first Lodge.
    /// </summary>
    public bool IsMarineVictoryDue()
    {
        return TryGetRule(out _) && IsLeaderGone() && !HasLodge() && !InFoundingGrace();
    }

    /// <summary>
    /// Nobody is left in the clan: no living Bad Blood with a player. A living one without a player still counts
    /// until it has been without for <see cref="BadBloodCampRuleComponent.LeaderSsdTimeout"/>, so a dropped
    /// connection does not wipe the clan. False for a clan that never had anyone.
    /// </summary>
    public bool IsClanWiped()
    {
        if (!TryGetRule(out var rule) || !ClanEverExisted(rule))
            return false;

        return !AnyBadBloodPresent(rule, EnsureComp<BadBloodCampEndComponent>(rule.Owner), Timing.CurTime);
    }

    private static bool ClanEverExisted(Entity<BadBloodCampRuleComponent> rule)
    {
        return rule.Comp.LandingParty > 0 || rule.Comp.LeaderEverAssigned || rule.Comp.DeadBadBloods.Count > 0;
    }

    private bool AnyBadBloodPresent(Entity<BadBloodCampRuleComponent> rule, BadBloodCampEndComponent end, TimeSpan now)
    {
        _endScratch.Clear();
        foreach (var uid in end.AbsentSince.Keys)
        {
            if (TerminatingOrDeleted(uid))
                _endScratch.Add(uid);
        }

        foreach (var uid in _endScratch)
        {
            end.AbsentSince.Remove(uid);
        }

        var present = false;
        var query = EntityQueryEnumerator<YautjaBadBloodComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out _, out var mobState))
        {
            if (_mobState.IsDead(uid, mobState))
            {
                end.AbsentSince.Remove(uid);
                continue;
            }

            if (HasComp<ActorComponent>(uid))
            {
                end.AbsentSince.Remove(uid);
                present = true;
                continue;
            }

            if (!end.AbsentSince.TryGetValue(uid, out var since))
            {
                since = now;
                end.AbsentSince[uid] = now;
            }

            if (now - since < rule.Comp.LeaderSsdTimeout)
                present = true;
        }

        return present;
    }

    #endregion

    #region Clan Wipe

    private void TickClanWipe(Entity<BadBloodCampRuleComponent> rule, BadBloodCampEndComponent end, TimeSpan now)
    {
        var comp = rule.Comp;

        // always run the headcount, it keeps the absence ledger current
        var present = AnyBadBloodPresent(rule, end, now);

        // the ship refills the clan for as long as the Lodge stands, so marines have to take the Lodge
        if (present || comp.ShipState == BadBloodShipState.Arrived || !ClanEverExisted(rule))
        {
            comp.WipeCollapseAt = null;
            if (present)
                comp.LastStandSquadOpened = false;

            return;
        }

        if (!comp.LastStandSquadOpened && TryOpenLastStandSquad(rule))
        {
            comp.LastStandSquadOpened = true;
            comp.WipeCollapseAt = now + comp.ClanWipeDelay;
            return;
        }

        comp.WipeCollapseAt ??= now + comp.ClanWipeDelay;
        if (now >= comp.WipeCollapseAt)
            TryDecideRound(BadBloodRoundOutcome.MarinesClanWipe);
    }

    // the clan pays for its cheapest squad by itself. seats nobody takes are refunded as usual
    private bool TryOpenLastStandSquad(Entity<BadBloodCampRuleComponent> rule)
    {
        var camp = EntityManager.System<BadBloodCampSystem>();
        var cost = rule.Comp.SquadOfTwoCost;
        if (!HasLodge() || camp.GetPendingSquadSeats() > 0 || !TrySpendSp(cost))
            return false;

        if (!camp.TryOpenSquad(LastStandSeats, out _, ignoreCaps: true, seatCost: cost / (float) LastStandSeats))
        {
            RefundSp(cost);
            return false;
        }

        AnnounceToClan(Loc.GetString("cmu-bad-blood-end-last-stand"), PopupType.LargeCaution);
        return true;
    }

    #endregion

    #region Deciding the round

    /// <summary>
    /// Decides the round as <paramref name="outcome"/> if its conditions hold right now, records it and, when a
    /// round is running, ends it. False if the round is already decided or the conditions do not hold.
    /// </summary>
    public bool TryDecideRound(BadBloodRoundOutcome outcome)
    {
        if (outcome == BadBloodRoundOutcome.None || !TryGetRule(out var rule))
            return false;

        var end = EnsureComp<BadBloodCampEndComponent>(rule.Owner);
        if (end.Outcome != BadBloodRoundOutcome.None)
            return false;

        var now = Timing.CurTime;

        // the same checks that brought us here, once more: a Lodge pitched or a Leader seated in between saves it
        var holds = outcome switch
        {
            BadBloodRoundOutcome.MarinesLeaderAndLodge => IsMarineVictoryDue(),
            BadBloodRoundOutcome.MarinesClanWipe =>
                rule.Comp.ShipState != BadBloodShipState.Arrived &&
                ClanEverExisted(rule) &&
                !AnyBadBloodPresent(rule, end, now),
            BadBloodRoundOutcome.BadBloodDeparture =>
                rule.Comp.ShipState == BadBloodShipState.Arrived &&
                HasLodge() &&
                rule.Comp.DepartureRemaining is { } left &&
                left <= TimeSpan.Zero,
            _ => false,
        };

        if (!holds)
        {
            if (outcome == BadBloodRoundOutcome.MarinesClanWipe)
                rule.Comp.WipeCollapseAt = null;

            return false;
        }

        end.Outcome = outcome;
        end.DecidedAt = now;

        var ev = new BadBloodRoundDecidedEvent(rule, outcome);
        RaiseLocalEvent(ref ev);

        // no round to end, e.g. the rule was started by hand in the lobby. the decision stands all the same
        if (GameTicker.RunLevel != GameRunLevel.InRound)
            return true;

        var stats = EntityManager.System<CMURoundStatisticsSystem>();
        string text;
        switch (outcome)
        {
            case BadBloodRoundOutcome.MarinesLeaderAndLodge:
                stats.RecordThreatDefeatedRule(LeaderAndLodgeSource);
                text = Loc.GetString("cmu-bad-blood-end-marines-win");
                break;
            case BadBloodRoundOutcome.MarinesClanWipe:
                stats.RecordThreatDefeatedRule(ClanWipeSource);
                text = Loc.GetString("cmu-bad-blood-end-clan-wipe");
                break;
            default:
                // the statistics have no outcome of their own for a departure. threat-survived is the closest one:
                // the threat wins without wiping the marines
                stats.RecordThreatSurviveRule();
                text = Loc.GetString("cmu-bad-blood-end-departure");
                break;
        }

        GameTicker.EndRound(text);
        return true;
    }

    #endregion
}
