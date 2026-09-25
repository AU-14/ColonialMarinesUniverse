using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.Station.Systems;
using System.Linq;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Ops.ForceOnForce;
using Content.Shared.GameTicking;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Configuration;
using Robust.Shared.Localization;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Server.Player;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Ops.ForceOnForce;

/// <summary>
/// Tracks which faction each player spawned as during Force on Force for the mid-round
/// balance count. The per-player faction lock is currently disabled: it let dead players
/// on the leading side respawn straight back into it, which defeated the gap balancer.
/// Round-start dealing lives in StationJobsSystem.AssignJobs.
/// </summary>
public sealed class ForceOnForceFactionSystem : EntitySystem
{
    private const double ConfirmValiditySeconds = 60;
    private static readonly ProtoId<JobPrototype> GovforRifleman = "AU14JobGOVFORSquadRifleman";
    private static readonly ProtoId<JobPrototype> OpforRifleman = "AU14JobOPFORSquadRifleman";

    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly StationJobsSystem _stationJobs = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IServerNetManager _netManager = default!;

    private readonly Dictionary<NetUserId, string> _factions = new();
    private readonly Dictionary<NetUserId, PendingBalanceJoin> _pendingJoins = new();
    private readonly HashSet<ProtoId<JobPrototype>> _govforJobs = new();
    private readonly HashSet<ProtoId<JobPrototype>> _opforJobs = new();
    private sealed record PendingBalanceJoin(EntityUid Station, ProtoId<JobPrototype> Job, TimeSpan ConfirmedAt);

    public override void Initialize()
    {
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnSpawnComplete);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        _netManager.RegisterNetMessage<FoFBalanceConfirmMessage>(OnBalanceConfirmed);

