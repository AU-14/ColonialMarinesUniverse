using System.Diagnostics.CodeAnalysis;
using Content.Server.Administration.Logs;
using Content.Server.CMU14.ShipWeapons;
using Content.Server.GameTicking;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Announce;
using Content.Shared._RMC14.Overwatch;
using Content.Shared.CMU14.ShipWeapons;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.DoAfter;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Roles.Jobs;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Yautja.BadBlood;

/// <summary>
/// ARCH-505: the Vessel Strike. After the uplink, a GOVFOR overwatch console lists "Vessel Strike" on its fire-mission
/// picker. Command (<see cref="BadBloodVesselStrikeComponent.AuthorizedJobs"/>) fires it by spending a ready orbital
/// battery on its ship; the strike then counts down <see cref="BadBloodVesselStrikeComponent.Duration"/> in any clan
/// ship state, with no power dependency. It pauses while the lock site is not working, a sabotage of the lock site adds
/// a one-time penalty, and marines resume by repairing the site or re-routing the lock. When it runs out the rule
/// decides <see cref="BadBloodRoundOutcome.MarinesVesselDestroyed"/>. The shipside counter cancels a running strike
/// through <see cref="TryCancelVesselStrike"/>. docs/GraphAudit-BadBlood-Objectives.md §5.3, §5.3a.
/// </summary>
public sealed class BadBloodVesselStrikeSystem : EntitySystem
{
    public static readonly ProtoId<CMUShipWeaponPrototype> VesselStrikeWeapon = "CMUVesselStrike";
    public const string GovforGroup = "GOVFOR";
    private const string GovforFaction = "govfor";

    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private BadBloodSystem _badBlood = default!;
    [Dependency] private BadBloodObjectiveChainSystem _chain = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private CMUShipWeaponGunSystem _guns = default!;
    [Dependency] private SharedJobSystem _jobs = default!;
    [Dependency] private SharedMarineAnnounceSystem _marineAnnounce = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private BadBloodCampRuleSystem _rule = default!;
    [Dependency] private BadBloodSabotageSystem _sabotage = default!;
    [Dependency] private CMUShipWeaponsSystem _shipWeapons = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private GameTicker _gameTicker = default!;

    private static readonly TimeSpan OfferCheckEvery = TimeSpan.FromSeconds(1);
    private TimeSpan _nextOfferCheck;
    private bool _lastLockedOut;
    private int _lastReadyGuns;

    public override void Initialize()
    {
        SubscribeLocalEvent<OverwatchConsoleComponent, CMUShipWeaponsCollectExtrasEvent>(OnCollectExtras);
        SubscribeLocalEvent<OverwatchConsoleComponent, CMUShipWeaponLaunchInterceptEvent>(OnLaunchIntercept);

        SubscribeLocalEvent<BadBloodUplinkSiteComponent, GetVerbsEvent<AlternativeVerb>>(OnSiteVerbs);
        SubscribeLocalEvent<BadBloodUplinkSiteComponent, BadBloodRerouteLockDoAfterEvent>(OnRerouteDoAfter);

        SubscribeLocalEvent<BadBloodSiteSabotagedEvent>(OnSiteSabotaged);
        SubscribeLocalEvent<BadBloodObjectiveStageEvent>(OnStage);

        SubscribeLocalEvent<BadBloodVesselStrikeConsoleComponent, EntityTerminatingEvent>(OnConsoleTerminating);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _nextOfferCheck = TimeSpan.Zero;
        _lastLockedOut = false;
        _lastReadyGuns = 0;
    }

    // the shipside counter needs a console to sabotage: when the firing console goes, the marker moves to another
    // GOVFOR overwatch console on the same ship
    private void OnConsoleTerminating(Entity<BadBloodVesselStrikeConsoleComponent> ent, ref EntityTerminatingEvent args)
    {
        if (!TryGetStrike(out var strike) || !strike.Comp.Running || strike.Comp.Console != ent.Owner)
            return;

        var comp = strike.Comp;
        var consoles = EntityQueryEnumerator<OverwatchConsoleComponent>();
        while (consoles.MoveNext(out var uid, out _))
        {
            if (uid == ent.Owner ||
                TerminatingOrDeleted(uid) ||
                EntityManager.IsQueuedForDeletion(uid) ||
                !IsGovforConsole(uid) ||
                !_shipWeapons.TryGetShip(uid, out var ship) ||
                ship != comp.Ship)
            {
                continue;
            }

            comp.Console = uid;
            EnsureComp<BadBloodVesselStrikeConsoleComponent>(uid);
            _adminLog.Add(LogType.Action, LogImpact.Medium,
                $"BadBlood obj: the Vessel Strike console {ToPrettyString(ent.Owner):console} was deleted; the strike now runs from {ToPrettyString(uid):console}");
            return;
        }

        _adminLog.Add(LogType.Action, LogImpact.High,
            $"BadBlood obj: the Vessel Strike console {ToPrettyString(ent.Owner):console} was deleted and no other GOVFOR overwatch console is on its ship; the strike can no longer be cancelled shipside");
    }

