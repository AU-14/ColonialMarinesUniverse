using Content.Server.Administration.Managers;
using Content.Server.Chat.Managers;
using Content.Server.Ghost.Roles.Events;
using Content.Server.Players.JobWhitelist;
using Content.Server.Players.PlayTimeTracking;
using Content.Server._RMC14.Vendors;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Dialog;
using Content.Shared._RMC14.Vendors;
using Content.Shared.Administration.Logs;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Station;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._CMU14.Yautja;

public enum YautjaYoungbloodRejection
{
    WhitelistOrBan,
    MaximumYoungbloodTime,
    MinimumYoungbloodTime,
    SquadAndXenoTime,
}

public readonly record struct YautjaYoungbloodEligibility(bool Allowed, YautjaYoungbloodRejection? Reason);

public sealed partial class YautjaYoungbloodSystem : EntitySystem
{
    private static readonly ProtoId<JobPrototype> AdultJob = "CMUYautjaHunter";
    private static readonly ProtoId<JobPrototype> YoungbloodJob = "CMUYautjaYoungblood";

    private static readonly string[] SquadTrackers =
    {
        "CMJobRifleman",
        "CMJobHospitalCorpsman",
        "CMJobCombatTech",
        "CMJobFireteamLeader",
        "CMJobWeaponsSpecialist",
        "CMJobSmartGunOperator",
        "CMJobSquadLeader",
    };

    private static readonly string[] XenoTrackers =
    {
        "CMJobSelectableXeno",
        "CMJobXenoBoiler",
        "CMJobXenoBurrower",
        "CMJobXenoCarrier",
        "CMJobXenoCrusher",
        "CMJobXenoDefender",
        "CMJobXenoDrone",
        "CMJobXenoHivelord",
        "RMCJobXenoKing",
        "CMJobXenoLarva",
        "CMJobXenoLesserDrone",
        "CMJobXenoLurker",
        "CMJobXenoParasite",
        "CMJobXenoPraetorian",
        "CMJobXenoQueen",
        "CMJobXenoRavager",
        "CMJobXenoRunner",
        "CMJobXenoSentinel",
        "CMJobXenoSpitter",
        "CMJobXenoWarrior",
    };

    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private IBanManager _banManager = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private JobWhitelistManager _jobWhitelist = default!;
    [Dependency] private DialogSystem _dialog = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private PlayTimeTrackingManager _playtime = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private CMAutomatedVendorSystem _vendors = default!;
    [Dependency] private SharedStationSpawningSystem _stationSpawning = default!;
    [Dependency] private YautjaMarkSystem _marks = default!;
    [Dependency] private YautjaPowerSystem _power = default!;
    [Dependency] private YautjaPredatorRoundSystem _predatorRound = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<YautjaYoungbloodGhostRoleComponent, GhostRoleRequestAttemptEvent>(OnGhostRoleRequestAttempt);
        SubscribeLocalEvent<YautjaYoungbloodGhostRoleComponent, MindAddedMessage>(OnMindAdded);
        SubscribeLocalEvent<YautjaYoungbloodComponent, EntityTerminatingEvent>(OnYoungbloodTerminating);
        SubscribeLocalEvent<YautjaComponent, EntityTerminatingEvent>(OnYautjaTerminating);
        SubscribeLocalEvent<YautjaBracerComponent, YautjaYoungbloodExecutionTargetSelectedEvent>(OnExecutionTargetSelected);
        SubscribeLocalEvent<YautjaBracerComponent, YautjaYoungbloodExecutionReasonEvent>(OnExecutionReason);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    public static YautjaYoungbloodEligibility CheckEligibility(
        YautjaHuntCallOption option,
        bool adultWhitelisted,
        bool jobBanned,
        TimeSpan youngbloodTime,
        TimeSpan squadTime,
        TimeSpan xenoTime)
    {
        if (adultWhitelisted || jobBanned)
            return new(false, YautjaYoungbloodRejection.WhitelistOrBan);

        if (youngbloodTime >= option.MaximumYoungbloodTime)
            return new(false, YautjaYoungbloodRejection.MaximumYoungbloodTime);

        if (squadTime >= option.RequiredSquadAndXenoTime &&
            xenoTime >= option.RequiredSquadAndXenoTime)
        {
            return new(true, null);
        }

        if (youngbloodTime < option.RejectionYoungbloodTime)
            return new(false, YautjaYoungbloodRejection.MinimumYoungbloodTime);

        return new(false, YautjaYoungbloodRejection.SquadAndXenoTime);
    }

