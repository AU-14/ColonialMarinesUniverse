using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.Station.Systems;
using System.Linq;
using Content.Shared.GameTicking;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Localization;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Server.Player;

namespace Content.Server.CMU14.Round;

/// <summary>
/// Tracks which faction each player spawned as during Force on Force. Mid-round spawns
/// (latejoin and respawn) stay locked to that faction so round intel stays one-sided, and
/// unrestricted joiners are steered to whichever side is behind. Round-start dealing lives
/// in StationJobsSystem.AssignJobs.
/// </summary>
public sealed class ForceOnForceFactionSystem : EntitySystem
{
    // joiners may only pick the leading side once the gap exceeds this; dead-but-connected
    // players count toward their side (they respawn into it), disconnected ones do not
    private const int MaxGap = 3;

    private static readonly ProtoId<JobPrototype> GovforRifleman = "AU14JobGOVFORSquadRifleman";
    private static readonly ProtoId<JobPrototype> OpforRifleman = "AU14JobOPFORSquadRifleman";

    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly StationJobsSystem _stationJobs = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private readonly Dictionary<NetUserId, string> _factions = new();

    private readonly HashSet<ProtoId<JobPrototype>> _govforJobs = new();
    private readonly HashSet<ProtoId<JobPrototype>> _opforJobs = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnSpawnComplete);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);

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

        var presetId = _gameTicker.CurrentPreset?.ID ?? _gameTicker.Preset?.ID;
        if (presetId is not ("ForceOnForce" or "forceonforce"))
            return false;

        string target;
        if (_factions.TryGetValue(player.UserId, out var locked))
        {
            target = locked;
        }
        else
        {
            var govfor = 0;
            var opfor = 0;
            foreach (var (userId, faction) in _factions)
            {
                if (!_playerManager.TryGetSessionById(userId, out _))
                    continue;

                if (faction == "GOVFOR")
                    govfor++;
                else
                    opfor++;
            }

            if (Math.Abs(govfor - opfor) <= MaxGap)
                return false;

            target = govfor < opfor ? "GOVFOR" : "OPFOR";
        }

        var other = target == "GOVFOR" ? "OPFOR" : "GOVFOR";
        var otherJobs = target == "GOVFOR" ? _opforJobs : _govforJobs;

        if (requestedJob is { } requested)
        {
            var req = new ProtoId<JobPrototype>(requested);
            if (!_govforJobs.Contains(req) && !_opforJobs.Contains(req))
                return false; // non-faction job, not our business

            if (!otherJobs.Contains(req))
            {
                job = req;
                jobStation = station;
                return true;
            }
            // requested the wrong side; fall through to a mapped pick
            _chat.DispatchServerMessage(player, Loc.GetString("cmu-fof-faction-lock-forced"));
        }

        var banned = new HashSet<ProtoId<JobPrototype>>(disallowed);
        banned.UnionWith(otherJobs);

        // map the player's preferences onto the allowed side, rifleman as guaranteed landing
        var priorities = new Dictionary<ProtoId<JobPrototype>, JobPriority>();
        foreach (var (pref, priority) in profile.JobPriorities)
        {
            var id = pref.Id;
            if (id.Contains(other))
            {
                id = id.Replace(other, target);
                if (!ProtoMan.HasIndex<JobPrototype>(id))
                    continue; // no equivalent role on the allowed side
            }
            else if (!id.Contains(target))
            {
                continue;
            }

            priorities[new ProtoId<JobPrototype>(id)] = priority;
        }

        priorities.TryAdd(target == "GOVFOR" ? GovforRifleman : OpforRifleman, JobPriority.Low);

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

        return false;
    }
}
