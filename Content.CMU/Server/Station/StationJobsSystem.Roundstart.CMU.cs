using System.Linq;
using Content.Server.Administration.Managers;
using Content.Server.Antag;
using Content.Server.Station.Events;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Network;
using Robust.Shared.Profiling;
using Robust.Shared.Prototypes;

namespace Content.Server.Station.Systems;

public sealed partial class StationJobsSystem
{
    [Dependency] private AntagSelectionSystem _antag = default!;
    [Dependency] private IBanManager _banManager = default!;
    [Dependency] private ProfManager _cmuRoundStartProf = default!;

    private readonly record struct JobCandidateBucket(int Weight, JobPriority Priority);

    private sealed class RoundStartJobEligibilitySnapshot
    {
        public Dictionary<JobCandidateBucket,
            Dictionary<NetUserId, List<ProtoId<JobPrototype>>>> Candidates { get; } = [];

        public int EntryCount { get; private set; }

        public void Add(JobCandidateBucket bucket, NetUserId player, ProtoId<JobPrototype> job)
        {
            if (!Candidates.TryGetValue(bucket, out var players))
            {
                players = [];
                Candidates.Add(bucket, players);
            }

            if (!players.TryGetValue(player, out var jobs))
            {
                jobs = [];
                players.Add(player, jobs);
            }

            jobs.Add(job);
            EntryCount++;
        }
    }

    /// <summary>
    /// Captures all live job-eligibility inputs once for this synchronous assignment pass.
    /// </summary>
    private RoundStartJobEligibilitySnapshot CreateRoundStartJobEligibilitySnapshot(
        IReadOnlyDictionary<NetUserId, HumanoidCharacterProfile> profiles)
    {
        using var profile = _cmuRoundStartProf.Group("CMU Round Job Eligibility Snapshot");
        var snapshot = new RoundStartJobEligibilitySnapshot();
        var antags = _antag.GetAntagJobs();
        var evaluatedPlayers = 0;

        foreach (var (player, character) in profiles)
        {
            var roleBans = _banManager.GetJobBans(player);
            var profileJobs = character.JobPriorities.Keys.ToList();
            var ev = new StationJobsGetCandidatesEvent(player, profileJobs);
            RaiseLocalEvent(ref ev);
            evaluatedPlayers++;

            // Shouldn't happen but you know :P
            if (!_player.TryGetSessionById(player, out var session))
                continue;

            var (whitelist, blacklist) = antags.GetValueOrDefault(session);

            foreach (var jobId in profileJobs)
            {
                if (!ProtoMan.Resolve(jobId, out JobPrototype? job))
                    continue;

                if (whitelist != null && !whitelist.Contains(jobId))
                    continue;

                if (blacklist != null && blacklist.Contains(jobId))
                    continue;

                if (!(roleBans == null || !roleBans.Contains(jobId))) // TODO: Replace with IsRoleBanned.
                    continue;

                var priority = character.JobPriorities[jobId];
                if (priority == JobPriority.Never)
                    continue;

                snapshot.Add(new JobCandidateBucket(job.Weight, priority), player, jobId);
            }
        }

        if (_cmuRoundStartProf.IsEnabled)
        {
            _cmuRoundStartProf.WriteValue("CMU Round Job Eligibility Players", evaluatedPlayers);
            _cmuRoundStartProf.WriteValue("CMU Round Job Eligibility Entries", snapshot.EntryCount);
            _cmuRoundStartProf.WriteValue("CMU Round Job Eligibility Buckets", snapshot.Candidates.Count);
        }

        return snapshot;
    }
}
