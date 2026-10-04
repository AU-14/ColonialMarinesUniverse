using System.Linq;
using Content.Server.Administration.Managers;
using Content.Server.Antag;
using Content.Server.AU14.Round;
using Content.Server.Players.PlayTimeTracking;
using Content.Server.Station.Components;
using Content.Server.Station.Events;
using Content.Shared.AU14;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.Station.Systems;

// Contains code for round-start spawning.
public sealed partial class StationJobsSystem
{
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IBanManager _banManager = default!;
    [Dependency] private AntagSelectionSystem _antag = default!;
    [Dependency] private AuJobSelectionSystem _auJobSelectionSystem = default!;
    [Dependency] private StationSystem _stationSystem = default!;

    private Dictionary<int, HashSet<string>> _jobsByWeight = default!;
    private List<int> _orderedWeights = default!;
    // Toggle used for ForceOnForce overflow assignment to alternate GOVFOR/OPFOR rifleman
    private bool _forceOnForceNextGovfor = true;

    /// <summary>
    /// Sets up some tables used by AssignJobs, including jobs sorted by their weights, and a list of weights in order from highest to lowest.
    /// </summary>
    private void InitializeRoundStart()
    {
        // Reset alternation each round so ForceOnForce starts consistently.
        _forceOnForceNextGovfor = true;
        _jobsByWeight = new Dictionary<int, HashSet<string>>();
        foreach (var job in _prototypeManager.EnumeratePrototypes<JobPrototype>())
        {
            if (!_jobsByWeight.ContainsKey(job.Weight))
                _jobsByWeight.Add(job.Weight, new HashSet<string>());

            _jobsByWeight[job.Weight].Add(job.ID);
        }

        _orderedWeights = _jobsByWeight.Keys.OrderByDescending(i => i).ToList();
    }