    #region Queries (used by the shipside counter and the end rule)

    /// <summary>
    /// The Vessel Strike state of the running Bad Blood rule. False on rounds without one.
    /// </summary>
    public bool TryGetStrike(out Entity<BadBloodVesselStrikeComponent> strike)
    {
        if (_rule.TryGetRule(out var rule) && TryComp(rule.Owner, out BadBloodVesselStrikeComponent? comp))
        {
            strike = (rule.Owner, comp);
            return true;
        }

        strike = default;
        return false;
    }

    public bool IsVesselStrikeRunning()
    {
        return TryGetStrike(out var strike) && strike.Comp.Running;
    }

    /// <summary>
    /// The strike ran out and destroyed the clan vessel.
    /// </summary>
    public bool IsStrikeComplete()
    {
        return TryGetStrike(out var strike) && strike.Comp.Completed;
    }

    /// <summary>
    /// The overwatch console the running strike was fired from. False while no strike runs.
    /// </summary>
    public bool TryGetStrikeConsole(out EntityUid console)
    {
        console = default;
        if (!TryGetStrike(out var strike) ||
            !strike.Comp.Running ||
            strike.Comp.Console is not { } fired ||
            TerminatingOrDeleted(fired))
        {
            return false;
        }

        console = fired;
        return true;
    }

    /// <summary>
    /// The ship (z-network owner or grid) the running strike was fired from. Null while no strike runs.
    /// </summary>
    public EntityUid? GetStrikeShip()
    {
        return TryGetStrike(out var strike) && strike.Comp.Running ? strike.Comp.Ship : null;
    }

