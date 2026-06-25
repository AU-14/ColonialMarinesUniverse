using System.Globalization;
using System.Linq;
using System.Numerics;
using Content.Server._CMU14.Ops.ThirdParty;
using Content.Server._CMU14.Threats;
using Content.Server.Administration.Managers;
using Content.Server.Administration.Systems;
using Content.Server.GameTicking.Events;
using Content.Server.Spawners.Components;
using Content.Server.Speech.Components;
using Content.Server.Station.Components;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Mind;
using Content.Shared.Players;
using Content.Shared.Preferences;
using Content.Shared.Random;
using Content.Shared.Random.Helpers;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;
using Content.Server._RMC14.Announce;
using Content.Server._RMC14.Xenonids.Hive;
using Content.Server.AU14.Round;
using Content.Server.AU14.Scenario;
using Content.Shared.AU14.util;

namespace Content.Server.GameTicking
{
    public sealed partial class GameTicker
    {
        [Dependency] private XenoHiveSystem _hive = default!;

        [Dependency] private IAdminManager _adminManager = default!;
        [Dependency] private SharedJobSystem _jobs = default!;
        [Dependency] private AdminSystem _admin = default!;
        [Dependency] private MarinePresenceAnnounceSystem _marinePresenceAnnounce = default!;
        [Dependency] private AuJobSelectionSystem _auJobSelectionSystem = default!;
        [Dependency] private ScenarioPlanSystem _scenarioPlan = default!;
        [Dependency] private ThreatSystem _threatSystem = default!;
        [Dependency] private ThreatVoteSystem _threatVoteSystem = default!;
        [Dependency] private ThirdPartySystem _ThirdParty = default!;
        [Dependency] private Content.Server.AU14.Allegiance.AllegianceSystem _allegianceSystem = default!;
        [Dependency] private Content.Server.AU14.Origin.OriginSystem _originSystem = default!;

        private const string AuThreatLeaderJob = "AU14JobThreatLeader";
        private const string AuThreatMemberJob = "AU14JobThreatMember";
        private const string AuThirdPartyLeaderJob = "AU14JobThirdPartyLeader";
        private const string AuThirdPartyMemberJob = "AU14JobThirdPartyMember";

        public static readonly EntProtoId ObserverPrototypeName = "MobObserver";
        public static readonly EntProtoId AdminObserverPrototypeName = "RMCAdminObserver";

        private readonly record struct AuAssignmentCounts(
            int NoJob,
            int ThreatLeaders,
            int ThreatMembers,
            int ThirdPartyLeaders,
            int ThirdPartyMembers);

        private static AuAssignmentCounts CountAuAssignments(
            Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)> assignedJobs)
        {
            var noJob = 0;
            var threatLeaders = 0;
            var threatMembers = 0;
            var thirdPartyLeaders = 0;
            var thirdPartyMembers = 0;

            foreach (var (_, (job, _)) in assignedJobs)
            {
                if (job == null)
                    noJob++;
                else if (job == AuThreatLeaderJob)
                    threatLeaders++;
                else if (job == AuThreatMemberJob)
                    threatMembers++;
                else if (job == AuThirdPartyLeaderJob)
                    thirdPartyLeaders++;
                else if (job == AuThirdPartyMemberJob)
                    thirdPartyMembers++;
            }

            return new AuAssignmentCounts(
                noJob,
                threatLeaders,
                threatMembers,
                thirdPartyLeaders,
                thirdPartyMembers);
        }

        /// <summary>
        /// Determines which platoon a job belongs to based on its ID.
        /// Returns null if the job doesn't belong to a specific platoon.
        /// </summary>
        private PlatoonPrototype? GetPlatoonForJob(string? jobId)
        {
            if (string.IsNullOrEmpty(jobId))
                return null;

            if (jobId.Contains("GOVFOR", StringComparison.OrdinalIgnoreCase))
                return _platoonSpawnRuleSystem.SelectedGovforPlatoon;

            if (jobId.Contains("OPFOR", StringComparison.OrdinalIgnoreCase))
                return _platoonSpawnRuleSystem.SelectedOpforPlatoon;

            return null;
        }

        private ScenarioPlanValidationRequest BuildScenarioPlanRuntimeRequest(string? presetId, int playerCount)
        {
            return new ScenarioPlanValidationRequest(
                presetId ?? string.Empty,
                playerCount,
                _platoonSpawnRuleSystem.SelectedGovforPlatoon?.ID,
                _platoonSpawnRuleSystem.SelectedOpforPlatoon?.ID,
                _auRoundSystem.GetSelectedPlanetId(),
                _auRoundSystem.GetSelectedPlanet()?.MapId,
                _auRoundSystem.SelectedThreat?.ID,
                _auRoundSystem.GetSelectedGovforShip(),
                _auRoundSystem.GetSelectedOpforShip());
        }