    /// <summary>
    /// Assigns jobs based on the given preferences and list of stations to assign for.
    /// This does NOT change the slots on the station, only figures out where each player should go.
    /// </summary>
    /// <param name="profiles">The profiles to use for selection.</param>
    /// <param name="stations">List of stations to assign for.</param>
    /// <param name="useRoundStartJobs">Whether or not to use the round-start jobs for the stations instead of their current jobs.</param>
    /// <returns>List of players and their assigned jobs.</returns>
    /// <remarks>
    /// You probably shouldn't use useRoundStartJobs mid-round if the station has been available to join,
    /// as there may end up being more round-start slots than available slots, which can cause weird behavior.
    /// A warning to all who enter ye cursed lands: This function is long and mildly incomprehensible. Best used without touching.
    /// </remarks>
    public Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)> AssignJobs(Dictionary<NetUserId, HumanoidCharacterProfile> profiles, IReadOnlyList<EntityUid> stations, bool useRoundStartJobs = true)
    {
        DebugTools.Assert(stations.Count > 0);

        InitializeRoundStart();

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
            // AuThirdPartySystem spawns the real entity. Falling back to a normal
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

        // The jobs left on the stations. This collection is modified as jobs are assigned to track what's available.
        var stationJobs = new Dictionary<EntityUid, Dictionary<ProtoId<JobPrototype>, int?>>();
        foreach (var station in stations)
        {
            if (useRoundStartJobs)
            {
                stationJobs.Add(station, GetRoundStartJobs(station).ToDictionary(x => x.Key, x => x.Value));
            }
            else
            {
                stationJobs.Add(station, GetJobs(station).ToDictionary(x => x.Key, x => x.Value));
            }
        }


        // We reuse this collection. It tracks what jobs we're currently trying to select players for.
        var currentlySelectingJobs = new Dictionary<EntityUid, Dictionary<ProtoId<JobPrototype>, int?>>(stations.Count);
        foreach (var station in stations)
        {
            currentlySelectingJobs.Add(station, new Dictionary<ProtoId<JobPrototype>, int?>());
        }

        // And these.
        // Tracks what players are available for a given job in the current iteration of selection.
        var jobPlayerOptions = new Dictionary<ProtoId<JobPrototype>, HashSet<NetUserId>>();
        // Tracks the total number of slots for the given stations in the current iteration of selection.
        var stationTotalSlots = new Dictionary<EntityUid, int>(stations.Count);
        // The share of the players each station gets in the current iteration of job selection.
        var stationShares = new Dictionary<EntityUid, int>(stations.Count);

        // Ok so the general algorithm:
        // We start with the highest weight jobs and work our way down. We filter jobs by weight when selecting as well.
        // Weight > Priority > Station.
        foreach (var weight in _orderedWeights)
        {
            for (var selectedPriority = JobPriority.High; selectedPriority > JobPriority.Never; selectedPriority--)
            {
                if (profiles.Count == 0)
                    goto endFunc;

                var candidates = GetPlayersJobCandidates(weight, selectedPriority, profiles);

                var optionsRemaining = 0;

                // Assigns a player to the given station, updating all the bookkeeping while at it.
                void AssignPlayer(NetUserId player, ProtoId<JobPrototype> job, EntityUid station)
                {
                    // Remove the player from all possible jobs as that's faster than actually checking what they have selected.
                    foreach (var (k, players) in jobPlayerOptions)
                    {
                        players.Remove(player);
                        if (players.Count == 0)
                            jobPlayerOptions.Remove(k);
                    }

                    stationJobs[station][job]--;
                    profiles.Remove(player);
                    assigned.Add(player, (job, station));

                    optionsRemaining--;
                }

                jobPlayerOptions.Clear(); // We reuse this collection.

                // Goes through every candidate, and adds them to jobPlayerOptions, so that the candidate players
                // have an index sorted by job. We use this (much) later when actually assigning people to randomly
                // pick from the list of candidates for the job.
                foreach (var (user, jobs) in candidates)
                {
                    foreach (var job in jobs)
                    {
                        if (!jobPlayerOptions.ContainsKey(job))
                            jobPlayerOptions.Add(job, new HashSet<NetUserId>());

                        jobPlayerOptions[job].Add(user);
                    }

                    optionsRemaining++;
                }

                // We reuse this collection, so clear it's children.
                foreach (var slots in currentlySelectingJobs)
                {
                    slots.Value.Clear();
                }

                // Go through every station..
                foreach (var station in stations)
                {
                    var slots = currentlySelectingJobs[station];

                    // Get all of the jobs in the selected weight category.
                    foreach (var (job, slot) in stationJobs[station])
                    {
                        if (_jobsByWeight[weight].Contains(job))
                            slots.Add(job, slot);
                    }
                }


                // Clear for reuse.
                stationTotalSlots.Clear();

                // Intentionally discounts the value of uncapped slots! They're only a single slot when deciding a station's share.
                foreach (var (station, jobs) in currentlySelectingJobs)
                {
                    stationTotalSlots.Add(
                        station,
                        (int)jobs.Values.Sum(x => x ?? 1)
                        );
                }

                var totalSlots = 0;

                // LINQ moment.
                // totalSlots = stationTotalSlots.Sum(x => x.Value);
                foreach (var (_, slot) in stationTotalSlots)
                {
                    totalSlots += slot;
                }

                if (totalSlots == 0)
                    continue; // No slots so just move to the next iteration.

                // Clear for reuse.
                stationShares.Clear();

                // How many players we've distributed so far. Used to grant any remaining slots if we have leftovers.
                var distributed = 0;

                // Goes through each station and figures out how many players we should give it for the current iteration.
                foreach (var station in stations)
                {
                    // Calculates the percent share then multiplies.
                    stationShares[station] = (int)Math.Floor(((float)stationTotalSlots[station] / totalSlots) * candidates.Count);
                    distributed += stationShares[station];
                }

                // Avoids the fair share problem where if there's two stations and one player neither gets one.
                // We do this by simply selecting a station randomly and giving it the remaining share(s).
                if (distributed < candidates.Count)
                {
                    var choice = _random.Pick(stations);
                    stationShares[choice] += candidates.Count - distributed;
                }

                // Actual meat, goes through each station and shakes the tree until everyone has a job.
                foreach (var station in stations)
                {
                    if (stationShares[station] == 0)
                        continue;

                    // The jobs we're selecting from for the current station.
                    var currStationSelectingJobs = currentlySelectingJobs[station];
                    // We only need this list because we need to go through this in a random order.
                    // Oh the misery, another allocation.
                    var allJobs = currStationSelectingJobs.Keys.ToList();
                    _random.Shuffle(allJobs);
                    // And iterates through all it's jobs in a random order until the count settles.
                    // No, AFAIK it cannot be done any saner than this. I hate "shaking" collections as much
                    // as you do but it's what seems to be the absolute best option here.
                    // It doesn't seem to show up on the chart, perf-wise, anyway, so it's likely fine.
                    int priorCount;
                    do
                    {
                        priorCount = stationShares[station];

                        foreach (var job in allJobs)
                        {
                            if (stationShares[station] == 0)
                                break;

                            if (currStationSelectingJobs[job] != null && currStationSelectingJobs[job] == 0)
                                continue; // Can't assign this job.

                            if (!jobPlayerOptions.ContainsKey(job))
                                continue;

                            // Picking players it finds that have the job set.
                            var player = _random.Pick(jobPlayerOptions[job]);
                            AssignPlayer(player, job, station);
                            stationShares[station]--;

                            if (currStationSelectingJobs[job] != null)
                                currStationSelectingJobs[job]--;

                            if (optionsRemaining == 0)
                                goto done;
                        }
                    } while (priorCount != stationShares[station]);
                }
                done: ;
            }
        }

        endFunc:
        return assigned;
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
            {
                continue;
            }

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
            foreach (var station in givenStations)
            {
                ProtoId<JobPrototype>? chosenOverflow = null;

                // Helper proto ids for common roles
                var protoColonist = new ProtoId<JobPrototype>("AU14JobCivilianColonist");
                var protoGovRifle = new ProtoId<JobPrototype>("AU14JobGOVFORSquadRifleman");
                var protoOpfRifle = new ProtoId<JobPrototype>("AU14JobOPFORSquadRifleman");

                var stationOverflows = GetOverflowJobs(station);

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
                        if (jobs.ContainsKey(protoGovRifle) || stationOverflows.Contains(protoGovRifle))
                            chosenOverflow = protoGovRifle;
                    }

                    // Fallback: any station that has the job
                    if (chosenOverflow == null)
                    {
                        if (stationOverflows.Contains(protoGovRifle))
                            chosenOverflow = protoGovRifle;
                        else
                        {
                            var jobs = GetJobs(station);
                            if (jobs.ContainsKey(protoGovRifle))
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
                        if (jobs.ContainsKey(wantProto) || stationOverflows.Contains(wantProto))
                            chosenOverflow = wantProto;
                    }
                    else
                    {
                        // Otherwise, if the station has the job in overflow or regular jobs, pick it as fallback.
                        if (stationOverflows.Contains(wantProto))
                            chosenOverflow = wantProto;
                        else
                        {
                            var jobs = GetJobs(station);
                            if (jobs.ContainsKey(wantProto))
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
    /// Gets all jobs that the input players have that match the given weight and priority.
    /// </summary>
    /// <param name="weight">Weight to find, if any.</param>
    /// <param name="selectedPriority">Priority to find, if any.</param>
    /// <param name="profiles">Profiles to look in.</param>
    /// <returns>Players and a list of their matching jobs.</returns>
    private Dictionary<NetUserId, List<string>> GetPlayersJobCandidates(int? weight, JobPriority? selectedPriority, Dictionary<NetUserId, HumanoidCharacterProfile> profiles)
    {
        var outputDict = new Dictionary<NetUserId, List<string>>(profiles.Count);

        foreach (var (player, profile) in profiles)
        {
            var roleBans = _banManager.GetJobBans(player);
            var antagBlocked = _antag.GetPreSelectedAntagSessions();
            var profileJobs = profile.JobPriorities.Keys.Select(k => new ProtoId<JobPrototype>(k)).ToList();
            var ev = new StationJobsGetCandidatesEvent(player, profileJobs);
            RaiseLocalEvent(ref ev);

            List<string>? availableJobs = null;

            foreach (var jobId in profileJobs)
            {
                var priority = profile.JobPriorities[jobId];

                if (!(priority == selectedPriority || selectedPriority is null))
                    continue;

                if (!_prototypeManager.TryIndex(jobId, out var job))
                    continue;

                if (!job.CanBeAntag && (!_player.TryGetSessionById(player, out var session) || antagBlocked.Contains(session)))
                    continue;

                if (weight is not null && job.Weight != weight.Value)
                    continue;

                if (!(roleBans == null || !roleBans.Contains(jobId)))
                    continue;

                availableJobs ??= new List<string>(profile.JobPriorities.Count);
                availableJobs.Add(jobId);
            }

            if (availableJobs is not null)
                outputDict.Add(player, availableJobs);
        }

        return outputDict;
    }
}
