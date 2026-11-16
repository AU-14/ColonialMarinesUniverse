using System.Diagnostics.CodeAnalysis;
using Content.Server.Administration.Logs;
using Content.Shared._RMC14.Marines.Announce;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Verbs;

namespace Content.Server.CMU14.Yautja.BadBlood;

/// <summary>
/// FLOW-514: the shipside counter. Any living Bad Blood who reaches the console a running Vessel Strike was fired from
/// can work it for <see cref="BadBloodShipsideCounterComponent.SabotageDoAfter"/> to cancel the strike. GOVFOR gets an
/// ARES warning when the sabotage starts. The cancel itself, and its lockout, belong to
/// <c>BadBloodVesselStrikeSystem.TryCancelVesselStrike</c>; gate, signature and uplink are kept, so command can
/// re-fire after the lockout. docs/GraphAudit-BadBlood-Objectives.md §5.3 (COUNTER 2).
/// <para>
/// Verify-first (FLOW-514): verbs on an RMC overwatch console need no power. <c>RMCOverwatchConsole</c> has no power
/// receiver, <c>RequiresSkill</c> only gates UI opening, use-in-hand and ranged interaction, and no system cancels
/// <c>GettingInteractedWithAttemptEvent</c> for unpowered machines, so this verb works after a crash blackout.
/// </para>
/// </summary>
public sealed class BadBloodStrikeSabotageSystem : EntitySystem
{
    /// <summary>
    /// The reason passed to <c>BadBloodVesselStrikeSystem.TryCancelVesselStrike</c>.
    /// </summary>
    public const string CancelReason = "BadBloodConsoleSabotage";

    /// <summary>
    /// The faction warned through ARES.
    /// </summary>
    public const string MarineFaction = "govfor";