        private static bool ShouldGenerateScenarioPlanShadow(string? presetId)
        {
            return presetId != null &&
                   (presetId.Equals("DistressSignal", StringComparison.OrdinalIgnoreCase) ||
                    presetId.Equals("Insurgency", StringComparison.OrdinalIgnoreCase) ||
                    presetId.Equals("ColonyFall", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Resolves the correct character profile for a player based on allegiance.
        /// If the player is ignoring allegiance or the job/platoon has no requirements, returns the selected profile.
        /// If the selected profile doesn't match, searches other profiles.
        /// Returns null if no matching profile is found and the player isn't ignoring allegiance.
        /// </summary>
        private HumanoidCharacterProfile? ResolveProfileForAllegiance(
            NetUserId userId,
            HumanoidCharacterProfile selectedProfile,
            string? jobId)
        {
            HumanoidCharacterProfile? FindMatchingProfile(Func<HumanoidCharacterProfile, bool> predicate)
            {
                if (_prefsManager.TryGetCachedPreferences(userId, out var prefs))
                {
                    foreach (var (_, profile) in prefs.Characters)
                    {
                        if (profile is HumanoidCharacterProfile humanoid && predicate(humanoid))
                            return humanoid;
                    }
                }

                return null;
            }

            // If player is ignoring allegiance, always use selected profile
            if (_allegianceSystem.IsIgnoringAllegiance(userId))
                return selectedProfile;

            JobPrototype? jobProto = null;
            if (jobId != null)
                _prototypeManager.TryIndex<JobPrototype>(jobId, out jobProto);

            bool MeetsJobRequirements(HumanoidCharacterProfile profile)
            {
                if (jobProto == null)
                    return true;

                return _allegianceSystem.DoesCharacterMeetJobAllegiance(profile, jobProto)
                       && _allegianceSystem.DoesCharacterMeetJobOrigin(profile, jobProto);
            }

            var platoon = GetPlatoonForJob(jobId);

            // No platoon for this job = no allegiance restriction
            if (platoon == null)
            {
                if (MeetsJobRequirements(selectedProfile))
                    return selectedProfile;

                return FindMatchingProfile(MeetsJobRequirements);
            }

            // No allegiance set on the platoon = no restriction
            if (platoon.Allegiance == null)
            {
                if (MeetsJobRequirements(selectedProfile))
                    return selectedProfile;

                return FindMatchingProfile(MeetsJobRequirements);
            }

            // Job ignores allegiance
            if (jobProto is { IgnoreAllegiance: true })
            {
                if (MeetsJobRequirements(selectedProfile))
                    return selectedProfile;

                return FindMatchingProfile(MeetsJobRequirements);
            }

            // Check if the selected profile matches
            if (_allegianceSystem.IsAllegianceApplicableForPlatoon(selectedProfile, platoon, jobProto))
                return selectedProfile;

            // Selected doesn't match — search all character profiles
            if (_prefsManager.TryGetCachedPreferences(userId, out var prefs))
            {
                var match = _allegianceSystem.FindApplicableCharacterForPlatoon(
                    prefs.Characters,
                    prefs.SelectedCharacterIndex,
                    platoon,
                    jobProto);

                if (match != null)
                    return match;
            }

            // No matching profile found
            return null;
        }

        /// <summary>
        /// How many players have joined the round through normal methods.
        /// Useful for game rules to look at. Doesn't count observers, people in lobby, etc.
        /// </summary>
        public int PlayersJoinedRoundNormally;

        // Mainly to avoid allocations.
        private readonly List<EntityCoordinates> _possiblePositions = new();

        private List<EntityUid> GetSpawnableStations()
        {
            var spawnableStations = new List<EntityUid>();
            var query = EntityQueryEnumerator<StationJobsComponent, StationSpawningComponent>();
            while (query.MoveNext(out var uid, out _, out _))
            {
                spawnableStations.Add(uid);
            }

            return spawnableStations;
        }

        private static Dictionary<NetUserId, HumanoidCharacterProfile> GetGamemodeAssignmentProfiles(
            Dictionary<NetUserId, HumanoidCharacterProfile> profiles,
            string? presetId)
        {
            if (string.IsNullOrWhiteSpace(presetId))
                return profiles.ShallowClone();

            return profiles.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.WithJobPriorities(pair.Value.GetJobPrioritiesForGamemode(presetId)));
        }

        private void SpawnPlayers(List<ICommonSession> readyPlayers,
            Dictionary<NetUserId, HumanoidCharacterProfile> profiles,
            bool force)
        {
            _distressSignal.TheHive = _hive.CreateHive("xenonid hive", "CMXenoHive");

            // For presets without CMDistressSignalRule (e.g. AU14 DistressSignal), the planet map
            // has already been loaded by LoadMaps and CMDistressSignalRuleSystem.OnRulePlayerSpawning
            // won't run, so map-placed weeds/tunnels need their hive assigned here. CMDistressSignal
            // handles its own assignment after it loads the planet via SpawnXenoMap.
            if (!IsGameRuleActive<Content.Shared._RMC14.Rules.CMDistressSignalRuleComponent>())
                _distressSignal.SetFriendlyHives(_distressSignal.TheHive);

            // Allow game rules to spawn players by themselves if needed. (For example, nuke ops or wizard)
            RaiseLocalEvent(new RulePlayerSpawningEvent(readyPlayers, profiles, force));

            var readySessions = new Dictionary<NetUserId, ICommonSession>(readyPlayers.Count);
            var playerNetIds = new HashSet<NetUserId>(readyPlayers.Count);
            foreach (var player in readyPlayers)
            {
                readySessions[player.UserId] = player;
                playerNetIds.Add(player.UserId);
            }

            // RulePlayerSpawning feeds a readonlydictionary of profiles.
            // We need to take these players out of the pool of players available as they've been used.
            if (readyPlayers.Count != profiles.Count)
            {
                var toRemove = new RemQueue<NetUserId>();

                foreach (var (player, _) in profiles)
                {
                    if (playerNetIds.Contains(player))
                        continue;

                    toRemove.Add(player);
                }

                foreach (var player in toRemove)
                {
                    profiles.Remove(player);
                }
            }

            var presetId = CurrentPreset?.ID ?? Preset?.ID ?? _auRoundSystem.SelectedPreset?.ID;
            var assignmentProfiles = GetGamemodeAssignmentProfiles(profiles, presetId);

            this._threatVoteSystem.ClearRoundJoinBlocks();
            var usesPostRoundstartThreatVote = _auRoundSystem.UsesPostRoundstartThreatVote();
            var threatVotePrepared = false;
            _sawmill.Debug(
                $"[RoundStart] SpawnPlayers begin: preset={presetId ?? "null"}, readyPlayers={readyPlayers.Count}, profiles={profiles.Count}, assignmentProfiles={assignmentProfiles.Count}, defaultMap={DefaultMap}, planet={_auRoundSystem.GetSelectedPlanet()?.MapId ?? "null"}, selectedThreat={_auRoundSystem.SelectedThreat?.ID ?? "null"}, postRoundstartThreatVote={usesPostRoundstartThreatVote}");
            if (usesPostRoundstartThreatVote)
            {
                _sawmill.Debug("[RoundStart] Preparing post-roundstart threat vote.");
                try
                {
                    threatVotePrepared = this._threatVoteSystem.TryPrepareThreatVote(assignmentProfiles, DefaultMap);
                    _sawmill.Debug($"[RoundStart] TryPrepareThreatVote result={threatVotePrepared}.");
                }
                catch (Exception threatVoteEx)
                {
                    Log.Error($"TryPrepareThreatVote threw - round will continue without a threat vote. {threatVoteEx}");
                    this._threatVoteSystem.ClearRoundJoinBlocks();
                    _auJobSelectionSystem.ForcedJobAssignments.Clear();
                }
            }
            else
            {
                _sawmill.Debug("[RoundStart] Assigning immediate threat jobs.");
                _auJobSelectionSystem.AssignThreatAndThirdPartyJobs(assignmentProfiles);
            }

            if (ShouldGenerateScenarioPlanShadow(presetId))
            {
                try
                {
                    _scenarioPlan.GenerateShadowPlan(
                        BuildScenarioPlanRuntimeRequest(presetId, assignmentProfiles.Count),
                        usesPostRoundstartThreatVote
                            ? "RoundStartDeferredThreatVotePrepared"
                            : "RoundStartThreatAssignmentPrepared");
                }
                catch (Exception scenarioEx)
                {
                    Log.Error($"GenerateShadowPlan threw - round will continue without a shadow Scenario Plan. {scenarioEx}");
                }
            }

            var spawnableStations = GetSpawnableStations();
            _sawmill.Debug($"[RoundStart] Found {spawnableStations.Count} spawnable station(s).");
            var assignedJobs = _stationJobs.AssignJobs(assignmentProfiles, spawnableStations);
            if (_sawmill.Level <= Robust.Shared.Log.LogLevel.Debug)
            {
                var stationAssignmentCounts = CountAuAssignments(assignedJobs);
                _sawmill.Debug(
                    $"[RoundStart] Station job assignment complete: assigned={assignedJobs.Count}, noJob={stationAssignmentCounts.NoJob}, threatLeaders={stationAssignmentCounts.ThreatLeaders}, threatMembers={stationAssignmentCounts.ThreatMembers}, thirdPartyLeaders={stationAssignmentCounts.ThirdPartyLeaders}, thirdPartyMembers={stationAssignmentCounts.ThirdPartyMembers}");
            }

            // Defensive: any exception inside SpawnPlayers propagates to StartRound's
            // EXCEPTION_TOLERANCE catch (only enabled in Release/Tools builds), which calls
            // RestartRound() — making the round appear to "instantly restart at start" in
            // production. Wrap the threat spawn so a single subsystem can't take the round down.
            var selectedThreat = _auRoundSystem.SelectedThreat;
            if (!usesPostRoundstartThreatVote && selectedThreat != null)
            {
                if (_sawmill.Level <= Robust.Shared.Log.LogLevel.Debug)
                {
                    var beforeThreatCounts = CountAuAssignments(assignedJobs);
                    _sawmill.Debug(
                        $"[RoundStart] Starting immediate threat spawn for '{selectedThreat.ID}' on map {DefaultMap}; assignedThreatLeaders={beforeThreatCounts.ThreatLeaders}, assignedThreatMembers={beforeThreatCounts.ThreatMembers}.");
                }

                try
                {
                    this._threatSystem.SpawnThreatAtRoundStart(selectedThreat, DefaultMap, assignedJobs);
                    if (_sawmill.Level <= Robust.Shared.Log.LogLevel.Debug)
                    {
                        var afterThreatCounts = CountAuAssignments(assignedJobs);
                        _sawmill.Debug(
                            $"[RoundStart] Threat spawn returned for '{selectedThreat.ID}'; remainingThreatLeaders={afterThreatCounts.ThreatLeaders}, remainingThreatMembers={afterThreatCounts.ThreatMembers}.");
                    }
                }
                catch (Exception threatEx)
                {
                    Log.Error($"SpawnThreatAtRoundStart threw — round will continue without threat spawn. {threatEx}");
                    var removed = ThreatSystem.RemoveThreatJobAssignments(assignedJobs);
                    if (removed > 0)
                        Log.Warning($"Removed {removed} threat assignment(s) after threat spawning failed so overflow assignment can handle those players.");
                }
            }
            else if (usesPostRoundstartThreatVote)
            {
                _sawmill.Debug("[RoundStart] Threat spawn deferred until post-roundstart threat vote finishes.");
            }
            else
            {
                Log.Debug("SpawnThreatAtRoundStart debug — no threat selected, skipping threat spawn.");
            }

            _stationJobs.AssignOverflowJobs(ref assignedJobs, playerNetIds, assignmentProfiles, spawnableStations);
            if (_sawmill.Level <= Robust.Shared.Log.LogLevel.Debug)
            {
                var overflowAssignmentCounts = CountAuAssignments(assignedJobs);
                _sawmill.Debug(
                    $"[RoundStart] Overflow assignment complete: assigned={assignedJobs.Count}, noJob={overflowAssignmentCounts.NoJob}, threatLeaders={overflowAssignmentCounts.ThreatLeaders}, threatMembers={overflowAssignmentCounts.ThreatMembers}, thirdPartyLeaders={overflowAssignmentCounts.ThirdPartyLeaders}, thirdPartyMembers={overflowAssignmentCounts.ThirdPartyMembers}");
            }

            // Calculate extended access for stations.
            var stationJobCounts = spawnableStations.ToDictionary(e => e, _ => 0);
            foreach (var (netUser, (job, station)) in assignedJobs)
            {
                if (job == null)
                {
                    if (readySessions.TryGetValue(netUser, out var playerSession))
                    {
                        var evNoJobs = new NoJobsAvailableSpawningEvent(playerSession); // Used by gamerules to wipe their antag slot, if they got one
                        RaiseLocalEvent(evNoJobs);

                        _chatManager.DispatchServerMessage(playerSession, Loc.GetString("job-not-available-wait-in-lobby"));
                    }
                }
                else
                {
                    stationJobCounts[station] += 1;
                }
            }

            _stationJobs.CalcExtendedAccess(stationJobCounts);

            // Spawn everybody in!
            foreach (var (player, (job, station)) in assignedJobs)
            {
                // Threat jobs are intentionally skipped here — ThreatSystem.SpawnThreatAtRoundStart
                // (called above) already spawns those entities at threat markers and mind-transfers
                // the players to them.
                //
                // ThirdParty jobs deliberately fall through to the standard spawn path. Putting them
                // in this skip list (as PR #838 did) caused both distress and insurgency rounds to
                // restart at start: those players ended up bodyless, then SpawnThirdParty's
                // mind-transfer block (called below) hit GetMind→null→CreateMind→PlayerJoinGame on a
                // session still in lobby state, which threw, which propagated to StartRound's
                // EXCEPTION_TOLERANCE catch and called RestartRound(). Keep them in the standard
                // pipeline so they have a body even if SpawnThirdParty later no-ops.
                if (job != null && (job == "AU14JobThreatLeader" || job == "AU14JobThreatMember"))
                    continue;
                if (job == null)
                    continue;

                if (!readySessions.TryGetValue(player, out var playerSession))
                    continue;

                // Allegiance check: resolve the correct character profile for this player's job/platoon
                var selectedProfile = profiles[player];
                var resolvedProfile = ResolveProfileForAllegiance(player, selectedProfile, job);

                if (resolvedProfile == null)
                {
                    // No matching character for this platoon's allegiance — keep player in lobby
                    _chatManager.DispatchServerMessage(playerSession,
                        Loc.GetString("allegiance-no-matching-character"));
                    continue;
                }

                SpawnPlayer(playerSession, resolvedProfile, station, job, false);
            }

            RefreshLateJoinAllowed();

            // Defensive: same rationale as the SpawnThreatAtRoundStart try/catch above. Without
            // this, an exception inside third-party spawning kills the entire round at start.
            if (threatVotePrepared)
            {
                _sawmill.Debug("[RoundStart] Starting prepared post-roundstart threat vote.");
                try
                {
                    this._threatVoteSystem.StartPreparedThreatVote(assignedJobs);
                    _sawmill.Debug("[RoundStart] Prepared threat vote started; threat and third-party spawn will continue from vote completion.");
                }
                catch (Exception threatVoteEx)
                {
                    Log.Error($"StartPreparedThreatVote threw - round will continue without a threat vote. {threatVoteEx}");
                    this._threatVoteSystem.ClearRoundJoinBlocks();
                    var removed = ThreatSystem.RemoveThreatJobAssignments(assignedJobs);
                    if (removed > 0)
                        Log.Warning($"Removed {removed} held threat assignment(s) after threat vote start failed.");
                }
            }
            if (!threatVotePrepared)
            {
                selectedThreat = _auRoundSystem.SelectedThreat;
                if (selectedThreat != null)
                {
                    if (_sawmill.Level <= Robust.Shared.Log.LogLevel.Debug)
                    {
                        var roundstartThirdParties = 0;
                        foreach (var party in _auRoundSystem.SelectedThirdParties)
                        {
                            if (party.RoundStart)
                                roundstartThirdParties++;
                        }

                        _sawmill.Debug(
                            $"[RoundStart] Starting third-party spawning for threat '{selectedThreat.ID}'; selectedThirdParties={_auRoundSystem.SelectedThirdParties.Count}, roundstartThirdParties={roundstartThirdParties}.");
                    }

                    try
                    {
                        _ThirdParty.StartThirdPartySpawning(selectedThreat, assignedJobs);
                        _sawmill.Debug($"[RoundStart] StartThirdPartySpawning returned for threat '{selectedThreat.ID}'.");
                    }
                    catch (Exception thirdPartyEx)
                    {
                        Log.Error($"StartThirdPartySpawning threw — round will continue without third-party spawn. {thirdPartyEx}");
                    }
                }
                else
                {
                    Log.Debug("StartThirdPartySpawning debug — no threat selected, skipping third-party spawn.");
                }
            }

            // Allow rules to add roles to players who have been spawned in. (For example, on-station traitors)
            var jobsAssignedList = new List<ICommonSession>(assignedJobs.Count);
            foreach (var (netUserId, (job, _)) in assignedJobs)
            {
                if (threatVotePrepared && ThreatSystem.IsThreatJob(job))
                    continue;

                if (readySessions.TryGetValue(netUserId, out var session))
                    jobsAssignedList.Add(session);
            }
            var jobsAssignedPlayers = jobsAssignedList.ToArray();
            RaiseLocalEvent(new RulePlayerJobsAssignedEvent(
                jobsAssignedPlayers,
                profiles,
                force));
        }

        private void SpawnPlayer(ICommonSession player,
            EntityUid station,
            string? jobId = null,
            bool lateJoin = true,
            bool silent = false)
        {
            if (IsThreatVoteRoundJoinBlocked(player))
                return;

            var character = GetPlayerProfile(player);

            var jobBans = _banManager.GetJobBans(player.UserId);
            if (jobBans == null || jobId != null && jobBans.Contains(jobId))
                return;

            if (jobId != null)
            {
                var ev = new IsJobAllowedEvent(player, new ProtoId<JobPrototype>(jobId));
                RaiseLocalEvent(ref ev);
                if (ev.Cancelled)
                    return;
            }

            // Allegiance check: resolve the correct character profile for this job/platoon
            var resolvedProfile = ResolveProfileForAllegiance(player.UserId, character, jobId);

            if (resolvedProfile == null)
            {
                // No matching character for this platoon's allegiance — keep player in lobby
                _chatManager.DispatchServerMessage(player,
                    Loc.GetString("allegiance-no-matching-character"));
                return;
            }

            SpawnPlayer(player, resolvedProfile, station, jobId, lateJoin, silent);
        }

        private bool IsThreatVoteRoundJoinBlocked(ICommonSession player)
        {
            if (!this._threatVoteSystem.IsRoundJoinBlocked(player.UserId))
                return false;

            _chatManager.DispatchServerMessage(player, Loc.GetString("au14-threat-vote-round-join-blocked"));
            return true;
        }

        private void SpawnPlayer(ICommonSession player,
            HumanoidCharacterProfile character,
            EntityUid station,
            string? jobId = null,
            bool lateJoin = true,
            bool silent = false)
        {
            // Can't spawn players with a dummy ticker!
            if (DummyTicker)
                return;

            if (IsThreatVoteRoundJoinBlocked(player))
                return;

            if (station == EntityUid.Invalid)
            {
                var stations = GetSpawnableStations();
                _robustRandom.Shuffle(stations);
                if (stations.Count == 0)
                    station = EntityUid.Invalid;
                else
                    station = stations[0];
            }

            if (lateJoin && DisallowLateJoin)
            {
                JoinAsObserver(player);
                return;
            }

            string speciesId;
            if (_randomizeCharacters)
            {
                var weightId = _cfg.GetCVar(CCVars.ICRandomSpeciesWeights);

                // If blank, choose a round start species.
                if (string.IsNullOrEmpty(weightId))
                {
                    var roundStart = new List<ProtoId<SpeciesPrototype>>();

                    var speciesPrototypes = _prototypeManager.EnumeratePrototypes<SpeciesPrototype>();
                    foreach (var proto in speciesPrototypes)
                    {
                        if (proto.RoundStart)
                            roundStart.Add(proto.ID);
                    }

                    speciesId = roundStart.Count == 0
                        ? SharedHumanoidAppearanceSystem.DefaultSpecies
                        : _robustRandom.Pick(roundStart);
                }
                else
                {
                    var weights = _prototypeManager.Index<WeightedRandomSpeciesPrototype>(weightId);
                    speciesId = weights.Pick(_robustRandom);
                }

                character = HumanoidCharacterProfile.RandomWithSpecies(speciesId);
            }

            // We raise this event to allow other systems to handle spawning this player themselves. (e.g. late-join wizard, etc)
            var bev = new PlayerBeforeSpawnEvent(player, character, jobId, lateJoin, station);
            RaiseLocalEvent(bev);

            // Do nothing, something else has handled spawning this player for us!
            if (bev.Handled)
            {
                PlayerJoinGame(player, silent);
                return;
            }

            // Figure out job restrictions
            var restrictedRoles = new HashSet<ProtoId<JobPrototype>>();
            var ev = new GetDisallowedJobsEvent(player, restrictedRoles);
            RaiseLocalEvent(ref ev);

            var jobBans = _banManager.GetJobBans(player.UserId);
            if (jobBans != null)
                restrictedRoles.UnionWith(jobBans);

            // Pick best job best on prefs.
            var presetId = CurrentPreset?.ID ?? Preset?.ID;
            jobId ??= _stationJobs.PickBestAvailableJobWithPriority(station,
                character.GetJobPrioritiesForGamemode(presetId),
                true,
                restrictedRoles);
            // If no job available, stay in lobby, or if no lobby spawn as observer
            if (jobId is null)
            {
                if (!LobbyEnabled)
                {
                    JoinAsObserver(player);
                }

                var evNoJobs = new NoJobsAvailableSpawningEvent(player); // Used by gamerules to wipe their antag slot, if they got one
                RaiseLocalEvent(evNoJobs);

                _chatManager.DispatchServerMessage(player,
                    Loc.GetString("game-ticker-player-no-jobs-available-when-joining"));
                return;
            }

            PlayerJoinGame(player, silent);

            var data = player.ContentData();

            DebugTools.AssertNotNull(data);

            var newMind = _mind.CreateMind(data!.UserId, character.Name);
            _mind.SetUserId(newMind, data.UserId);

            var jobPrototype = _prototypeManager.Index<JobPrototype>(jobId);

            _playTimeTrackings.PlayerRolesChanged(player);

            var mobMaybe = _stationSpawning.SpawnPlayerCharacterOnStation(station, jobId, character);
            DebugTools.AssertNotNull(mobMaybe);
            var mob = mobMaybe!.Value;

            // Apply origin effects (components, accents, items)
            _originSystem.ApplyOrigin(mob, character);

            _mind.TransferTo(newMind, mob);

            _roles.MindAddJobRole(newMind, silent: silent, jobPrototype: jobId);
            var jobName = _jobs.MindTryGetJobName(newMind);
            _admin.UpdatePlayerList(player);

            if (lateJoin && !silent && false) // RMC14
            {
                if (jobPrototype.JoinNotifyCrew)
                {
                    _chatSystem.DispatchStationAnnouncement(station,
                        Loc.GetString("latejoin-arrival-announcement-special",
                            ("character", MetaData(mob).EntityName),
                            ("entity", mob),
                            ("job", CultureInfo.CurrentCulture.TextInfo.ToTitleCase(jobName))),
                        Loc.GetString("latejoin-arrival-sender"),
                        playDefaultSound: false,
                        colorOverride: Color.Gold);
                }
                else
                {
                    _chatSystem.DispatchStationAnnouncement(station,
                        Loc.GetString("latejoin-arrival-announcement",
                            ("character", MetaData(mob).EntityName),
                            ("entity", mob),
                            ("job", CultureInfo.CurrentCulture.TextInfo.ToTitleCase(jobName))),
                        Loc.GetString("latejoin-arrival-sender"),
                        playDefaultSound: false);
                }
            }

            if (player.UserId == new Guid("{e887eb93-f503-4b65-95b6-2f282c014192}"))
            {
                AddComp<OwOAccentComponent>(mob);
            }

            _stationJobs.TryAssignJob(station, jobPrototype, player.UserId);

            if (lateJoin)
            {
                _adminLogger.Add(LogType.LateJoin,
                    LogImpact.Medium,
                    $"Player {player.Name} late joined as {character.Name:characterName} on station {Name(station):stationName} with {ToPrettyString(mob):entity} as a {jobName:jobName}.");
            }
            else
            {
                _adminLogger.Add(LogType.RoundStartJoin,
                    LogImpact.Medium,
                    $"Player {player.Name} joined as {character.Name:characterName} on station {Name(station):stationName} with {ToPrettyString(mob):entity} as a {jobName:jobName}.");
            }

            // Make sure they're aware of extended access.
            if (Comp<StationJobsComponent>(station).ExtendedAccess
                && (jobPrototype.ExtendedAccess.Count > 0 || jobPrototype.ExtendedAccessGroups.Count > 0))
            {
                _chatManager.DispatchServerMessage(player, Loc.GetString("job-greet-crew-shortages"));
            }

            if (!silent && TryComp(station, out MetaDataComponent? metaData))
            {
                _chatManager.DispatchServerMessage(player,
                    Loc.GetString("job-greet-station-name", ("stationName", metaData.EntityName)));
            }

            if (_distressSignal?.SelectedPlanetMapName != null)
            {
                _chatManager.DispatchServerMessage(player,
                    Loc.GetString("job-greet-planet-name", ("planetName",_distressSignal.SelectedPlanetMapName)));
            }

            // We raise this event directed to the mob, but also broadcast it so game rules can do something now.
            PlayersJoinedRoundNormally++;
            var aev = new PlayerSpawnCompleteEvent(mob,
                player,
                jobId,
                lateJoin,
                silent,
                PlayersJoinedRoundNormally,
                station,
                character);
            RaiseLocalEvent(mob, aev, true);

            _marinePresenceAnnounce.AnnounceLateJoin(lateJoin, silent, mob, jobId, jobName, jobPrototype); // RMC14
        }

        public void Respawn(ICommonSession player)
        {
            _mind.WipeMind(player);
            _adminLogger.Add(LogType.Respawn, LogImpact.Medium, $"Player {player} was respawned.");

            if (LobbyEnabled)
                PlayerJoinLobby(player);
            else
                SpawnPlayer(player, EntityUid.Invalid);
        }

        /// <summary>
        /// Makes a player join into the game and spawn on a station.
        /// </summary>
        /// <param name="player">The player joining</param>
        /// <param name="station">The station they're spawning on</param>
        /// <param name="jobId">An optional job for them to spawn as</param>
        /// <param name="silent">Whether or not the player should be greeted upon joining</param>
        public void MakeJoinGame(ICommonSession player, EntityUid station, string? jobId = null, bool silent = false)
        {
            if (!_playerGameStatuses.ContainsKey(player.UserId))
                return;

            if (!_userDb.IsLoadComplete(player))
                return;

            SpawnPlayer(player, station, jobId, silent: silent);
        }

        /// <summary>
        /// Causes the given player to join the current game as observer ghost. See also <see cref="SpawnObserver"/>
        /// </summary>
        public void JoinAsObserver(ICommonSession player)
        {
            // Can't spawn players with a dummy ticker!
            if (DummyTicker)
                return;

            PlayerJoinGame(player);
            SpawnObserver(player);
        }

        /// <summary>
        /// Spawns an observer ghost and attaches the given player to it. If the player does not yet have a mind, the
        /// player is given a new mind with the observer role. Otherwise, the current mind is transferred to the ghost.
        /// </summary>
        public void SpawnObserver(ICommonSession player)
        {
            if (DummyTicker)
                return;

            var makeObserver = false;
            Entity<MindComponent?>? mind = player.GetMind();
            if (mind == null)
            {
                var name = GetPlayerProfile(player).Name;
                var (mindId, mindComp) = _mind.CreateMind(player.UserId, name);
                mind = (mindId, mindComp);
                _mind.SetUserId(mind.Value, player.UserId);
                makeObserver = true;
            }

            var ghost = _ghost.SpawnGhost(mind.Value);
            if (makeObserver)
                _roles.MindAddRole(mind.Value, "MindRoleObserver");

            _adminLogger.Add(LogType.LateJoin,
                LogImpact.Low,
                $"{player.Name} late joined the round as an Observer with {ToPrettyString(ghost):entity}.");
        }

        #region Spawn Points

        public EntityCoordinates GetObserverSpawnPoint()
        {
            _possiblePositions.Clear();
            var spawnPointQuery = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
            while (spawnPointQuery.MoveNext(out var uid, out var point, out var transform))
            {
                if (point.SpawnType != SpawnPointType.Observer
                   || TerminatingOrDeleted(uid)
                   || transform.MapUid == null
                   || TerminatingOrDeleted(transform.MapUid.Value))
                {
                    continue;
                }

                _possiblePositions.Add(transform.Coordinates);
            }

            var metaQuery = GetEntityQuery<MetaDataComponent>();

            // Fallback to a random grid.
            if (_possiblePositions.Count == 0)
            {
                var query = AllEntityQuery<MapGridComponent>();
                while (query.MoveNext(out var uid, out var grid))
                {
                    if (!metaQuery.TryGetComponent(uid, out var meta) || meta.EntityPaused || TerminatingOrDeleted(uid))
                    {
                        continue;
                    }

                    _possiblePositions.Add(new EntityCoordinates(uid, Vector2.Zero));
                }
            }

            if (_possiblePositions.Count != 0)
            {
                // TODO: This is just here for the eye lerping.
                // Ideally engine would just spawn them on grid directly I guess? Right now grid traversal is handling it during
                // update which means we need to add a hack somewhere around it.
                var spawn = _robustRandom.Pick(_possiblePositions);
                var toMap = _transform.ToMapCoordinates(spawn);

                if (_mapManager.TryFindGridAt(toMap, out var gridUid, out _))
                {
                    var gridXform = Transform(gridUid);

                    return new EntityCoordinates(gridUid, Vector2.Transform(toMap.Position, _transform.GetInvWorldMatrix(gridXform)));
                }

                return spawn;
            }

            if (_map.MapExists(DefaultMap))
            {
                var mapUid = _map.GetMapOrInvalid(DefaultMap);
                if (!TerminatingOrDeleted(mapUid))
                    return new EntityCoordinates(mapUid, Vector2.Zero);
            }

            // Just pick a point at this point I guess.
            foreach (var map in _map.GetAllMapIds())
            {
                var mapUid = _map.GetMapOrInvalid(map);

                if (!metaQuery.TryGetComponent(mapUid, out var meta)
                    || meta.EntityPaused
                    || TerminatingOrDeleted(mapUid))
                {
                    continue;
                }

                return new EntityCoordinates(mapUid, Vector2.Zero);
            }

            // AAAAAAAAAAAAA
            // This should be an error, if it didn't cause tests to start erroring when they delete a player.
            _sawmill.Warning("Found no observer spawn points!");
            return EntityCoordinates.Invalid;
        }

        #endregion
    }
}