    private void OnGhostRoleRequestAttempt(
        Entity<YautjaYoungbloodGhostRoleComponent> ent,
        ref GhostRoleRequestAttemptEvent args)
    {
        if (ent.Comp.BypassEligibility)
            return;

        if (!TryGetCallOption(ent.Comp.CallId, out var option) ||
            !_playtime.TryGetTrackerTimes(args.Player, out var playtimes))
        {
            args.Cancelled = true;
            return;
        }

        var bans = _banManager.GetJobBans(args.Player.UserId);
        var eligibility = CheckEligibility(
            option,
            _jobWhitelist.IsWhitelisted(args.Player.UserId, AdultJob),
            bans == null || bans.Contains(YoungbloodJob),
            GetTrackerTime(playtimes, YoungbloodJob),
            SumTrackerTimes(playtimes, SquadTrackers),
            SumTrackerTimes(playtimes, XenoTrackers));

        args.Cancelled = !eligibility.Allowed;
    }

    private void OnMindAdded(Entity<YautjaYoungbloodGhostRoleComponent> ent, ref MindAddedMessage args)
    {
        if (args.Mind.Comp.UserId is not { } userId ||
            !_players.TryGetSessionById(userId, out var session))
        {
            return;
        }

        if (ent.Comp.SetupComplete)
            return;

        if (_prototype.TryIndex<JobPrototype>(YoungbloodJob, out var job))
            _stationSpawning.EquipStartingGear(ent.Owner, job.StartingGear, raiseEvent: false);

        ent.Comp.SetupComplete = true;

        _predatorRound.RegisterYoungblood(ent.Owner);

        var vendor = EnsureComp<CMVendorUserComponent>(ent);
        _vendors.SetPoints((ent.Owner, vendor), 50);
        SendServerMessage(session, Loc.GetString("cmu-yautja-youngblood-role-header"));
        SendServerMessage(session, Loc.GetString("cmu-yautja-youngblood-role-briefing"));

        Timer.Spawn(TimeSpan.FromSeconds(30), () =>
        {
            if (!Deleted(ent.Owner) && _players.TryGetSessionById(userId, out var currentSession))
                SendServerMessage(currentSession, Loc.GetString("cmu-yautja-youngblood-role-objectives"));
        });
    }

