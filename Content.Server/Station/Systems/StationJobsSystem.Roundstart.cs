using System.Linq;
using Content.Server.Administration.Managers;
using Content.Server.Antag;
using Content.Server.CMU14.Round;
using Content.Server.Station.Components;
using Content.Server.Station.Events;
using Content.Shared.CMU14;
using Content.Shared.Preferences;
using Content.Shared.Roles;
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
    /// Allocation considers High, Medium, then Low preferences across all stations.
    /// Within each preference level, minimum roles are filled first in station order, using station job weights.
    /// Remaining slots use random player order. Never roles stay unassigned even when minimum staffing is unmet.
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
            if (!profiles.TryGetValue(player, out var profile) ||
                !profile.JobPriorities.TryGetValue(jobId, out var priority) || priority <= JobPriority.Never)
            {
                continue;
            }
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
        // The minimum phase selects players for a job, and the maximum phase selects jobs for a player.
        var jobCandidates = GetJobCandidates(profiles);
        var playerCandidates = GetPlayerCandidates(jobCandidates);

        var stationRequiredJobs = stationMinimumJobs.ToDictionary(
            pair => pair.Key,
            pair => pair.Value
                .Where(x => x.Value is > 0)
                .OrderByDescending(x => GetJobWeight(pair.Key, ProtoMan.Index(x.Key)))
                .ThenBy(x => x.Key.Id)
                .Select(x => x.Key)
                .ToList());

        // Finish each preference level across every station before considering a lower preference.
        // Staffing requirements and job weights only take precedence within the same preference level.
        for (var priority = JobPriority.High; priority > JobPriority.Never; priority--)
        {
            foreach (var station in stations)
            {
                foreach (var job in stationRequiredJobs[station])
                {
                    while (stationMinimumJobs[station][job] is > 0 && profiles.Count > 0)
                    {
                        if (stationJobs[station][job] is <= 0 ||
                            !TryPickCandidate(job, priority, jobCandidates, out var player))
                        {
                            break;
                        }

                        AssignPlayer(player, job, station, stationJobs, stationMinimumJobs,
                            jobCandidates, playerCandidates, profiles, assigned);
                    }
                }
            }

            // Optional slots compete at the same preference level as minimum slots.
            foreach (var station in stations)
            {
                var players = profiles.Keys.ToList();
                _random.Shuffle(players);

                foreach (var player in players)
                {
                    if (TryPickJob(player, station, priority, stationJobs, playerCandidates, out var job))
                    {
                        AssignPlayer(player, job, station, stationJobs, stationMinimumJobs,
                            jobCandidates, playerCandidates, profiles, assigned);
                    }
                }
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
        JobPriority priority,
        Dictionary<ProtoId<JobPrototype>, Dictionary<JobPriority, HashSet<NetUserId>>> jobCandidates,
        out NetUserId player)
    {
        if (!jobCandidates.TryGetValue(job, out var candidates) ||
            !candidates.TryGetValue(priority, out var players) || players.Count == 0)
        {
            player = default;
            return false;
        }

        player = _random.Pick(players);
        return true;
    }

    private bool TryPickJob(
        NetUserId player,
        EntityUid station,
        JobPriority priority,
        Dictionary<EntityUid, Dictionary<ProtoId<JobPrototype>, int?>> stationJobs,
        Dictionary<NetUserId, Dictionary<JobPriority, List<ProtoId<JobPrototype>>>> playerCandidates,
        out ProtoId<JobPrototype> job)
    {
        if (!playerCandidates.TryGetValue(player, out var candidates) ||
            !candidates.TryGetValue(priority, out var jobs))
        {
            job = default;
            return false;
        }

        var availableJobs = jobs
            .Where(jobId => stationJobs[station].TryGetValue(jobId, out var slots) && slots is null or > 0)
            .ToList();
        if (availableJobs.Count == 0)
        {
            job = default;
            return false;
        }

        job = _random.Pick(availableJobs);
        return true;
    }

    private void AssignPlayer(
        NetUserId player,
        ProtoId<JobPrototype> job,
        EntityUid station,
        Dictionary<EntityUid, Dictionary<ProtoId<JobPrototype>, int?>> stationJobs,
        Dictionary<EntityUid, Dictionary<ProtoId<JobPrototype>, int?>> stationMinimumJobs,
        Dictionary<ProtoId<JobPrototype>, Dictionary<JobPriority, HashSet<NetUserId>>> jobCandidates,
        Dictionary<NetUserId, Dictionary<JobPriority, List<ProtoId<JobPrototype>>>> playerCandidates,
        Dictionary<NetUserId, HumanoidCharacterProfile> profiles,
        Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)> assigned)
    {
        if (stationJobs[station][job] is { } slots)
            stationJobs[station][job] = slots - 1;

        if (stationMinimumJobs[station].TryGetValue(job, out var minimum) && minimum is > 0)
            stationMinimumJobs[station][job] = minimum - 1;

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

        // Overflow opt-in does not override Never for individual jobs.
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
            var allowedJobs = profile.JobPriorities
                .Where(preference => preference.Value > JobPriority.Never &&
                    (bannedRoles == null || !bannedRoles.Contains(preference.Key.Id)))
                .Select(preference => preference.Key)
                .ToHashSet();
            foreach (var station in givenStations)
            {
                ProtoId<JobPrototype>? chosenOverflow = null;

                // Helper proto ids for common roles
                var protoColonist = new ProtoId<JobPrototype>("AU14JobCivilianColonist");
                var protoGovRifle = new ProtoId<JobPrototype>("AU14JobGOVFORSquadRifleman");
                var protoOpfRifle = new ProtoId<JobPrototype>("AU14JobOPFORSquadRifleman");

                var stationOverflows = GetOverflowJobs(station)
                    .Where(allowedJobs.Contains)
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
                        if (allowedJobs.Contains(protoGovRifle) &&
                            (jobs.ContainsKey(protoGovRifle) || stationOverflows.Contains(protoGovRifle)))
                            chosenOverflow = protoGovRifle;
                    }

                    // Fallback: any station that has the job
                    if (chosenOverflow == null)
                    {
                        if (allowedJobs.Contains(protoGovRifle) &&
                            stationOverflows.Contains(protoGovRifle))
                            chosenOverflow = protoGovRifle;
                        else
                        {
                            var jobs = GetJobs(station);
                            if (allowedJobs.Contains(protoGovRifle) &&
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
                        if (allowedJobs.Contains(wantProto) &&
                            (jobs.ContainsKey(wantProto) || stationOverflows.Contains(wantProto)))
                            chosenOverflow = wantProto;
                    }
                    else
                    {
                        // Otherwise, if the station has the job in overflow or regular jobs, pick it as fallback.
                        if (allowedJobs.Contains(wantProto) &&
                            stationOverflows.Contains(wantProto))
                            chosenOverflow = wantProto;
                        else
                        {
                            var jobs = GetJobs(station);
                            if (allowedJobs.Contains(wantProto) &&
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
