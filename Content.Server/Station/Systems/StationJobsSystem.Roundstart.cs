using System.Linq;
using Content.Server.Administration.Managers;
using Content.Server.Antag;
using Content.Server.CMU14.Round;
using Content.Server.Station.Components;
using Content.Server.Station.Events;
using Content.Shared.CMU14;
using Content.Shared.CCVar;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Content.Shared.Station.Components;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.Station.Systems;

// Contains code for round-start spawning.
public sealed partial class StationJobsSystem
{
    [Dependency] private IBanManager _banManager = default!;
    [Dependency] private AuJobSelectionSystem _auJobSelectionSystem = default!;
    [Dependency] private StationSystem _stationSystem = default!;
    [Dependency] private SharedJobSystem _jobs = default!;
    [Dependency] private AntagSelectionSystem _antag = default!;

    // Toggle used for ForceOnForce overflow assignment to alternate GOVFOR/OPFOR rifleman
    private bool _forceOnForceNextGovfor = true;

    private int GetJobWeight(EntityUid station, JobPrototype job)
    {
        var jobWeights = TryComp<StationDataComponent>(station, out var stationData)
            ? stationData.JobWeights
            : null;

        return TryGetJobWeight(job, jobWeights, out var weight) ? weight : 0;
    }

    /// <summary>
    /// Resolves a job's map-specific weight, falling back to the global default profile.
    /// </summary>
    /// <returns>True, using the legacy per-job weight when neither profile defines this job.</returns>
    public bool TryGetJobWeight(
        JobPrototype job,
        ProtoId<JobWeightPrototype>? mapWeights,
        out int weight)
    {
        if (mapWeights != null
            && ProtoMan.TryIndex(mapWeights.Value, out var mapProfile)
            && mapProfile.Weights.TryGetValue(job.ID, out weight))
        {
            return true;
        }

        if (ProtoMan.TryIndex(JobWeightPrototype.Default, out var defaultProfile)
            && defaultProfile.Weights.TryGetValue(job.ID, out weight))
        {
            return true;
        }

        weight = job.Weight;
        return true;
    }

    /// <summary>
    /// Returns whether the global fallback job-weight profile is available.
    /// </summary>
    public bool HasDefaultJobWeights()
    {
        return ProtoMan.HasIndex<JobWeightPrototype>(JobWeightPrototype.Default);
    }