        foreach (var job in ProtoMan.EnumeratePrototypes<JobPrototype>())
        {
            if (job.ID.Contains("GOVFOR"))
                _govforJobs.Add(job.ID);
            else if (job.ID.Contains("OPFOR"))
                _opforJobs.Add(job.ID);
        }
    }

    private void OnSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (ev.JobId is not { } jobId)
            return;

        if (jobId.Contains("GOVFOR"))
            _factions[ev.Player.UserId] = "GOVFOR";
        else if (jobId.Contains("OPFOR"))
            _factions[ev.Player.UserId] = "OPFOR";
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _factions.Clear();
        _pendingJoins.Clear();
    }

    /// <summary>
    /// Decides the job and station for a mid-round Force on Force spawn. Returns false when
    /// the mode imposes nothing (not Force on Force, both sides within the gap, or a
    /// non-faction job was requested) and the normal preference flow should run untouched.
    /// </summary>
    public bool TryDecideSpawn(
        ICommonSession player,
        EntityUid station,
        string? requestedJob,
        HumanoidCharacterProfile profile,
        HashSet<ProtoId<JobPrototype>> disallowed,
        out ProtoId<JobPrototype> job,
        out EntityUid jobStation)
    {
        job = default;
        jobStation = default;

        if (!IsFoFRound())
            return false;

        string target;
        // Faction lock disabled: every spawn runs the balance check, otherwise locked
        // respawners keep feeding the leading side past the gap
        // if (_factions.TryGetValue(player.UserId, out var locked))
        //     target = locked;
        // else
        {
            CountSides(out var govfor, out var opfor);

            if (Math.Abs(govfor - opfor) <= _cfg.GetCVar(CCVars.FoFMaxGap))
                return false;

            target = govfor < opfor ? "GOVFOR" : "OPFOR";
        }

        var otherJobs = target == "GOVFOR" ? _opforJobs : _govforJobs;

        if (requestedJob is { } requested)
        {
            var req = new ProtoId<JobPrototype>(requested);
            if (!_govforJobs.Contains(req) && !_opforJobs.Contains(req))
                return false;

            if (!otherJobs.Contains(req))
            {
                job = req;
                jobStation = station;
                return true;
            }
            _chat.DispatchServerMessage(player, Loc.GetString("cmu-fof-faction-lock-forced"));
        }

        var banned = new HashSet<ProtoId<JobPrototype>>(disallowed);
        banned.UnionWith(otherJobs);

        var priorities = FofJobs.MapSide(profile.JobPriorities, target, keepNeutral: false, ProtoMan);

        var rifleman = target == "GOVFOR" ? GovforRifleman : OpforRifleman;
        var stations = _station.GetStations().ToList();
        _random.Shuffle(stations);
        foreach (var candidate in stations)
        {
            if (_stationJobs.PickBestAvailableJobWithPriority(candidate, priorities, true, banned) is not { } picked)
                continue;

            job = picked;
            jobStation = candidate;
            return true;
        }

        foreach (var candidate in stations)
        {
            var overflowJobs = _stationJobs.GetOverflowJobs(candidate);
            var open = _stationJobs.GetAvailableJobs(candidate)
                .Where(id => id.Id.Contains(target)
                    && !overflowJobs.Contains(id)
                    && !banned.Contains(id))
                .ToList();

            if (open.Count == 0)
                continue;

            job = _random.Pick(open);
            jobStation = candidate;
            return true;
        }

        if (disallowed.Contains(rifleman))
            return false;

        job = rifleman;
        jobStation = stations.FirstOrDefault();
        return jobStation != EntityUid.Invalid;
    }

    private bool IsFoFRound()
    {
        var presetId = _gameTicker.CurrentPreset?.ID ?? _gameTicker.Preset?.ID;
        return presetId is "ForceOnForce" or "forceonforce";
    }

    private void CountSides(out int govfor, out int opfor)
    {
        govfor = 0;
        opfor = 0;
        foreach (var (userId, faction) in _factions)
        {
            if (!_playerManager.TryGetSessionById(userId, out _))
                continue;

            if (faction == "GOVFOR")
                govfor++;
            else
                opfor++;
        }
    }

    /// <summary>
    /// Opens the balance confirm popup when the requested job sits on the leading side and the
    /// gap is past the cvar. Returns true when the popup was sent and the spawn must wait.
    /// </summary>
    public bool TryOpenBalanceConfirm(ICommonSession player, EntityUid station, string? jobId)
    {
        if (jobId is not { } requested
            || !IsFoFRound())
            return false;

        CountSides(out var govfor, out var opfor);

        var maxGap = _cfg.GetCVar(CCVars.FoFMaxGap);
        if (Math.Abs(govfor - opfor) <= maxGap)
            return false;

        var target = govfor < opfor ? "GOVFOR" : "OPFOR";
        var leadingJobs = target == "GOVFOR" ? _opforJobs : _govforJobs;
        if (!leadingJobs.Contains(new ProtoId<JobPrototype>(requested)))
            return false;

        if (_pendingJoins.Remove(player.UserId, out var pending)
            && pending.Job.Id == requested
            && pending.ConfirmedAt != TimeSpan.MinValue
            && _timing.RealTime - pending.ConfirmedAt <= TimeSpan.FromSeconds(ConfirmValiditySeconds))
            return false;

        _pendingJoins[player.UserId] = new PendingBalanceJoin(
            station,
            new ProtoId<JobPrototype>(requested),
            TimeSpan.MinValue);
        RaiseNetworkEvent(new FoFBalanceConfirmEvent(govfor, opfor, maxGap), player.Channel);
        return true;
    }

    private void OnBalanceConfirmed(FoFBalanceConfirmMessage message)
    {
        var userId = message.MsgChannel.UserId;
        if (!_pendingJoins.TryGetValue(userId, out var pending)
            || pending.ConfirmedAt != TimeSpan.MinValue)
            return;

        if (!_playerManager.TryGetSessionById(userId, out var session))
            return;

        if (_gameTicker.PlayerGameStatuses.TryGetValue(userId, out var status)
            && status == PlayerGameStatus.JoinedGame)
        {
            _pendingJoins.Remove(userId);
            return;
        }

        _pendingJoins[userId] = pending with { ConfirmedAt = _timing.RealTime };
        _gameTicker.MakeJoinGame(session, pending.Station, pending.Job.Id);
    }
}