    public void HandleMarkAttempt(ref YautjaMarkAttemptEvent args)
    {
        if (args.Kind != YautjaMarkKind.Student && args.Kind != YautjaMarkKind.Blooded)
            return;

        if (!TryComp(args.Target, out YautjaYoungbloodComponent? youngblood))
        {
            if (args.Kind == YautjaMarkKind.Student)
                args.Cancelled = true;

            return;
        }

        if (!HasComp<YautjaComponent>(args.Hunter) ||
            HasComp<YautjaYoungbloodComponent>(args.Hunter))
        {
            args.Cancelled = true;
            return;
        }

        if (args.Kind == YautjaMarkKind.Blooded)
        {
            if (youngblood.Blooded)
            {
                _popup.PopupEntity(
                    Loc.GetString(
                        "cmu-yautja-youngblood-already-blooded",
                        ("target", args.Target),
                        ("mentor", youngblood.BloodedBy ?? args.Hunter),
                        ("reason", youngblood.BloodingReason)),
                    args.Hunter,
                    args.Hunter,
                    PopupType.MediumCaution);
                args.Cancelled = true;
            }

            return;
        }

        if (youngblood.Mentor is { } mentor && mentor != args.Hunter)
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-yautja-youngblood-already-claimed", ("target", args.Target), ("mentor", mentor)),
                args.Hunter,
                args.Hunter,
                PopupType.SmallCaution);
            args.Cancelled = true;
            return;
        }

        if (TryFindPupil(args.Hunter, args.Target, out _))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-youngblood-mentor-already-has-pupil"), args.Hunter, args.Hunter, PopupType.SmallCaution);
            args.Cancelled = true;
        }
    }

    public void HandleMarkApplied(ref YautjaMarkAppliedEvent args)
    {
        if (!TryComp(args.Target, out YautjaYoungbloodComponent? youngblood))
            return;

        switch (args.Kind)
        {
            case YautjaMarkKind.Student:
                youngblood.Mentor = args.Hunter;
                Dirty(args.Target, youngblood);
                NotifyMentorClaim(args.Hunter, args.Target);
                break;
            case YautjaMarkKind.Blooded:
                youngblood.Blooded = true;
                youngblood.BloodedBy = args.Hunter;
                youngblood.BloodingReason = args.Reason ?? string.Empty;
                EnsureComp<YautjaTechAuthorizedComponent>(args.Target);
                Dirty(args.Target, youngblood);
                NotifyBlooding(args.Hunter, args.Target, youngblood.BloodingReason);
                break;
        }
    }

    public void HandleMarkRemoveAttempt(ref YautjaMarkRemoveAttemptEvent args)
    {
        if (!TryComp(args.Target, out YautjaYoungbloodComponent? youngblood))
            return;

        if (args.Kind == YautjaMarkKind.Blooded)
        {
            args.Cancelled = true;
            return;
        }

        if (args.Kind != YautjaMarkKind.Student ||
            youngblood.Mentor == args.Hunter)
        {
            return;
        }

        _popup.PopupEntity(Loc.GetString("cmu-yautja-youngblood-not-your-pupil"), args.Hunter, args.Hunter, PopupType.SmallCaution);
        args.Cancelled = true;
    }

    public void HandleMarkRemoved(ref YautjaMarkRemovedEvent args)
    {
        if (!TryComp(args.Target, out YautjaYoungbloodComponent? youngblood))
            return;

        switch (args.Kind)
        {
            case YautjaMarkKind.Student when youngblood.Mentor == args.Hunter:
                youngblood.Mentor = null;
                Dirty(args.Target, youngblood);
                NotifyMentorRelease(args.Hunter, args.Target);
                break;
            case YautjaMarkKind.Blooded:
                if (args.CleanupOnly)
                {
                    if (youngblood.BloodedBy == args.Hunter)
                        youngblood.BloodedBy = null;

                    Dirty(args.Target, youngblood);
                    break;
                }

                youngblood.Blooded = false;
                youngblood.BloodedBy = null;
                RemCompDeferred<YautjaTechAuthorizedComponent>(args.Target);
                Dirty(args.Target, youngblood);
                break;
        }
    }

    public bool TryOpenRemoteExecution(Entity<YautjaBracerComponent> bracer, EntityUid user)
    {
        if (!CanUseRemoteExecution(bracer, user))
            return false;

        var options = new List<DialogOption>();
        var query = EntityQueryEnumerator<YautjaYoungbloodComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (Deleted(uid) || _mobState.IsDead(uid))
                continue;

            options.Add(new DialogOption(
                Name(uid),
                new YautjaYoungbloodExecutionTargetSelectedEvent(GetNetEntity(user), GetNetEntity(uid))));
        }

        if (options.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-youngblood-execute-none"), user, user, PopupType.SmallCaution);
            return false;
        }

        options.Sort((a, b) => string.Compare(a.Text, b.Text, StringComparison.OrdinalIgnoreCase));
        _dialog.OpenOptions(
            bracer.Owner,
            user,
            Loc.GetString("cmu-yautja-youngblood-execute-title"),
            options,
            Loc.GetString("cmu-yautja-youngblood-execute-message"));
        return true;
    }

    public bool TryExecuteYoungblood(EntityUid user, EntityUid target, string reason)
    {
        reason = reason.Trim();
        if (reason.Length == 0)
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-youngblood-execute-reason-required"), user, user, PopupType.SmallCaution);
            return false;
        }

        if (!HasComp<YautjaComponent>(user) ||
            HasComp<YautjaYoungbloodComponent>(user) ||
            !HasComp<YautjaYoungbloodComponent>(target) ||
            _mobState.IsDead(target))
        {
            return false;
        }

        _mobState.ChangeMobState(target, Content.Shared.Mobs.MobState.Dead);
        BroadcastToYautja(Loc.GetString(
            "cmu-yautja-youngblood-execute-broadcast",
            ("hunter", Name(user)),
            ("target", Name(target)),
            ("reason", reason)));
        _adminLog.Add(LogType.Action, LogImpact.High,
            $"{ToPrettyString(user):hunter} remotely executed Youngblood {ToPrettyString(target):target} for '{reason}'");
        return true;
    }

    private void OnYoungbloodTerminating(Entity<YautjaYoungbloodComponent> ent, ref EntityTerminatingEvent args)
    {
        ent.Comp.Mentor = null;
    }

    private void OnYautjaTerminating(Entity<YautjaComponent> ent, ref EntityTerminatingEvent args)
    {
        ClearMentorLinks(ent.Owner);
        ClearBloodedLinks(ent.Owner);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        var links = new List<(EntityUid Pupil, EntityUid Mentor)>();
        var query = EntityQueryEnumerator<YautjaYoungbloodComponent>();
        while (query.MoveNext(out var uid, out var youngblood))
        {
            if (youngblood.Mentor is { } mentor)
                links.Add((uid, mentor));
        }

        foreach (var (pupil, mentor) in links)
        {
            if (!_marks.TryClearMark(pupil, YautjaMarkKind.Student, mentor) &&
                TryComp(pupil, out YautjaYoungbloodComponent? youngblood))
            {
                youngblood.Mentor = null;
                Dirty(pupil, youngblood);
            }
        }
    }

    private void OnExecutionTargetSelected(Entity<YautjaBracerComponent> bracer, ref YautjaYoungbloodExecutionTargetSelectedEvent args)
    {
        if (!TryGetEntity(args.User, out var user) ||
            !TryGetEntity(args.Target, out var target) ||
            !CanUseRemoteExecution(bracer, user.Value) ||
            !HasComp<YautjaYoungbloodComponent>(target.Value) ||
            _mobState.IsDead(target.Value))
        {
            return;
        }

        _dialog.OpenInput(
            bracer.Owner,
            user.Value,
            Loc.GetString("cmu-yautja-youngblood-execute-reason-prompt", ("target", Name(target.Value))),
            new YautjaYoungbloodExecutionReasonEvent(GetNetEntity(user.Value), GetNetEntity(target.Value)),
            characterLimit: 200,
            minCharacterLimit: 1,
            smartCheck: true);
    }

    private void OnExecutionReason(Entity<YautjaBracerComponent> bracer, ref YautjaYoungbloodExecutionReasonEvent args)
    {
        if (!TryGetEntity(args.User, out var user) ||
            !TryGetEntity(args.Target, out var target) ||
            !CanUseRemoteExecution(bracer, user.Value))
        {
            return;
        }

        TryExecuteYoungblood(user.Value, target.Value, args.Message);
    }

    private bool CanUseRemoteExecution(Entity<YautjaBracerComponent> bracer, EntityUid user)
    {
        return bracer.Comp.User == user &&
               HasComp<YautjaComponent>(user) &&
               !HasComp<YautjaYoungbloodComponent>(user) &&
               _power.TryGetWornBracer(user, out var worn) &&
               worn.Owner == bracer.Owner;
    }

    private bool TryFindPupil(EntityUid mentor, EntityUid allowedPupil, out Entity<YautjaYoungbloodComponent> pupil)
    {
        var query = EntityQueryEnumerator<YautjaYoungbloodComponent>();
        while (query.MoveNext(out var uid, out var youngblood))
        {
            if (uid == allowedPupil || Deleted(uid) || youngblood.Mentor != mentor)
                continue;

            pupil = (uid, youngblood);
            return true;
        }

        pupil = default;
        return false;
    }

    private void ClearMentorLinks(EntityUid mentor)
    {
        var query = EntityQueryEnumerator<YautjaYoungbloodComponent>();
        while (query.MoveNext(out var uid, out var youngblood))
        {
            if (youngblood.Mentor != mentor)
                continue;

            if (!_marks.TryClearMark(uid, YautjaMarkKind.Student, mentor))
            {
                youngblood.Mentor = null;
                Dirty(uid, youngblood);
            }
        }
    }

    private void ClearBloodedLinks(EntityUid mentor)
    {
        var query = EntityQueryEnumerator<YautjaYoungbloodComponent>();
        while (query.MoveNext(out var uid, out var youngblood))
        {
            if (youngblood.BloodedBy != mentor)
                continue;

            youngblood.BloodedBy = null;
            Dirty(uid, youngblood);
        }
    }

    private void NotifyMentorClaim(EntityUid mentor, EntityUid pupil)
    {
        _popup.PopupEntity(Loc.GetString("cmu-yautja-youngblood-mentor-claimed", ("target", pupil)), mentor, mentor);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-youngblood-pupil-claimed", ("mentor", mentor)), pupil, pupil, PopupType.MediumCaution);
        BroadcastToYautja(Loc.GetString("cmu-yautja-youngblood-mentor-broadcast", ("mentor", mentor), ("target", pupil)));
        _adminLog.Add(LogType.Action, LogImpact.High,
            $"{ToPrettyString(mentor):mentor} claimed Youngblood pupil {ToPrettyString(pupil):pupil}");
    }

    private void NotifyMentorRelease(EntityUid mentor, EntityUid pupil)
    {
        _popup.PopupEntity(Loc.GetString("cmu-yautja-youngblood-mentor-released", ("target", pupil)), mentor, mentor);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-youngblood-pupil-released"), pupil, pupil, PopupType.SmallCaution);
        BroadcastToYautja(Loc.GetString("cmu-yautja-youngblood-release-broadcast", ("mentor", mentor), ("target", pupil)));
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(mentor):mentor} released Youngblood pupil {ToPrettyString(pupil):pupil}");
    }

    private void NotifyBlooding(EntityUid mentor, EntityUid pupil, string reason)
    {
        _popup.PopupEntity(Loc.GetString("cmu-yautja-youngblood-blooded-mentor", ("target", pupil)), mentor, mentor);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-youngblood-blooded-pupil"), pupil, pupil, PopupType.Medium);
        BroadcastToYautja(Loc.GetString("cmu-yautja-youngblood-blooded-broadcast", ("mentor", mentor), ("target", pupil), ("reason", reason)));
        _adminLog.Add(LogType.Action, LogImpact.High,
            $"{ToPrettyString(mentor):mentor} has blooded {ToPrettyString(pupil):pupil} for '{reason}'");
    }

    private void BroadcastToYautja(string message)
    {
        var query = EntityQueryEnumerator<YautjaComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (_players.TryGetSessionByEntity(uid, out var session))
                SendServerMessage(session, message);
        }
    }

    private void SendServerMessage(ICommonSession session, string message)
    {
        var wrapped = Loc.GetString("chat-manager-server-wrap-message", ("message", message));
        _chat.ChatMessageToOne(ChatChannel.Server, message, wrapped, default, false, session.Channel);
    }

    private bool TryGetCallOption(string callId, out YautjaHuntCallOption option)
    {
        var query = EntityQueryEnumerator<YautjaHuntConsoleComponent>();
        while (query.MoveNext(out _, out var console))
        {
            foreach (var candidate in console.BloodingCallOptions)
            {
                if (!string.Equals(candidate.Id, callId, StringComparison.OrdinalIgnoreCase))
                    continue;

                option = candidate;
                return true;
            }
        }

        option = default!;
        return false;
    }

    private static TimeSpan SumTrackerTimes(IReadOnlyDictionary<string, TimeSpan> playtimes, IEnumerable<string> trackers)
    {
        var result = TimeSpan.Zero;
        foreach (var tracker in trackers)
        {
            result += GetTrackerTime(playtimes, tracker);
        }

        return result;
    }

    private static TimeSpan GetTrackerTime(IReadOnlyDictionary<string, TimeSpan> playtimes, string tracker)
    {
        return playtimes.GetValueOrDefault(tracker);
    }
}