    // used when the rule entity has no BadBloodShipsideCounter (admin-started rules built without it)
    private static readonly TimeSpan DefaultSabotageDoAfter = TimeSpan.FromSeconds(30);

    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private BadBloodSystem _badBlood = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedMarineAnnounceSystem _marineAnnounce = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private BadBloodCampRuleSystem _rule = default!;
    [Dependency] private BadBloodVesselStrikeSystem _strike = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BadBloodVesselStrikeConsoleComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<YautjaBadBloodComponent, BadBloodStrikeSabotageDoAfterEvent>(OnSabotageDoAfter);
    }

    private void OnGetVerbs(Entity<BadBloodVesselStrikeConsoleComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !CanSabotage(args.User, ent.Owner, out _))
            return;

        var user = args.User;
        var console = ent.Owner;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("cmu-bad-blood-shipside-sabotage-verb"),
            Message = Loc.GetString("cmu-bad-blood-shipside-sabotage-verb-message"),
            Act = () => TryStartSabotage(user, console),
        });
    }

    /// <summary>
    /// Whether <paramref name="user"/> may sabotage <paramref name="console"/> now: a living Bad Blood at the console a
    /// running Vessel Strike was fired from. <paramref name="reason"/> is the locale id of the refusal.
    /// </summary>
    public bool CanSabotage(EntityUid user, EntityUid console, [NotNullWhen(false)] out string? reason)
    {
        reason = null;
        if (!_badBlood.IsBadBlood(user))
        {
            reason = "cmu-bad-blood-shipside-sabotage-not-clan";
            return false;
        }

        if (!_mobState.IsAlive(user))
        {
            reason = "cmu-bad-blood-shipside-sabotage-not-alive";
            return false;
        }

        if (!HasComp<BadBloodVesselStrikeConsoleComponent>(console) || TerminatingOrDeleted(console))
        {
            reason = "cmu-bad-blood-shipside-sabotage-no-strike";
            return false;
        }

        if (!_strike.IsVesselStrikeRunning())
        {
            reason = "cmu-bad-blood-shipside-sabotage-no-strike";
            return false;
        }

        // a stale marker on an older console does not count
        if (_strike.TryGetStrikeConsole(out var strikeConsole) && strikeConsole != console)
        {
            reason = "cmu-bad-blood-shipside-sabotage-no-strike";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Starts the sabotage do-after and warns GOVFOR. Tells <paramref name="user"/> why when it is refused.
    /// </summary>
    public bool TryStartSabotage(EntityUid user, EntityUid console)
    {
        if (!CanSabotage(user, console, out var reason))
        {
            _popup.PopupEntity(Loc.GetString(reason), user, user, PopupType.SmallCaution);
            return false;
        }

        var counter = GetCounter();
        var delay = counter?.SabotageDoAfter ?? DefaultSabotageDoAfter;
        var doAfter = new DoAfterArgs(EntityManager, user, delay, new BadBloodStrikeSabotageDoAfterEvent(), user,
            target: console)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BlockDuplicate = true,
            CancelDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameEvent,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return false;

        _popup.PopupEntity(Loc.GetString("cmu-bad-blood-shipside-sabotage-start"), user, user, PopupType.Medium);

        if (counter?.AnnounceOnStart ?? true)
        {
            _marineAnnounce.AnnounceARES(null, Loc.GetString("cmu-bad-blood-shipside-sabotage-ares-start"),
                SharedMarineAnnounceSystem.AresAnnouncementSound, MarineFaction);
        }

        _adminLog.Add(LogType.Action, LogImpact.High,
            $"BadBlood obj: {ToPrettyString(user):player} started sabotaging the Vessel Strike console {ToPrettyString(console):console}");
        return true;
    }

    /// <summary>
    /// Finishes a sabotage: checks again that the strike still runs from <paramref name="console"/> and that
    /// <paramref name="user"/> is still a living Bad Blood, then cancels the strike and tells both sides.
    /// </summary>
    public bool CompleteSabotage(EntityUid user, EntityUid console)
    {
        if (!CanSabotage(user, console, out var reason))
        {
            _popup.PopupEntity(Loc.GetString(reason), user, user, PopupType.SmallCaution);
            _adminLog.Add(LogType.Action, LogImpact.Medium,
                $"BadBlood obj: {ToPrettyString(user):player} finished sabotaging {ToPrettyString(console):console} but nothing was cancelled ({reason})");
            return false;
        }

        if (!_strike.TryCancelVesselStrike(CancelReason))
        {
            _popup.PopupEntity(Loc.GetString("cmu-bad-blood-shipside-sabotage-no-strike"), user, user,
                PopupType.SmallCaution);
            _adminLog.Add(LogType.Action, LogImpact.Medium,
                $"BadBlood obj: {ToPrettyString(user):player} finished sabotaging {ToPrettyString(console):console} but the strike refused to cancel");
            return false;
        }

        // TryCancelVesselStrike already announced the cancel to ARES and the clan
        _popup.PopupEntity(Loc.GetString("cmu-bad-blood-shipside-sabotage-done"), user, user, PopupType.Large);

        _adminLog.Add(LogType.Action, LogImpact.High,
            $"BadBlood obj: {ToPrettyString(user):player} sabotaged the Vessel Strike console {ToPrettyString(console):console} and cancelled the strike");
        return true;
    }

    private void OnSabotageDoAfter(Entity<YautjaBadBloodComponent> ent, ref BadBloodStrikeSabotageDoAfterEvent args)
    {
        if (args.Handled)
            return;

        var user = args.User;
        if (args.Cancelled)
        {
            _adminLog.Add(LogType.Action, LogImpact.Medium,
                $"BadBlood obj: {ToPrettyString(user):player} was interrupted sabotaging the Vessel Strike console {ToPrettyString(args.Target):console}");
            return;
        }

        args.Handled = true;
        if (args.Target is not { } console)
            return;

        CompleteSabotage(user, console);
    }

    private BadBloodShipsideCounterComponent? GetCounter()
    {
        return _rule.TryGetRule(out var rule) && TryComp(rule.Owner, out BadBloodShipsideCounterComponent? counter)
            ? counter
            : null;
    }
}
