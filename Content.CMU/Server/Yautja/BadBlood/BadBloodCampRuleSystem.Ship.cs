using Content.Shared._RMC14.Marines.Announce;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Popups;

namespace Content.Server.CMU14.Yautja.BadBlood;

// FLOW-120, the Ship Call-In.
//   The call: once RipeTotemsForCall ripe Totems have stood together for RipeHoldForCall without a break, and never
//   before ShipEarliestCall. A hold that completes earlier calls the ship the moment that lock lifts, if the Totems
//   are still ripe then. Nothing else calls it: a clan without its ripe Totems never sees the ship. Only with a
//   Lodge standing. ShipEarliestCall is counted from the start of the rule.
//   Inbound: ShipInboundTime. Destroying the Lodge turns the ship back, and it can be called again.
//   Arrived: tier IV, one free seat every SummonInterval up to the threat budget (paused with no Leader or no
//   Lodge), and the departure timer. The Lodge stays as destructible as it was. No ship grid is loaded.
//   Departure: the Lodge has stood for DepartureTime since the arrival. The timer only runs while a Lodge stands.
// No dependency fields here: other systems are looked up when they are needed, so this file cannot collide with
// the fields of the other partial files.
public sealed partial class BadBloodCampRuleSystem
{
    // minutes left at which the departure countdown is announced, highest first
    private static readonly int[] DepartureMarks = { 10, 5, 1 };

    partial void InitializeShip()
    {
        SubscribeLocalEvent<YautjaBadBloodComponent, YautjaBracerRegenModifyEvent>(OnShipRegenModify);
    }

    partial void StartedShip(Entity<BadBloodCampRuleComponent> rule)
    {
        ResetShip(rule);
    }

    partial void EndedShip(Entity<BadBloodCampRuleComponent> rule)
    {
        ResetShip(rule);
    }

    private void ResetShip(Entity<BadBloodCampRuleComponent> rule)
    {
        var comp = rule.Comp;
        comp.ShipState = BadBloodShipState.None;
        comp.ShipArrivesAt = null;
        comp.NextSummonAt = null;
        comp.TierFourUnlocked = false;
        comp.RipeHoldStartedAt = null;
        comp.DepartureRemaining = null;
        comp.DepartureAnnounced.Clear();

        if (TryComp(rule.Owner, out BadBloodCampEndComponent? end))
            end.DepartureCountedAt = null;
    }

    partial void TickShip(Entity<BadBloodCampRuleComponent> rule, TimeSpan now)
    {
        UpdateRipeHold(rule, now);

        switch (rule.Comp.ShipState)
        {
            case BadBloodShipState.None:
                if (IsShipCallDue(rule, now))
                    CallShip(rule, now);
                break;
            case BadBloodShipState.Inbound:
                if (!HasLodge())
                    TurnShipBack(rule);
                else if (rule.Comp.ShipArrivesAt is not { } arrives || now >= arrives)
                    ArriveShip(rule, now);
                break;
            case BadBloodShipState.Arrived:
                TickSummoning(rule, now);
                TickDeparture(rule, now);
                break;
        }
    }

    partial void LodgeDestroyedShip(Entity<BadBloodCampRuleComponent> rule, EntityUid lodge)
    {
        switch (rule.Comp.ShipState)
        {
            case BadBloodShipState.Inbound:
                TurnShipBack(rule);
                break;
            case BadBloodShipState.Arrived:
                // nothing is summoned to a ruin, and the departure timer stops where it is
                rule.Comp.NextSummonAt = null;
                if (rule.Comp.DepartureRemaining is { } left && left > TimeSpan.Zero)
                {
                    var minutes = MinutesLeft(left);
                    AnnounceToClan(Loc.GetString("cmu-bad-blood-ship-departure-paused-clan", ("minutes", minutes)),
                        PopupType.LargeCaution);
                    AnnounceShipToMarines(Loc.GetString("cmu-bad-blood-ship-departure-paused-marines", ("minutes", minutes)));
                }

                break;
        }
    }

    partial void LodgePitchedShip(Entity<BadBloodCampRuleComponent> rule, EntityUid lodge, bool first)
    {
        if (rule.Comp.ShipState != BadBloodShipState.Arrived ||
            rule.Comp.DepartureRemaining is not { } left ||
            left <= TimeSpan.Zero)
        {
            return;
        }

        var minutes = MinutesLeft(left);
        AnnounceToClan(Loc.GetString("cmu-bad-blood-ship-departure-resumed-clan", ("minutes", minutes)));
        AnnounceShipToMarines(Loc.GetString("cmu-bad-blood-ship-departure-resumed-marines", ("minutes", minutes)));
    }