    public bool IsGovforConsole(EntityUid console)
    {
        return TryComp(console, out OverwatchConsoleComponent? overwatch) &&
               string.Equals(overwatch.Group, GovforGroup, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The mind of <paramref name="user"/> holds one of <see cref="BadBloodVesselStrikeComponent.AuthorizedJobs"/>.
    /// </summary>
    public bool IsAuthorized(EntityUid user)
    {
        if (!TryGetStrike(out var strike) ||
            !_mind.TryGetMind(user, out var mindId, out _) ||
            !_jobs.MindTryGetJobId(mindId, out var job) ||
            job is not { } jobId)
        {
            return false;
        }

        return strike.Comp.AuthorizedJobs.Contains(jobId.Id);
    }

    #endregion

    #region Firing

    private void OnCollectExtras(Entity<OverwatchConsoleComponent> ent, ref CMUShipWeaponsCollectExtrasEvent args)
    {
        if (!IsGovforConsole(ent.Owner) ||
            !TryGetStrike(out var strike) ||
            strike.Comp.Completed ||
            !_chain.IsUplinked())
        {
            return;
        }

        // Remaining 0 greys out the fire button: running, locked out, or no ready battery
        var fireable = CanFire(strike, ent.Owner, out _, out _, out _);
        args.Extras.Add(new CMUShipWeaponStatus(VesselStrikeWeapon, fireable ? 1 : 0, 1));
    }

    private void OnLaunchIntercept(Entity<OverwatchConsoleComponent> ent, ref CMUShipWeaponLaunchInterceptEvent args)
    {
        if (args.Handled || args.Weapon != VesselStrikeWeapon)
            return;

        args.Handled = true;
        if (!TryStartStrike(args.User, ent.Owner, out var reason))
            _popup.PopupCursor(reason, args.User, PopupType.LargeCaution);
    }

    // everything but the user's job
    private bool CanFire(
        Entity<BadBloodVesselStrikeComponent> strike,
        EntityUid console,
        out EntityUid ship,
        out Entity<CMUShipWeaponGunComponent> gun,
        [NotNullWhen(false)] out string? reason)
    {
        ship = default;
        gun = default;
        reason = null;
        var comp = strike.Comp;

        if (!IsGovforConsole(console))
        {
            reason = Loc.GetString("cmu-bad-blood-strike-wrong-console");
            return false;
        }

        if (!_chain.IsUplinked())
        {
            reason = Loc.GetString("cmu-bad-blood-strike-no-uplink");
            return false;
        }

        if (comp.Completed)
        {
            reason = Loc.GetString("cmu-bad-blood-strike-completed");
            return false;
        }

        if (comp.Running)
        {
            reason = Loc.GetString("cmu-bad-blood-strike-running");
            return false;
        }

        var now = _timing.CurTime;
        if (now < comp.LockoutUntil)
        {
            reason = Loc.GetString("cmu-bad-blood-strike-lockout",
                ("seconds", (int) Math.Ceiling((comp.LockoutUntil - now).TotalSeconds)));
            return false;
        }

        if (!_shipWeapons.TryGetShip(console, out ship) || !_shipWeapons.CMUTryGetReadyGun(ship, out gun))
        {
            reason = Loc.GetString("cmu-bad-blood-strike-no-battery");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Fires the Vessel Strike from <paramref name="console"/> on behalf of <paramref name="user"/>: the launch of the
    /// "Vessel Strike" fire mission, and tests. Spends a ready orbital battery on the console's ship.
    /// </summary>
    public bool TryStartStrike(EntityUid user, EntityUid console, [NotNullWhen(false)] out string? reason)
    {
        if (!TryGetStrike(out var strike))
        {
            reason = Loc.GetString("cmu-bad-blood-strike-no-rule");
            return false;
        }

        if (!_chain.IsUplinked())
        {
            reason = Loc.GetString("cmu-bad-blood-strike-no-uplink");
            return false;
        }

        if (!IsAuthorized(user))
        {
            reason = Loc.GetString("cmu-bad-blood-strike-unauthorized");
            return false;
        }

        if (!CanFire(strike, console, out var ship, out var gun, out reason))
            return false;

        var comp = strike.Comp;
        var now = _timing.CurTime;
        _guns.Spend(gun);

        comp.Running = true;
        comp.Paused = false;
        comp.Completed = false;
        comp.PenaltyApplied = false;
        comp.Remaining = comp.Duration;
        comp.PausedTotal = TimeSpan.Zero;
        comp.RunTotal = TimeSpan.Zero;
        comp.Console = console;
        comp.Ship = ship;
        comp.NextTick = now + comp.Tick;
        EnsureComp<BadBloodVesselStrikeConsoleComponent>(console);

        var minutes = Minutes(comp.Remaining);
        _marineAnnounce.AnnounceARES(null, Loc.GetString("cmu-bad-blood-strike-started-marines", ("minutes", minutes)),
            faction: GovforFaction);
        _rule.AnnounceToClan(Loc.GetString("cmu-bad-blood-strike-started-clan", ("minutes", minutes)),
            PopupType.LargeCaution);
        _popup.PopupCursor(Loc.GetString("cmu-bad-blood-strike-fired"), user, PopupType.Large);

        _adminLog.Add(LogType.Action, LogImpact.High,
            $"BadBlood obj: {ToPrettyString(user):player} fired the Vessel Strike from {ToPrettyString(console):console} on ship {ToPrettyString(ship):ship}, spending battery {ToPrettyString(gun.Owner):battery}; {(int) comp.Remaining.TotalSeconds}s to impact");

        RaiseStage(BadBloodObjectiveStage.StrikeStarted, $"fired by {Name(user)} from {Name(console)}", console);
        _shipWeapons.CMURefreshOverwatchConsoles();

        // the lock can already be down when it is fired
        if (!IsLockWorking())
            Pause(strike, "the lock site is down");

        return true;
    }

    public bool TryStartStrike(EntityUid user, EntityUid console)
    {
        return TryStartStrike(user, console, out _);
    }

    /// <summary>
    /// The shipside counter: stops the running strike and locks the strike out for
    /// <see cref="BadBloodVesselStrikeComponent.CancelLockout"/>. Gate, signature and uplink are kept, so command can
    /// fire again with a new battery and no new uplink.
    /// </summary>
    public bool TryCancelVesselStrike(string reason)
    {
        if (!TryGetStrike(out var strike) || !strike.Comp.Running)
            return false;

        var comp = strike.Comp;
        var now = _timing.CurTime;
        var left = comp.Remaining;
        comp.Running = false;
        comp.Paused = false;
        comp.PenaltyApplied = false;
        comp.Remaining = TimeSpan.Zero;
        comp.LockoutUntil = now + comp.CancelLockout;
        var console = comp.Console;
        ClearConsoleMarker(comp);

        var lockout = Minutes(comp.CancelLockout);
        _marineAnnounce.AnnounceARES(null, Loc.GetString("cmu-bad-blood-strike-cancelled-marines", ("minutes", lockout)),
            faction: GovforFaction);
        _rule.AnnounceToClan(Loc.GetString("cmu-bad-blood-strike-cancelled-clan"), PopupType.Large);

        _adminLog.Add(LogType.Action, LogImpact.High,
            $"BadBlood obj: the Vessel Strike was cancelled ({reason}) with {(int) left.TotalSeconds}s left; locked out for {(int) comp.CancelLockout.TotalSeconds}s");

        RaiseStage(BadBloodObjectiveStage.StrikeCancelled, reason, console);
        _shipWeapons.CMURefreshOverwatchConsoles();
        return true;
    }

    #endregion

    #region Countdown

    public override void Update(float frameTime)
    {
        // a round that already ended announces nothing more. Not gated on InRound: rules started in the lobby
        // (admins, tests) keep running
        if (_gameTicker.RunLevel == GameRunLevel.PostRound || !TryGetStrike(out var strike))
            return;

        var comp = strike.Comp;
        var now = _timing.CurTime;
        if (now >= _nextOfferCheck)
        {
            _nextOfferCheck = now + OfferCheckEvery;
            CheckOffer(comp, now);
        }

        if (!comp.Running || now < comp.NextTick)
            return;

        comp.NextTick = now + comp.Tick;
        Tick(strike, comp.Tick);
    }

    private void Tick(Entity<BadBloodVesselStrikeComponent> strike, TimeSpan step)
    {
        var comp = strike.Comp;
        comp.RunTotal += step;

        if (!IsLockWorking())
        {
            if (!comp.Paused)
                Pause(strike, "the lock site is down");

            comp.PausedTotal += step;
            return;
        }

        if (comp.Paused)
            Resume(strike, "the lock site works again");

        comp.Remaining -= step;
        if (comp.Remaining <= TimeSpan.Zero)
            Complete(strike);
    }

    // the fire button greys out on lockout or a spent battery; re-enable it the moment either recovers, without
    // waiting for someone to reopen the console. Refreshes once per change
    private void CheckOffer(BadBloodVesselStrikeComponent comp, TimeSpan now)
    {
        var lockedOut = now < comp.LockoutUntil;
        var ready = 0;
        var guns = EntityQueryEnumerator<CMUShipWeaponGunComponent>();
        while (guns.MoveNext(out var gun))
        {
            if (gun.Ready)
                ready++;
        }

        if (lockedOut == _lastLockedOut && ready == _lastReadyGuns)
            return;

        var recovered = _lastLockedOut && !lockedOut || ready > _lastReadyGuns;
        _lastLockedOut = lockedOut;
        _lastReadyGuns = ready;
        if (recovered && !comp.Running && !comp.Completed && _chain.IsUplinked())
            _shipWeapons.CMURefreshOverwatchConsoles();
    }

    private bool IsLockWorking()
    {
        return _chain.GetLockSite() is { } site &&
               !TerminatingOrDeleted(site) &&
               !EntityManager.IsQueuedForDeletion(site) &&
               _chain.IsSiteWorking(site);
    }

    private void Pause(Entity<BadBloodVesselStrikeComponent> strike, string why)
    {
        var comp = strike.Comp;
        if (comp.Paused)
            return;

        comp.Paused = true;
        var minutes = Minutes(comp.Remaining);
        _marineAnnounce.AnnounceARES(null, Loc.GetString("cmu-bad-blood-strike-paused-marines", ("minutes", minutes)),
            faction: GovforFaction);
        _rule.AnnounceToClan(Loc.GetString("cmu-bad-blood-strike-paused-clan"));

        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"BadBlood obj: the Vessel Strike paused ({why}) with {(int) comp.Remaining.TotalSeconds}s left");
        RaiseStage(BadBloodObjectiveStage.StrikePaused, why, _chain.GetLockSite());
    }

    private void Resume(Entity<BadBloodVesselStrikeComponent> strike, string why)
    {
        var comp = strike.Comp;
        if (!comp.Paused)
            return;

        comp.Paused = false;
        comp.PenaltyApplied = false;
        var minutes = Minutes(comp.Remaining);
        _marineAnnounce.AnnounceARES(null, Loc.GetString("cmu-bad-blood-strike-resumed-marines", ("minutes", minutes)),
            faction: GovforFaction);
        _rule.AnnounceToClan(Loc.GetString("cmu-bad-blood-strike-resumed-clan", ("minutes", minutes)),
            PopupType.LargeCaution);

        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"BadBlood obj: the Vessel Strike resumed ({why}) with {(int) comp.Remaining.TotalSeconds}s left");
        RaiseStage(BadBloodObjectiveStage.StrikeResumed, why, _chain.GetLockSite());
    }

    private void Complete(Entity<BadBloodVesselStrikeComponent> strike)
    {
        var comp = strike.Comp;
        comp.Completed = true;
        comp.Running = false;
        comp.Paused = false;
        comp.Remaining = TimeSpan.Zero;
        var console = comp.Console;
        ClearConsoleMarker(comp);

        _marineAnnounce.AnnounceARES(null, Loc.GetString("cmu-bad-blood-strike-completed-marines"),
            faction: GovforFaction);
        _rule.AnnounceToClan(Loc.GetString("cmu-bad-blood-strike-completed-clan"), PopupType.LargeCaution);

        _adminLog.Add(LogType.Action, LogImpact.Extreme,
            $"BadBlood obj: the Vessel Strike destroyed the clan vessel after {(int) comp.RunTotal.TotalSeconds}s, {(int) comp.PausedTotal.TotalSeconds}s of it paused");

        RaiseStage(BadBloodObjectiveStage.StrikeCompleted,
            $"ran {(int) comp.RunTotal.TotalSeconds}s, paused {(int) comp.PausedTotal.TotalSeconds}s", console);
        _shipWeapons.CMURefreshOverwatchConsoles();

        _rule.TryDecideRound(BadBloodRoundOutcome.MarinesVesselDestroyed);
    }

    // a sabotage of the lock site pauses the strike at once and costs the marines the penalty, once per pause
    private void OnSiteSabotaged(ref BadBloodSiteSabotagedEvent ev)
    {
        if (!TryGetStrike(out var strike) || !strike.Comp.Running || _chain.GetLockSite() != ev.Site)
            return;

        var comp = strike.Comp;
        if (!comp.PenaltyApplied)
        {
            var before = comp.Remaining;
            var after = comp.Remaining + comp.SabotagePenalty;
            comp.Remaining = after > comp.Duration ? comp.Duration : after;
            comp.PenaltyApplied = true;
            _adminLog.Add(LogType.Action, LogImpact.Medium,
                $"BadBlood obj: {ToPrettyString(ev.User):player} sabotaged the Vessel Strike lock site {ToPrettyString(ev.Site):site}; {(int) before.TotalSeconds}s -> {(int) comp.Remaining.TotalSeconds}s left");
        }

        Pause(strike, $"{Name(ev.User)} sabotaged the lock site");
    }

    private void OnStage(ref BadBloodObjectiveStageEvent ev)
    {
        // the uplink puts the Vessel Strike on the GOVFOR fire-mission pickers
        if (ev.Stage == BadBloodObjectiveStage.Uplink)
            _shipWeapons.CMURefreshOverwatchConsoles();
    }

    #endregion

    #region Re-route

    private void OnSiteVerbs(Entity<BadBloodUplinkSiteComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess ||
            !args.CanInteract ||
            !IsVesselStrikeRunning() ||
            _badBlood.IsBadBlood(args.User) ||
            !HasComp<MarineComponent>(args.User) ||
            _chain.GetLockSite() == ent.Owner ||
            IsLockWorking())
        {
            return;
        }

        var user = args.User;
        var site = ent.Owner;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("cmu-bad-blood-strike-reroute-verb"),
            Message = Loc.GetString("cmu-bad-blood-strike-reroute-verb-message"),
            Act = () => TryStartReroute(user, site),
        });
    }

    /// <summary>
    /// Whether <paramref name="user"/> may move the running strike's lock to <paramref name="site"/>.
    /// </summary>
    public bool CanReroute(EntityUid user, EntityUid site, [NotNullWhen(false)] out string? reason)
    {
        reason = null;
        if (!IsVesselStrikeRunning())
        {
            reason = Loc.GetString("cmu-bad-blood-strike-reroute-not-running");
            return false;
        }

        // re-routing is how marines resume after the lock site went down. While it works, moving it would only
        // hand out another sabotage immunity
        if (IsLockWorking())
        {
            reason = Loc.GetString("cmu-bad-blood-strike-reroute-lock-working");
            return false;
        }

        if (_badBlood.IsBadBlood(user) || !HasComp<MarineComponent>(user) || !_mobState.IsAlive(user))
        {
            reason = Loc.GetString("cmu-bad-blood-strike-reroute-not-marine");
            return false;
        }

        if (!HasComp<BadBloodUplinkSiteComponent>(site) || _chain.GetLockSite() == site)
        {
            reason = Loc.GetString("cmu-bad-blood-strike-reroute-same-site");
            return false;
        }

        if (!_chain.IsSiteWorking(site))
        {
            reason = Loc.GetString("cmu-bad-blood-strike-reroute-not-working");
            return false;
        }

        return true;
    }

    public bool TryStartReroute(EntityUid user, EntityUid site)
    {
        if (!CanReroute(user, site, out var reason) || !TryGetStrike(out var strike))
        {
            if (reason != null)
                _popup.PopupEntity(reason, user, user, PopupType.SmallCaution);

            return false;
        }

        var doAfter = new DoAfterArgs(EntityManager, user, strike.Comp.RerouteTime, new BadBloodRerouteLockDoAfterEvent(),
            site, target: site)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BlockDuplicate = true,
            CancelDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameTarget | DuplicateConditions.SameEvent,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return false;

        _popup.PopupEntity(Loc.GetString("cmu-bad-blood-strike-reroute-start"), user, user);
        return true;
    }

    private void OnRerouteDoAfter(Entity<BadBloodUplinkSiteComponent> ent, ref BadBloodRerouteLockDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;
        TryReroute(args.User, ent.Owner);
    }

    /// <summary>
    /// Moves the running strike's lock to <paramref name="site"/> right now, without the do-after: the end of the
    /// do-after, and tests. The new site gets the repair immunity and a paused strike resumes.
    /// </summary>
    public bool TryReroute(EntityUid user, EntityUid site)
    {
        if (!CanReroute(user, site, out var reason) || !TryGetStrike(out var strike))
        {
            if (reason != null)
                _popup.PopupEntity(reason, user, user, PopupType.SmallCaution);

            return false;
        }

        var old = _chain.GetLockSite();
        _chain.SetLockSite(site);
        _sabotage.GrantImmunity(site, strike.Comp.RepairImmunity);

        _marineAnnounce.AnnounceARES(null, Loc.GetString("cmu-bad-blood-strike-rerouted-marines"),
            faction: GovforFaction);
        _rule.AnnounceToClan(Loc.GetString("cmu-bad-blood-strike-rerouted-clan"), PopupType.LargeCaution);
        _popup.PopupEntity(Loc.GetString("cmu-bad-blood-strike-reroute-done"), user, user, PopupType.Medium);

        var from = old is { } previous ? ToPrettyString(previous).ToString() : "nowhere";
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"BadBlood obj: {ToPrettyString(user):player} re-routed the Vessel Strike lock from {from} to {ToPrettyString(site):site}");

        RaiseStage(BadBloodObjectiveStage.Rerouted, $"{Name(user)} re-routed the lock", site);
        Resume(strike, "the lock was re-routed");
        return true;
    }

    #endregion

    private void ClearConsoleMarker(BadBloodVesselStrikeComponent comp)
    {
        if (comp.Console is { } console && !TerminatingOrDeleted(console))
            RemComp<BadBloodVesselStrikeConsoleComponent>(console);
    }

    private void RaiseStage(BadBloodObjectiveStage stage, string detail, EntityUid? site)
    {
        var ev = new BadBloodObjectiveStageEvent(stage, detail, site);
        RaiseLocalEvent(ref ev);
    }

    private static int Minutes(TimeSpan time)
    {
        return Math.Max(1, (int) Math.Ceiling(time.TotalMinutes));
    }
}