    /// <summary>
    /// Assigns jobs based on the given preferences and list of stations to assign for.
    /// This does NOT change the slots on the station, only figures out where each player should go.
    /// </summary>
    /// <param name="profiles">The profiles to use for selection.</param>
    /// <param name="stations">List of stations to assign for.</param>
    /// <param name="useRoundStartJobs">Whether or not to use the round-start minimum jobs for the stations.</param>
    /// <returns>List of players and their assigned jobs.</returns>
    /// <remarks>
    /// You probably shouldn't use useRoundStartJobs mid-round if the station has been available to join,
    /// as there may end up being more round-start slots than available slots, which can cause weird behavior.
    /// Round-start allocation attempts each station's minimum roles first, ordered by the station's job weights.
    /// Unpreferred minimum roles can use an eligible random player when configured to do so.
    /// It then considers remaining players in random order and gives each their highest available preference.
    /// </remarks>
    public Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)> AssignJobs(
        Dictionary<NetUserId, HumanoidCharacterProfile> profiles,
        IReadOnlyList<EntityUid> stations,
        bool useRoundStartJobs = true)
    {
        DebugTools.Assert(stations.Count > 0);

        // Reset alternation each round so ForceOnForce starts consistently.
        _forceOnForceNextGovfor = true;

        if (profiles.Count == 0)
            return new();

        // We need to modify this collection later, so make a copy of it.
        profiles = profiles.ShallowClone();

        // Player <-> (job, station)
        var assigned = new Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)>(profiles.Count);

        // --- AU14: Assign forced jobs first ---
        var forcedAssignments = _auJobSelectionSystem.ForcedJobAssignments;
        var forcedToRemove = new List<NetUserId>();
        foreach (var (player, jobId) in forcedAssignments)
        {
            if (!profiles.ContainsKey(player))
                continue;
            // Find a station with the job available
            EntityUid? assignedStation = null;
            ProtoId<JobPrototype>? protoJob = null;
            foreach (var station in stations)
            {
                var jobs = useRoundStartJobs ? GetRoundStartJobs(station) : GetJobs(station);
                if (jobs.ContainsKey(jobId) && (jobs[jobId] == null || jobs[jobId] > 0))
                {
                    assignedStation = station;
                    protoJob = new ProtoId<JobPrototype>(jobId);
                    break;
                }
            }
            // Third-party utility jobs are only used as role labels after
            // ThirdPartySystem spawns the real entity. Falling back to a normal
            // station spawn for them creates naked placeholder bodies.
            if (assignedStation == null && (jobId == "AU14JobThirdPartyLeader" || jobId == "AU14JobThirdPartyMember"))
                continue;

            // If not found, just assign to first station (fallback)
            if (assignedStation == null && stations.Count > 0)
            {
                assignedStation = stations[0];
                protoJob = new ProtoId<JobPrototype>(jobId);
            }
            assigned[player] = (protoJob, assignedStation ?? EntityUid.Invalid);
            forcedToRemove.Add(player);
        }
        // Remove forced players from profiles so they are not assigned again
        foreach (var player in forcedToRemove)
        {
            profiles.Remove(player);
        }

        // The maximum jobs left on each station. This is modified as players are assigned.
        var stationJobs = new Dictionary<EntityUid, Dictionary<ProtoId<JobPrototype>, int?>>();
        var stationMinimumJobs = new Dictionary<EntityUid, Dictionary<ProtoId<JobPrototype>, int?>>();
        foreach (var station in stations)
        {
            stationJobs.Add(station, GetJobs(station).ToDictionary(x => x.Key, x => x.Value));
            stationMinimumJobs.Add(
                station,
                useRoundStartJobs
                    ? GetRoundStartJobs(station)
                    : new Dictionary<ProtoId<JobPrototype>, int?>());
        }

        // Jobs assigned after this point must satisfy bans, antag restrictions, and any other candidate filter.
        // The minimum phase selects players for a job*, and the maximum phase selects jobs for a player.
        var jobCandidates = GetJobCandidates(profiles);
        var playerCandidates = GetPlayerCandidates(jobCandidates);

        // Phase one: complete every required role on a station before considering the next station.
        // Within a station, job priority win over player preference; player preference breaks ties between candidates.
        var jobFallback = _configurationManager.GetCVar(CCVars.GameMinimumJobFallback);

        foreach (var station in stations)
        {
            var requiredJobs = stationMinimumJobs[station]
                .Where(x => x.Value is > 0)
                .OrderByDescending(x => GetJobWeight(station, ProtoMan.Index(x.Key)))
                .ThenBy(x => x.Key.Id)
                .ToList();

            foreach (var (job, minimum) in requiredJobs)
            {
                for (var assignedToJob = 0; assignedToJob < minimum!.Value && profiles.Count > 0; assignedToJob++)
                {
                    if (stationJobs[station][job] is <= 0)
                        break;

                    if (!TryPickCandidate(job, jobCandidates, out var player) &&
                        !TryPickMinimumJobFallbackCandidate(
                            job, profiles, jobFallback, out player))
                    {
                        break;
                    }

                    AssignPlayer(player, job, station, stationJobs, jobCandidates, playerCandidates, profiles, assigned);
                }
            }
        }

        // Phase two: each remaining player gets their highest available preference. Shuffle the player order and
        // equal-priority jobs so contention is still fair, while preserving station-by-station allocation.
        foreach (var station in stations)
        {
            var players = profiles.Keys.ToList();
            _random.Shuffle(players);

            foreach (var player in players)
            {
                if (TryPickJob(player, station, stationJobs, playerCandidates, out var job))
                    AssignPlayer(player, job, station, stationJobs, jobCandidates, playerCandidates, profiles, assigned);
            }
        }

        return assigned;
    }

    private void RemovePlayerFromCandidates(
        NetUserId player,
        Dictionary<ProtoId<JobPrototype>, Dictionary<JobPriority, HashSet<NetUserId>>> jobCandidates,
        Dictionary<NetUserId, Dictionary<JobPriority, List<ProtoId<JobPrototype>>>> playerCandidates)
    {
        foreach (var priorities in jobCandidates.Values)
        {
            foreach (var players in priorities.Values)
            {
                players.Remove(player);
            }
        }

        playerCandidates.Remove(player);
    }

    private bool TryPickCandidate(
        ProtoId<JobPrototype> job,
        Dictionary<ProtoId<JobPrototype>, Dictionary<JobPriority, HashSet<NetUserId>>> jobCandidates,
        out NetUserId player)
    {
        if (!jobCandidates.TryGetValue(job, out var candidates))
        {
            player = default;
            return false;
        }

        for (var priority = JobPriority.High; priority > JobPriority.Never; priority--)
        {
            if (!candidates.TryGetValue(priority, out var players) || players.Count == 0)
                continue;

            player = _random.Pick(players);
            return true;
        }

        player = default;
        return false;
    }

    private bool TryPickJob(
        NetUserId player,
        EntityUid station,
        Dictionary<EntityUid, Dictionary<ProtoId<JobPrototype>, int?>> stationJobs,
        Dictionary<NetUserId, Dictionary<JobPriority, List<ProtoId<JobPrototype>>>> playerCandidates,
        out ProtoId<JobPrototype> job)
    {
        if (!playerCandidates.TryGetValue(player, out var candidates))
        {
            job = default;
            return false;
        }

        for (var priority = JobPriority.High; priority > JobPriority.Never; priority--)
        {
            if (!candidates.TryGetValue(priority, out var jobs))
                continue;

            var availableJobs = jobs
                .Where(jobId => stationJobs[station].TryGetValue(jobId, out var slots) && slots is null or > 0)
                .ToList();
            if (availableJobs.Count == 0)
                continue;

            job = _random.Pick(availableJobs);
            return true;
        }

        job = default;
        return false;
    }

    private void AssignPlayer(
        NetUserId player,
        ProtoId<JobPrototype> job,
        EntityUid station,
        Dictionary<EntityUid, Dictionary<ProtoId<JobPrototype>, int?>> stationJobs,
        Dictionary<ProtoId<JobPrototype>, Dictionary<JobPriority, HashSet<NetUserId>>> jobCandidates,
        Dictionary<NetUserId, Dictionary<JobPriority, List<ProtoId<JobPrototype>>>> playerCandidates,
        Dictionary<NetUserId, HumanoidCharacterProfile> profiles,
        Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)> assigned)
    {
        if (stationJobs[station][job] is { } slots)
            stationJobs[station][job] = slots - 1;

        RemovePlayerFromCandidates(player, jobCandidates, playerCandidates);
        profiles.Remove(player);
        assigned.Add(player, (job, station));
    }

    /// <summary>
    /// Attempts to assign overflow jobs to any player in allPlayersToAssign that is not in assignedJobs.
    /// </summary>
    /// <param name="assignedJobs">All assigned jobs.</param>
    /// <param name="allPlayersToAssign">All players that might need an overflow assigned.</param>
    /// <param name="profiles">Player character profiles.</param>
    /// <param name="stations">The stations to consider for spawn location.</param>
    public void AssignOverflowJobs(
        ref Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)> assignedJobs,
        IEnumerable<NetUserId> allPlayersToAssign,
        IReadOnlyDictionary<NetUserId, HumanoidCharacterProfile> profiles,
        IReadOnlyList<EntityUid> stations)
    {
        var givenStations = stations.ToList();
        if (givenStations.Count == 0)
            return; // Don't attempt to assign them if there are no stations.

        // For players without jobs, give them the overflow job if they have that set...
        // Determine the current preset so we can apply gamemode specific overflow behaviour.
        var presetId = _gameTicker.CurrentPreset?.ID ?? _gameTicker.Preset?.ID;

        foreach (var player in allPlayersToAssign)
        {
            if (assignedJobs.ContainsKey(player))
                continue;

            var profile = profiles[player];
            if (profile.PreferenceUnavailable != PreferenceUnavailableMode.SpawnAsOverflow)
            {
                assignedJobs.Add(player, (null, EntityUid.Invalid));
                continue;
            }

            _random.Shuffle(givenStations);

            // Build a mapping of station -> ship faction (if any) so we can prefer shipside stations for specific factions.
            var stationFaction = new Dictionary<EntityUid, string?>();
            var shipQuery = EntityQueryEnumerator<ShipFactionComponent>();
            while (shipQuery.MoveNext(out var shipUid, out var shipComp))
            {
                var owning = _stationSystem.GetOwningStation(shipUid);
                if (owning != null)
                {
                    stationFaction[owning.Value] = shipComp.Faction?.ToLowerInvariant();
                }
            }

            // Try to select a station+overflow job pair according to gamemode rules.
            var bannedRoles = _banManager.GetRoleBans(player)?.Select(role => role.RoleId).ToHashSet();
            foreach (var station in givenStations)
            {
                ProtoId<JobPrototype>? chosenOverflow = null;

                // Helper proto ids for common roles
                var protoColonist = new ProtoId<JobPrototype>("AU14JobCivilianColonist");
                var protoGovRifle = new ProtoId<JobPrototype>("AU14JobGOVFORSquadRifleman");
                var protoOpfRifle = new ProtoId<JobPrototype>("AU14JobOPFORSquadRifleman");

                var stationOverflows = GetOverflowJobs(station)
                    .Where(job => bannedRoles == null || !bannedRoles.Contains(job.Id))
                    .ToHashSet();

                // Colony modes: prefer colonist
                if (!string.IsNullOrEmpty(presetId) && (presetId.Equals("Insurgency", StringComparison.InvariantCultureIgnoreCase) || presetId.Equals("ColonyFall", StringComparison.InvariantCultureIgnoreCase)))
                {
                    if (stationOverflows.Contains(protoColonist))
                        chosenOverflow = protoColonist;
                }

                // Distress signal: put them as GOVFOR rifleman if possible and prefer GOVFOR ships
                if (chosenOverflow == null && !string.IsNullOrEmpty(presetId) && presetId.Equals("DistressSignal", StringComparison.InvariantCultureIgnoreCase))
                {
                    // Prefer GOVFOR stations (ships) if present
                    if (stationFaction.TryGetValue(station, out var faction) && faction != null && faction == "govfor")
                    {
                        var jobs = GetJobs(station);
                        if ((bannedRoles == null || !bannedRoles.Contains(protoGovRifle.Id)) &&
                            (jobs.ContainsKey(protoGovRifle) || stationOverflows.Contains(protoGovRifle)))
                            chosenOverflow = protoGovRifle;
                    }

                    // Fallback: any station that has the job
                    if (chosenOverflow == null)
                    {
                        if ((bannedRoles == null || !bannedRoles.Contains(protoGovRifle.Id)) &&
                            stationOverflows.Contains(protoGovRifle))
                            chosenOverflow = protoGovRifle;
                        else
                        {
                            var jobs = GetJobs(station);
                            if ((bannedRoles == null || !bannedRoles.Contains(protoGovRifle.Id)) &&
                                jobs.ContainsKey(protoGovRifle))
                                chosenOverflow = protoGovRifle;
                        }
                    }
                }

                // Force on Force: alternate between GOVFOR and OPFOR rifleman and prefer the ship station for that faction
                if (chosenOverflow == null && !string.IsNullOrEmpty(presetId) && presetId.Equals("ForceOnForce", StringComparison.InvariantCultureIgnoreCase))
                {
                    var wantGov = _forceOnForceNextGovfor;
                    var wantProto = wantGov ? protoGovRifle : protoOpfRifle;

                    // If this station matches the faction we want, pick it.
                    if (stationFaction.TryGetValue(station, out var faction) && faction != null && ((wantGov && faction == "govfor") || (!wantGov && faction == "opfor")))
                    {
                        var jobs = GetJobs(station);
                        if ((bannedRoles == null || !bannedRoles.Contains(wantProto.Id)) &&
                            (jobs.ContainsKey(wantProto) || stationOverflows.Contains(wantProto)))
                            chosenOverflow = wantProto;
                    }
                    else
                    {
                        // Otherwise, if the station has the job in overflow or regular jobs, pick it as fallback.
                        if ((bannedRoles == null || !bannedRoles.Contains(wantProto.Id)) &&
                            stationOverflows.Contains(wantProto))
                            chosenOverflow = wantProto;
                        else
                        {
                            var jobs = GetJobs(station);
                            if ((bannedRoles == null || !bannedRoles.Contains(wantProto.Id)) &&
                                jobs.ContainsKey(wantProto))
                                chosenOverflow = wantProto;
                        }
                    }

                    // If we successfully chose one, flip the toggle for the next assignment
                    if (chosenOverflow != null)
                        _forceOnForceNextGovfor = !_forceOnForceNextGovfor;
                }

                // Fallback: pick any overflow job on the station as before
                if (chosenOverflow == null)
                {
                    var overflows = stationOverflows.ToList();
                    _random.Shuffle(overflows);
                    if (overflows.Count == 0)
                        continue;
                    chosenOverflow = overflows[0];
                }

                assignedJobs.Add(player, (chosenOverflow, station));
                break;
            }

            if (!assignedJobs.ContainsKey(player))
                assignedJobs.Add(player, (null, EntityUid.Invalid));
        }
    }

    public void CalcExtendedAccess(Dictionary<EntityUid, int> jobsCount)
    {
        // Calculate whether stations need to be on extended access or not.
        foreach (var (station, count) in jobsCount)
        {
            var jobs = Comp<StationJobsComponent>(station);

            var thresh = jobs.ExtendedAccessThreshold;

            jobs.ExtendedAccess = count <= thresh;

            Log.Debug("Station {Station} on extended access: {ExtendedAccess}",
                Name(station), jobs.ExtendedAccess);
        }
    }

    /// <summary>
    /// Gets all jobs that the input players can receive, grouped by their selected preference priority.
    /// </summary>
    /// <param name="profiles">Profiles to look in.</param>
    /// <returns>Jobs and their eligible players, grouped by player preference.</returns>
    private Dictionary<ProtoId<JobPrototype>, Dictionary<JobPriority, HashSet<NetUserId>>> GetJobCandidates(
        IReadOnlyDictionary<NetUserId, HumanoidCharacterProfile> profiles)
    {
        var outputDict = new Dictionary<ProtoId<JobPrototype>, Dictionary<JobPriority, HashSet<NetUserId>>>();

        var antags = _antag.GetAntagJobs();
        var antagBlocked = _antag.GetPreSelectedAntagSessions();

        foreach (var (player, profile) in profiles)
        {
            var roleBans = _banManager.GetJobBans(player);
            var profileJobs = profile.JobPriorities.Keys.Select(k => new ProtoId<JobPrototype>(k)).ToList();
            var ev = new StationJobsGetCandidatesEvent(player, profileJobs);
            RaiseLocalEvent(ref ev);

            // Shouldn't happen but you know :P
            if (!_player.TryGetSessionById(player, out var session))
                continue;

            var (whitelist, blacklist) = antags.GetValueOrDefault(session);

            foreach (var jobId in profileJobs)
            {
                if (!profile.JobPriorities.TryGetValue(jobId, out var priority) || priority == JobPriority.Never)
                    continue;

                if (!ProtoMan.Resolve(jobId, out var job))
                    continue;

                if (!job.CanBeAntag && antagBlocked.Contains(session))
                    continue;

                if (whitelist != null && !whitelist.Contains(jobId))
                    continue;

                if (blacklist != null && blacklist.Contains(jobId))
                    continue;

                if (!(roleBans == null || !roleBans.Contains(jobId))) //TODO: Replace with IsRoleBanned
                    continue;

                if (!outputDict.TryGetValue(jobId, out var priorities))
                {
                    priorities = new Dictionary<JobPriority, HashSet<NetUserId>>();
                    outputDict.Add(jobId, priorities);
                }

                if (!priorities.TryGetValue(priority, out var players))
                {
                    players = new HashSet<NetUserId>();
                    priorities.Add(priority, players);
                }

                players.Add(player);
            }
        }

        return outputDict;
    }

    /// <summary>
    /// Tries the configured fallback for a required role that has no direct preference candidates.
    /// </summary>
    private bool TryPickMinimumJobFallbackCandidate(
        ProtoId<JobPrototype> job,
        IReadOnlyDictionary<NetUserId, HumanoidCharacterProfile> profiles,
        MinimumJobFallback fallback,
        out NetUserId player)
    {
        switch (fallback)
        {
            case MinimumJobFallback.SameDepartment:
                return TryPickSameDepartmentCandidate(job, profiles, out player);

            case MinimumJobFallback.AnyEligiblePlayer:
                if (TryPickSameDepartmentCandidate(job, profiles, out player))
                    return true;

                return TryPickCandidateIgnoringPreferences(job, profiles, out player);

            default:
                player = default;
                return false;
        }
    }

    /// <summary>
    /// Gets a random eligible player who prefers a role in the target job's primary department.
    /// </summary>
    private bool TryPickSameDepartmentCandidate(
        ProtoId<JobPrototype> job,
        IReadOnlyDictionary<NetUserId, HumanoidCharacterProfile> profiles,
        out NetUserId player)
    {
        if (!_jobs.TryGetPrimaryDepartment(job.Id, out var department) || department.IgnoreForDepartmentFallback)
        {
            player = default;
            return false;
        }

        var matchingProfiles = profiles
            .Where(pair => pair.Value.JobPriorities.Any(preference =>
                preference.Value != JobPriority.Never && department.Roles.Contains(preference.Key)))
            .ToDictionary();
        return TryPickCandidateIgnoringPreferences(job, matchingProfiles, out player);
    }

    /// <summary>
    /// Gets a random eligible player for a required role without requiring a preference for that role.
    /// </summary>
    /// <remarks>
    /// This deliberately uses the same candidate-filter event as normal assignment so role timers and whitelists
    /// still apply. The only criterion omitted is the player's job preference.
    /// </remarks>
    private bool TryPickCandidateIgnoringPreferences(
        ProtoId<JobPrototype> job,
        IReadOnlyDictionary<NetUserId, HumanoidCharacterProfile> profiles,
        out NetUserId player)
    {
        if (!ProtoMan.Resolve(job, out var jobPrototype))
        {
            player = default;
            return false;
        }

        var candidates = new HashSet<NetUserId>();
        var antags = _antag.GetAntagJobs();
        var antagBlocked = _antag.GetPreSelectedAntagSessions();

        foreach (var (userId, _) in profiles)
        {
            if (!_player.TryGetSessionById(userId, out var session))
                continue;

            var jobs = new List<ProtoId<JobPrototype>> { job };
            var ev = new StationJobsGetCandidatesEvent(userId, jobs);
            RaiseLocalEvent(ref ev);

            if (!jobs.Contains(job))
                continue;

            var roleBans = _banManager.GetJobBans(userId);
            var (whitelist, blacklist) = antags.GetValueOrDefault(session);
            if ((!jobPrototype.CanBeAntag && antagBlocked.Contains(session)) ||
                (whitelist != null && !whitelist.Contains(job)) ||
                (blacklist != null && blacklist.Contains(job)) ||
                (roleBans != null && roleBans.Contains(job)))
            {
                continue;
            }

            candidates.Add(userId);
        }

        if (candidates.Count > 0)
        {
            player = _random.Pick(candidates);
            return true;
        }

        player = default;
        return false;
    }

    /// <summary>
    /// Builds the inverse candidate index used by the player-first maximum-slot phase.
    /// </summary>
    private static Dictionary<NetUserId, Dictionary<JobPriority, List<ProtoId<JobPrototype>>>> GetPlayerCandidates(
        Dictionary<ProtoId<JobPrototype>, Dictionary<JobPriority, HashSet<NetUserId>>> jobCandidates)
    {
        var output = new Dictionary<NetUserId, Dictionary<JobPriority, List<ProtoId<JobPrototype>>>>();
        foreach (var (job, priorities) in jobCandidates)
        {
            foreach (var (priority, players) in priorities)
            {
                foreach (var player in players)
                {
                    if (!output.TryGetValue(player, out var playerPriorities))
                    {
                        playerPriorities = new Dictionary<JobPriority, List<ProtoId<JobPrototype>>>();
                        output.Add(player, playerPriorities);
                    }

                    if (!playerPriorities.TryGetValue(priority, out var jobs))
                    {
                        jobs = new List<ProtoId<JobPrototype>>();
                        playerPriorities.Add(priority, jobs);
                    }

                    jobs.Add(job);
                }
            }
        }

        return output;
    }
}