    #region The call

    /// <summary>
    /// Time until the inbound ship arrives. Null unless it is inbound.
    /// </summary>
    public TimeSpan? GetShipTimeLeft()
    {
        if (!TryGetRule(out var rule) ||
            rule.Comp.ShipState != BadBloodShipState.Inbound ||
            rule.Comp.ShipArrivesAt is not { } arrives)
        {
            return null;
        }

        var left = arrives - Timing.CurTime;
        return left > TimeSpan.Zero ? left : TimeSpan.Zero;
    }

    /// <summary>
    /// What is left of the departure timer. Null before the ship has arrived.
    /// </summary>
    public TimeSpan? GetDepartureRemaining()
    {
        return TryGetRule(out var rule) ? rule.Comp.DepartureRemaining : null;
    }

    /// <summary>
    /// Calls the clan ship now, whatever the lock and the Totems say. False without a rule or a Lodge, and when
    /// the ship is already inbound or here.
    /// </summary>
    public bool TryCallShip()
    {
        if (!TryGetRule(out var rule) || rule.Comp.ShipState != BadBloodShipState.None || !HasLodge())
            return false;

        CallShip(rule, Timing.CurTime);
        return true;
    }

    // the hold is tracked with or without a Lodge, the call itself waits for one
    private void UpdateRipeHold(Entity<BadBloodCampRuleComponent> rule, TimeSpan now)
    {
        var comp = rule.Comp;
        if (comp.ShipState != BadBloodShipState.None)
            return;

        var ripe = EntityManager.System<BadBloodTotemSystem>().CountRipeTotems();
        if (comp.RipeTotemsForCall > 0 && ripe >= comp.RipeTotemsForCall)
            comp.RipeHoldStartedAt ??= now;
        else
            comp.RipeHoldStartedAt = null;
    }

    private bool IsShipCallDue(Entity<BadBloodCampRuleComponent> rule, TimeSpan now)
    {
        var comp = rule.Comp;
        if (!HasLodge())
            return false;

        // the hold is cleared the tick the ripe count drops, so a hold that is still running means the Totems are
        // still ripe
        return now - comp.StartedAt >= comp.ShipEarliestCall &&
               comp.RipeHoldStartedAt is { } since &&
               now - since >= comp.RipeHoldForCall;
    }

    private void CallShip(Entity<BadBloodCampRuleComponent> rule, TimeSpan now)
    {
        var comp = rule.Comp;
        comp.ShipState = BadBloodShipState.Inbound;
        comp.ShipArrivesAt = now + comp.ShipInboundTime;

        // the Lodge is on the marine map for as long as the ship is inbound or here
        EntityManager.System<BadBloodCampSystem>().RevealLodge();

        var minutes = MinutesLeft(comp.ShipInboundTime);
        AnnounceToClan(Loc.GetString("cmu-bad-blood-ship-called-clan", ("minutes", minutes)), PopupType.Large);
        AnnounceShipToMarines(Loc.GetString("cmu-bad-blood-ship-called-marines", ("minutes", minutes)));
    }

    private void TurnShipBack(Entity<BadBloodCampRuleComponent> rule)
    {
        var comp = rule.Comp;
        if (comp.ShipState != BadBloodShipState.Inbound)
            return;

        comp.ShipState = BadBloodShipState.None;
        comp.ShipArrivesAt = null;
        // a turned-back ship has to be earned again: three ripe Totems held for the full hold
        comp.RipeHoldStartedAt = null;

        AnnounceToClan(Loc.GetString("cmu-bad-blood-ship-turned-back-clan"), PopupType.LargeCaution);
        AnnounceShipToMarines(Loc.GetString("cmu-bad-blood-ship-turned-back-marines"));
    }

    // +25% bracer regen while the ship is on its way
    private void OnShipRegenModify(Entity<YautjaBadBloodComponent> ent, ref YautjaBracerRegenModifyEvent args)
    {
        if (TryGetRule(out var rule) && rule.Comp.ShipState == BadBloodShipState.Inbound)
            args.Multiplier *= 1f + Math.Max(0f, rule.Comp.ShipRegenBonus);
    }

    #endregion

    #region Arrival and summoning

