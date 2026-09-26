using System.Linq;
using Content.Server.CMU14.ForceOnForce;
using Content.Shared.CMU14.Round.Roles;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared._RMC14.Marines;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Player;

namespace Content.Server.Station.Systems;

public sealed partial class StationJobsSystem
{
    private bool IsForceOnForce => string.Equals(_gameTicker.CurrentPreset?.ID ?? _gameTicker.Preset?.ID,
        "ForceOnForce", StringComparison.OrdinalIgnoreCase);

    public bool CanJoinForceOnForceSide(ProtoId<JobPrototype> jobId)
    {
        if (!IsForceOnForce || !ProtoMan.TryIndex(jobId, out var job) ||
            job.RoundSide is not (RoundJobSide.Govfor or RoundJobSide.Opfor)) return true;
        var govfor = 0;
        var opfor = 0;
        var query = EntityQueryEnumerator<MarineComponent, MobStateComponent, ActorComponent>();
        while (query.MoveNext(out var marine, out var mob, out _))
        {
            if (mob.CurrentState == MobState.Dead) continue;
            if (string.Equals(marine.Faction, "govfor", StringComparison.OrdinalIgnoreCase)) govfor++;
            if (string.Equals(marine.Faction, "opfor", StringComparison.OrdinalIgnoreCase)) opfor++;
        }
        return job.RoundSide == RoundJobSide.Govfor ? govfor <= opfor : opfor <= govfor;
    }

    private Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)> AssignForceOnForceJobs(
        Dictionary<NetUserId, HumanoidCharacterProfile> profiles, IReadOnlyList<EntityUid> stations)
    {
        var jobs = new List<(ProtoId<JobPrototype> Job, EntityUid Station, RoundJobSide Side)>();
        var slots = new List<ForceOnForceAssignment.Slot>();
        foreach (var station in stations)
        {
            foreach (var (id, count) in GetJobs(station))
            {
                if (count is <= 0 || !ProtoMan.TryIndex(id, out var job) ||
                    job.RoundSide is not (RoundJobSide.Govfor or RoundJobSide.Opfor)) continue;
                jobs.Add((id, station, job.RoundSide));
                slots.Add(new(job.RoundSide == RoundJobSide.Govfor ? 0 : 1, count ?? profiles.Count));
            }
        }

        var costs = new Dictionary<(NetUserId Player, ProtoId<JobPrototype> Job), int>();
        var expanded = new Dictionary<NetUserId, HumanoidCharacterProfile>();
        foreach (var (player, profile) in profiles)
        {
            var priorities = new Dictionary<ProtoId<JobPrototype>, JobPriority>();
            foreach (var (id, _, side) in jobs)
            {
                var job = ProtoMan.Index(id);
                if (!EntityManager.System<ForceOnForceRespawnSystem>().CanJoinSide(player, side))
                    continue;
                var cost = GetForceOnForceJobCost(profile, job);
                if (cost == null || _gameTicker.ResolveProfileForAllegiance(player, profile, id.Id) == null) continue;
                // Resolve character eligibility before balancing; candidate events then enforce
                // playtime, whitelists, bans, and antag exclusions.
                priorities[id] = JobPriority.Low;
                costs[(player, id)] = cost.Value;
            }
            expanded[player] = profile.WithJobPriorities(priorities);
        }
        var eligible = GetJobCandidates(expanded);
        var players = profiles.Keys.ToList();
        _random.Shuffle(players);
        var candidates = new List<ForceOnForceAssignment.Candidate>();
        for (var p = 0; p < players.Count; p++)
        {
            for (var j = 0; j < jobs.Count; j++)
            {
                var id = jobs[j].Job;
                if (costs.TryGetValue((players[p], id), out var cost) &&
                    eligible.TryGetValue(id, out var priorities) && priorities.Values.Any(set => set.Contains(players[p])))
                    candidates.Add(new(p, j, cost));
            }
        }

        var result = new Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)>();
        foreach (var (player, slot) in ForceOnForceAssignment.Assign(players.Count, slots, candidates))
            result[players[player]] = (jobs[slot].Job, jobs[slot].Station);
        return result;
    }

    private int? GetForceOnForceJobCost(HumanoidCharacterProfile profile, JobPrototype job)
    {
        var desired = profile.FoFSide switch
        {
            ForceOnForceSide.Govfor => RoundJobSide.Govfor,
            ForceOnForceSide.Opfor => RoundJobSide.Opfor,
            _ => RoundJobSide.None,
        };
        var otherSide = desired != RoundJobSide.None && job.RoundSide != desired;
        if (otherSide && !profile.FoFFallback.HasFlag(ForceOnForceFallback.OtherSide)) return null;

        var priority = profile.GetForceOnForceJobPriority(job, ProtoMan);
        if (priority > JobPriority.Never) return ((int) JobPriority.High - (int) priority) * 100 + (otherSide ? 50 : 0);
        return profile.FoFFallback.HasFlag(ForceOnForceFallback.OtherRole) && job.RoundRole == "SquadRifleman"
            ? 400 + (otherSide ? 50 : 0)
            : null;
    }
}