    private void ArriveShip(Entity<BadBloodCampRuleComponent> rule, TimeSpan now)
    {
        var comp = rule.Comp;
        comp.ShipState = BadBloodShipState.Arrived;
        comp.ShipArrivesAt = null;
        comp.TierFourUnlocked = true;
        comp.NextSummonAt = now + comp.SummonInterval;
        comp.DepartureRemaining = comp.DepartureTime;
        comp.DepartureAnnounced.Clear();
        comp.WipeCollapseAt = null;
        EnsureComp<BadBloodCampEndComponent>(rule.Owner).DepartureCountedAt = now;

        var minutes = MinutesLeft(comp.DepartureTime);
        AnnounceToClan(Loc.GetString("cmu-bad-blood-ship-arrived-clan", ("minutes", minutes)), PopupType.Large);
        AnnounceShipToMarines(Loc.GetString("cmu-bad-blood-ship-arrived-marines", ("minutes", minutes)));
        GrantArrivalDiscs();
    }

    // tier IV: the ship drops one free smart disc to every living Bad Blood, once, on arrival
    private void GrantArrivalDiscs()
    {
        var hands = EntityManager.System<Content.Shared.Hands.EntitySystems.SharedHandsSystem>();
        var clan = new System.Collections.Generic.List<EntityUid>();
        var query = EntityQueryEnumerator<YautjaBadBloodComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (_mobState.IsAlive(uid))
                clan.Add(uid);
        }

        foreach (var uid in clan)
        {
            var disc = Spawn("CMUYautjaSmartDisc", Transform(uid).Coordinates);
            hands.PickupOrDrop(uid, disc);
        }
    }

    // one free seat at a time. the seat API holds the cap: Bad Bloods, hounds and open seats against the budget
    private void TickSummoning(Entity<BadBloodCampRuleComponent> rule, TimeSpan now)
    {
        var comp = rule.Comp;
        if (!HasLodge() || !HasLivingLeader())
        {
            // paused. a full interval runs again once the clan has both back
            comp.NextSummonAt = null;
            return;
        }

        comp.NextSummonAt ??= now + comp.SummonInterval;
        if (now < comp.NextSummonAt)
            return;

        comp.NextSummonAt = now + comp.SummonInterval;
        EntityManager.System<BadBloodCampSystem>().TryOpenFreeSeat();
    }

    #endregion

    #region Departure

    private void TickDeparture(Entity<BadBloodCampRuleComponent> rule, TimeSpan now)
    {
        var comp = rule.Comp;
        var end = EnsureComp<BadBloodCampEndComponent>(rule.Owner);
        var since = end.DepartureCountedAt ?? now;
        end.DepartureCountedAt = now;

        if (comp.DepartureRemaining is not { } left)
            return;

        // only time with a Lodge standing counts
        if (!HasLodge())
            return;

        if (left > TimeSpan.Zero)
        {
            left -= now - since;
            if (left < TimeSpan.Zero)
                left = TimeSpan.Zero;

            comp.DepartureRemaining = left;
        }

        if (left > TimeSpan.Zero)
        {
            AnnounceDepartureMarks(rule, left);
            return;
        }

        if (TryDecideRound(BadBloodRoundOutcome.BadBloodDeparture))
        {
            AnnounceToClan(Loc.GetString("cmu-bad-blood-ship-departed-clan"), PopupType.Large);
            AnnounceShipToMarines(Loc.GetString("cmu-bad-blood-ship-departed-marines"));
        }
    }

    private void AnnounceDepartureMarks(Entity<BadBloodCampRuleComponent> rule, TimeSpan left)
    {
        var comp = rule.Comp;
        int? announce = null;
        foreach (var mark in DepartureMarks)
        {
            var at = TimeSpan.FromMinutes(mark);

            // a timer shorter than the mark never had that much left to announce
            if (left > at || comp.DepartureTime <= at || !comp.DepartureAnnounced.Add(mark))
                continue;

            // several marks passed at once: only the last one is worth saying
            announce = mark;
        }

        if (announce is not { } minutes)
            return;

        AnnounceToClan(Loc.GetString("cmu-bad-blood-ship-departure-clan", ("minutes", minutes)));
        AnnounceShipToMarines(Loc.GetString("cmu-bad-blood-ship-departure-marines", ("minutes", minutes)));
    }

    #endregion

    private static int MinutesLeft(TimeSpan time)
    {
        return Math.Max(1, (int) Math.Ceiling(time.TotalMinutes));
    }

    private void AnnounceShipToMarines(string message)
    {
        EntityManager.System<SharedMarineAnnounceSystem>().AnnounceToMarines(message);
    }
}
