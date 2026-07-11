using System.Linq;
using System.Numerics;
using Content.Server._CMU14.Ops.ThirdParty;
using Content.Server._CMU14.Threats;
using Content.Server._RMC14.Announce;
using Content.Server._RMC14.Xenonids.Hive;
using Content.Server.Administration.Managers;
using Content.Server.Administration.Systems;
using Content.Server.AU14.Allegiance;
using Content.Server.AU14.Origin;
using Content.Server.AU14.Round;
using Content.Server.AU14.Scenario;
using Content.Server.GameTicking.Events;
using Content.Server.Spawners.Components;
using Content.Server.Station.Components;
using Content.Shared._CMU14.Threats;
using Content.Shared._RMC14.Rules;
using Content.Shared.AU14.util;
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
        [Dependency] private ThirdPartySystem _thirdParty = default!;
        [Dependency] private AllegianceSystem _allegianceSystem = default!;
        [Dependency] private OriginSystem _originSystem = default!;

        public static readonly EntProtoId ObserverPrototypeName = "MobObserver";
        public static readonly EntProtoId MentorObserverPrototypeName = "MentorObserver";
        public static readonly EntProtoId AdminObserverPrototypeName = "RMCAdminObserver";

        private const string AuThreatLeaderJob = "AU14JobThreatLeader";
        private const string AuThreatMemberJob = "AU14JobThreatMember";
        private const string AuThirdPartyLeaderJob = "AU14JobThirdPartyLeader";
        private const string AuThirdPartyMemberJob = "AU14JobThirdPartyMember";

        // Mainly to avoid allocations.
        private readonly List<EntityCoordinates> _possiblePositions = new();

        /// <summary>
        ///     How many players have joined the round through normal methods.
        ///     Useful for game rules to look at. Doesn't count observers, people in lobby, etc.
        /// </summary>
        public int PlayersJoinedRoundNormally;

        private static AuAssignmentCounts CountAuAssignments(Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)> assignedJobs)
        {
            var noJob = 0;
            var threatLeaders = 0;
            var threatMembers = 0;
            var thirdPartyLeaders = 0;
            var thirdPartyMembers = 0;

            foreach ((NetUserId _, (ProtoId<JobPrototype>? job, EntityUid _)) in assignedJobs)
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

            return new(
                noJob,
                threatLeaders,
                threatMembers,
                thirdPartyLeaders,
                thirdPartyMembers);
        }

        /// <summary>
        ///     Determines which platoon a job belongs to based on its ID.
        ///     Returns null if the job doesn't belong to a specific platoon.
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

        private ScenarioPlanValidationRequest BuildScenarioPlanRuntimeRequest(string? presetId, int playerCount) => new(
            presetId ?? string.Empty,
            playerCount,
            _platoonSpawnRuleSystem.SelectedGovforPlatoon?.ID,
            _platoonSpawnRuleSystem.SelectedOpforPlatoon?.ID,
            _auRoundSystem.GetSelectedPlanetId(),
            _auRoundSystem.GetSelectedPlanet()?.MapId,
            _auRoundSystem.SelectedThreat?.ID,
            _auRoundSystem.GetSelectedGovforShip(),
            _auRoundSystem.GetSelectedOpforShip());

        private static bool ShouldGenerateScenarioPlanShadow(string? presetId)
            => presetId != null
                && (presetId.Equals("DistressSignal", StringComparison.OrdinalIgnoreCase)
                    || presetId.Equals("Insurgency", StringComparison.OrdinalIgnoreCase)
                    || presetId.Equals("ColonyFall", StringComparison.OrdinalIgnoreCase));

        /// <summary>
        ///     Resolves the correct character profile for a player based on allegiance.
        ///     If the player is ignoring allegiance or the job/platoon has no requirements, returns the selected profile.
        ///     If the selected profile doesn't match, searches other profiles.
        ///     Returns null if no matching profile is found and the player isn't ignoring allegiance.
        /// </summary>
        private HumanoidCharacterProfile? ResolveProfileForAllegiance(NetUserId userId,
            HumanoidCharacterProfile selectedProfile,
            string? jobId)
        {
            HumanoidCharacterProfile? FindMatchingProfile(Func<HumanoidCharacterProfile, bool> predicate)
            {
                if (!_prefsManager.TryGetCachedPreferences(userId, out PlayerPreferences? cachedPrefs))
                    return null;

                foreach ((int _, ICharacterProfile profile) in cachedPrefs.Characters)
                {
                    if (profile is HumanoidCharacterProfile humanoid && predicate(humanoid))
                        return humanoid;
                }

                return null;
            }

            // If player is ignoring allegiance, always use selected profile
            if (_allegianceSystem.IsIgnoringAllegiance(userId))
                return selectedProfile;

            JobPrototype? jobProto = null;
            if (jobId != null)
                _prototypeManager.TryIndex(jobId, out jobProto);

            bool MeetsJobRequirements(HumanoidCharacterProfile profile)
            {
                if (jobProto == null)
                    return true;

                return _allegianceSystem.DoesCharacterMeetJobAllegiance(profile, jobProto)
                    && _allegianceSystem.DoesCharacterMeetJobOrigin(profile, jobProto);
            }

            PlatoonPrototype? platoon = GetPlatoonForJob(jobId);

            // No platoon for this job = no allegiance restriction
            if (platoon == null)
                return MeetsJobRequirements(selectedProfile) ? selectedProfile : FindMatchingProfile(MeetsJobRequirements);

            // No allegiance set on the platoon = no restriction
            if (platoon.Allegiance == null)
                return MeetsJobRequirements(selectedProfile) ? selectedProfile : FindMatchingProfile(MeetsJobRequirements);

            // Job ignores allegiance
            if (jobProto is { IgnoreAllegiance: true })
                return MeetsJobRequirements(selectedProfile) ? selectedProfile : FindMatchingProfile(MeetsJobRequirements);

            // Check if the selected profile matches
            if (_allegianceSystem.IsAllegianceApplicableForPlatoon(selectedProfile, platoon, jobProto))
                return selectedProfile;

            // Selected doesn't match — search all character profiles
            if (!_prefsManager.TryGetCachedPreferences(userId, out PlayerPreferences? prefs))
                return null;

            HumanoidCharacterProfile? match = _allegianceSystem.FindApplicableCharacterForPlatoon(
                prefs.Characters,
                prefs.SelectedCharacterIndex,
                platoon,
                jobProto);

            return match ?? null;
        }

        private List<EntityUid> GetSpawnableStations()
        {
            var spawnableStations = new List<EntityUid>();
            EntityQueryEnumerator<StationJobsComponent, StationSpawningComponent> query
                = EntityQueryEnumerator<StationJobsComponent, StationSpawningComponent>();
            while (query.MoveNext(out EntityUid uid, out _, out _))
            {
                spawnableStations.Add(uid);
            }

            return spawnableStations;
        }

        private static Dictionary<NetUserId, HumanoidCharacterProfile> GetGamemodeAssignmentProfiles(
            Dictionary<NetUserId, HumanoidCharacterProfile> profiles, string? presetId)
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
            if (!IsGameRuleActive<CMDistressSignalRuleComponent>())
                _distressSignal.SetFriendlyHives(_distressSignal.TheHive);

            // Allow game rules to spawn players by themselves if needed. (For example, nuke ops or wizard)
            RaiseLocalEvent(new RulePlayerSpawningEvent(readyPlayers, profiles, force));

            var readySessions = new Dictionary<NetUserId, ICommonSession>(readyPlayers.Count);
            var playerNetIds = new HashSet<NetUserId>(readyPlayers.Count);
            foreach (ICommonSession player in readyPlayers)
            {
                readySessions[player.UserId] = player;
                playerNetIds.Add(player.UserId);
            }

            // RulePlayerSpawning feeds a readonlydictionary of profiles.
            // We need to take these players out of the pool of players available as they've been used.
            if (readyPlayers.Count != profiles.Count)
            {
                var toRemove = new RemQueue<NetUserId>();

                foreach ((NetUserId player, HumanoidCharacterProfile _) in profiles)
                {
                    if (playerNetIds.Contains(player))
                        continue;

                    toRemove.Add(player);
                }

                foreach (NetUserId player in toRemove)
                {
                    profiles.Remove(player);
                }
            }

            string? presetId = CurrentPreset?.ID ?? Preset?.ID ?? _auRoundSystem.SelectedPreset?.ID;
            Dictionary<NetUserId, HumanoidCharacterProfile> assignmentProfiles
                = GameTicker.GetGamemodeAssignmentProfiles(profiles, presetId);

            _threatVoteSystem.ClearRoundJoinBlocks();
            bool usesPostRoundstartThreatVote = _auRoundSystem.UsesPostRoundstartThreatVote();
            var threatVotePrepared = false;

            _sawmill.Debug(
                $"[RoundStart] SpawnPlayers begin: preset={presetId ?? "null"}, readyPlayers={readyPlayers.Count
                }, profiles={profiles.Count}, assignmentProfiles={assignmentProfiles.Count}, defaultMap={DefaultMap
                }, planet={_auRoundSystem.GetSelectedPlanet()?.MapId ?? "null"}, selectedThreat={
                    _auRoundSystem.SelectedThreat?.ID ?? "null"}, postRoundstartThreatVote={usesPostRoundstartThreatVote
                    }");

            if (usesPostRoundstartThreatVote)
            {
                _sawmill.Debug("[RoundStart] Preparing post-roundstart threat vote.");
                try
                {
                    threatVotePrepared = _threatVoteSystem.TryPrepareThreatVote(assignmentProfiles, DefaultMap);
                    _sawmill.Debug($"[RoundStart] TryPrepareThreatVote result={threatVotePrepared}.");
                }
                catch (Exception threatVoteEx)
                {
                    Log.Error($"TryPrepareThreatVote threw - round will continue without a threat vote. {threatVoteEx}");
                    _threatVoteSystem.ClearRoundJoinBlocks();
                    _auJobSelectionSystem.ForcedJobAssignments.Clear();
                }
            }
            else
            {
                _sawmill.Debug("[RoundStart] Assigning immediate threat jobs.");
                _auJobSelectionSystem.AssignThreatAndThirdPartyJobs(assignmentProfiles);
            }

            if (GameTicker.ShouldGenerateScenarioPlanShadow(presetId))
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
                    _chatManager.DispatchServerAnnouncement(
                        Loc.GetString("au14-scenario-plan-threw-announcement",
                            ("preset", presetId ?? "<unknown>")),
                        Color.Red);
                }
            }

            List<EntityUid> spawnableStations = GetSpawnableStations();
            _sawmill.Debug($"[RoundStart] Found {spawnableStations.Count} spawnable station(s).");
            Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)> assignedJobs
                = _stationJobs.AssignJobs(assignmentProfiles, spawnableStations);
            if (_sawmill.Level <= LogLevel.Debug)
            {
                AuAssignmentCounts stationAssignmentCounts = GameTicker.CountAuAssignments(assignedJobs);
                _sawmill.Debug(
                    $"[RoundStart] Station job assignment complete: assigned={assignedJobs.Count}, noJob={
                        stationAssignmentCounts.NoJob}, threatLeaders={stationAssignmentCounts.ThreatLeaders
                        }, threatMembers={stationAssignmentCounts.ThreatMembers}, thirdPartyLeaders={
                            stationAssignmentCounts.ThirdPartyLeaders}, thirdPartyMembers={
                                stationAssignmentCounts.ThirdPartyMembers}");
            }

            // Defensive: any exception inside SpawnPlayers propagates to StartRound's
            // EXCEPTION_TOLERANCE catch (only enabled in Release/Tools builds), which calls
            // RestartRound() — making the round appear to "instantly restart at start" in
            // production. Wrap the threat spawn so a single subsystem can't take the round down.
            ThreatPrototype? selectedThreat = _auRoundSystem.SelectedThreat;
            switch (usesPostRoundstartThreatVote)
            {
                case false when selectedThreat != null:
                {
                    if (_sawmill.Level <= LogLevel.Debug)
                    {
                        AuAssignmentCounts beforeThreatCounts = GameTicker.CountAuAssignments(assignedJobs);

                        _sawmill.Debug(
                            $"[RoundStart] Starting immediate threat spawn for '{selectedThreat.ID}' on map {DefaultMap
                            }; assignedThreatLeaders={beforeThreatCounts.ThreatLeaders}, assignedThreatMembers={
                                beforeThreatCounts.ThreatMembers}.");
                    }

                    try
                    {
                        _threatSystem.SpawnThreatAtRoundStart(selectedThreat, DefaultMap, assignedJobs);

                        if (_sawmill.Level <= LogLevel.Debug)
                        {
                            AuAssignmentCounts afterThreatCounts = CountAuAssignments(assignedJobs);
                            _sawmill.Debug(
                                $"[RoundStart] Threat spawn returned for '{selectedThreat.ID}'; remainingThreatLeaders={
                                    afterThreatCounts.ThreatLeaders}, remainingThreatMembers={
                                        afterThreatCounts.ThreatMembers
                                    }.");
                        }

                        // Return unselected threat players to lobby so they can JIP
                        var threatLosers = new List<(NetUserId UserId, ICommonSession Session)>();
                        foreach ((NetUserId userId, (ProtoId<JobPrototype>? job, EntityUid _)) in assignedJobs)
                        {
                            if ((job != AuThreatLeaderJob && job != AuThreatMemberJob)
                                || !readySessions.TryGetValue(userId, out ICommonSession? session))
                                continue;

                            threatLosers.Add((userId, session));
                        }
                        _sawmill.Debug($"[RoundStart] Returning {threatLosers.Count} unselected threat player(s) to lobby.");
                        foreach ((NetUserId userId, ICommonSession session) in threatLosers)
                        {
                            assignedJobs.Remove(userId);
                            PlayerJoinLobby(session);
                            _chatManager.DispatchServerMessage(session,
                                Loc.GetString("au14-threat-not-selected-return-to-lobby"));
                        }
                    }
                    catch (Exception threatEx)
                    {
                        Log.Error($"SpawnThreatAtRoundStart threw — round will continue without threat spawn. {threatEx}");
                        int removed = ThreatSystem.RemoveThreatJobAssignments(assignedJobs);
                        if (removed > 0)
                        {
                            Log.Warning($"Removed {removed} "
                                + $"threat assignment(s) after threat spawning failed so overflow assignment can handle those players.");
                        }
                    }

                    break;
                }
                case true: _sawmill.Debug("[RoundStart] Threat spawn deferred until post-roundstart threat vote finishes."); break;
                default:   Log.Debug("SpawnThreatAtRoundStart debug — no threat selected, skipping threat spawn."); break;
            }

            _stationJobs.AssignOverflowJobs(ref assignedJobs, playerNetIds, assignmentProfiles, spawnableStations);
            if (_sawmill.Level <= LogLevel.Debug)
            {
                AuAssignmentCounts overflowAssignmentCounts = GameTicker.CountAuAssignments(assignedJobs);

                _sawmill.Debug(
                    $"[RoundStart] Overflow assignment complete: assigned={assignedJobs.Count}, noJob={
                        overflowAssignmentCounts.NoJob}, threatLeaders={overflowAssignmentCounts.ThreatLeaders
                        }, threatMembers={overflowAssignmentCounts.ThreatMembers}, thirdPartyLeaders={
                            overflowAssignmentCounts.ThirdPartyLeaders}, thirdPartyMembers={
                                overflowAssignmentCounts.ThirdPartyMembers}");
            }

            // Calculate extended access for stations.
            Dictionary<EntityUid, int> stationJobCounts = spawnableStations.ToDictionary(e => e, _ => 0);
            foreach ((NetUserId netUser, (ProtoId<JobPrototype>? job, EntityUid station)) in assignedJobs)
            {
                if (job == null)
                {
                    if (!readySessions.TryGetValue(netUser, out ICommonSession? playerSession))
                        continue;

                    var evNoJobs = new NoJobsAvailableSpawningEvent(
                        playerSession); // Used by gamerules to wipe their antag slot, if they got one
                    RaiseLocalEvent(evNoJobs);

                    _chatManager.DispatchServerMessage(playerSession,
                        Loc.GetString("job-not-available-wait-in-lobby"));
                }
                else
                    stationJobCounts[station] += 1;
            }

            _stationJobs.CalcExtendedAccess(stationJobCounts);

            // Spawn everybody in!
            foreach ((NetUserId player, (ProtoId<JobPrototype>? job, EntityUid station)) in assignedJobs)
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

                if (!readySessions.TryGetValue(player, out ICommonSession? playerSession))
                    continue;

                // Allegiance check: resolve the correct character profile for this player's job/platoon
                HumanoidCharacterProfile selectedProfile = profiles[player];
                HumanoidCharacterProfile? resolvedProfile = ResolveProfileForAllegiance(player, selectedProfile, job);

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

            switch (threatVotePrepared)
            {
                // Defensive: same rationale as the SpawnThreatAtRoundStart try/catch above. Without
                // this, an exception inside third-party spawning kills the entire round at start.
                case true:
                    _sawmill.Debug("[RoundStart] Starting prepared post-roundstart threat vote.");
                    try
                    {
                        if (_threatVoteSystem.StartPreparedThreatVote(assignedJobs))
                        {
                            _sawmill.Debug("[RoundStart] Prepared threat vote handled; threat and third-party spawn will continue from vote completion or single-candidate auto-selection.");
                        }
                        else
                        {
                            Log.Warning("Prepared threat vote could not start.");

                            _threatVoteSystem.ClearRoundJoinBlocks();
                            int removed = ThreatSystem.RemoveThreatJobAssignments(assignedJobs);
                            if (removed > 0)
                                Log.Warning($"Removed {removed} held threat assignment(s) after threat vote start failed.");
                        }
                    }
                    catch (Exception threatVoteEx)
                    {
                        Log.Error($"StartPreparedThreatVote threw - round will continue without a threat vote. {threatVoteEx}");

                        _threatVoteSystem.ClearRoundJoinBlocks();
                        int removed = ThreatSystem.RemoveThreatJobAssignments(assignedJobs);
                        if (removed > 0)
                            Log.Warning($"Removed {removed} held threat assignment(s) after threat vote start failed.");
                    }

                    break;
                case false:
                {
                    selectedThreat = _auRoundSystem.SelectedThreat;
                    if (selectedThreat != null)
                    {
                        if (_sawmill.Level <= LogLevel.Debug)
                        {
                            int roundstartThirdParties = _auRoundSystem.SelectedThirdParties.Count(party => party.RoundStart);

                            _sawmill.Debug(
                                $"[RoundStart] Starting third-party spawning for threat '{selectedThreat.ID
                                }'; selectedThirdParties={_auRoundSystem.SelectedThirdParties.Count
                                }, roundstartThirdParties={
                                    roundstartThirdParties}.");
                        }

                        try
                        {
                            _thirdParty.StartThirdPartySpawning(selectedThreat, assignedJobs);
                            _sawmill.Debug($"[RoundStart] StartThirdPartySpawning returned for threat '{selectedThreat.ID
                            }'.");
                        }
                        catch (Exception thirdPartyEx)
                        {
                            Log.Error($"StartThirdPartySpawning threw — round will continue without third-party spawn. {thirdPartyEx}");
                        }
                    }
                    else
                        Log.Debug("StartThirdPartySpawning debug — no threat selected, skipping third-party spawn.");

                    break;
                }
            }

            // Allow rules to add roles to players who have been spawned in. (For example, on-station traitors)
            var jobsAssignedList = new List<ICommonSession>(assignedJobs.Count);
            foreach ((NetUserId netUserId, (ProtoId<JobPrototype>? job, EntityUid _)) in assignedJobs)
            {
                if (threatVotePrepared && ThreatSystem.IsThreatJob(job))
                    continue;

                if (readySessions.TryGetValue(netUserId, out ICommonSession? session))
                    jobsAssignedList.Add(session);
            }

            ICommonSession[] jobsAssignedPlayers = jobsAssignedList.ToArray();
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

            HumanoidCharacterProfile character = GetPlayerProfile(player);

            HashSet<ProtoId<JobPrototype>>? jobBans = _banManager.GetJobBans(player.UserId);
            if (jobBans == null || (jobId != null && jobBans.Contains(jobId)))
                return;

            if (jobId != null)
            {
                var ev = new IsJobAllowedEvent(player, new(jobId));
                RaiseLocalEvent(ref ev);
                if (ev.Cancelled)
                    return;
            }

            // Allegiance check: resolve the correct character profile for this job/platoon
            HumanoidCharacterProfile? resolvedProfile = ResolveProfileForAllegiance(player.UserId, character, jobId);

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
            if (!_threatVoteSystem.IsRoundJoinBlocked(player.UserId))
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
                List<EntityUid> stations = GetSpawnableStations();
                _robustRandom.Shuffle(stations);
                station = stations.Count == 0 ? EntityUid.Invalid : stations[0];
            }

            if (lateJoin && DisallowLateJoin)
            {
                JoinAsObserver(player);
                return;
            }

            if (_randomizeCharacters)
            {
                string speciesId;
                string weightId = _cfg.GetCVar(CCVars.ICRandomSpeciesWeights);

                // If blank, choose a round start species.
                if (string.IsNullOrEmpty(weightId))
                {
                    IEnumerable<SpeciesPrototype> speciesPrototypes = _prototypeManager
                        .EnumeratePrototypes<SpeciesPrototype>();

                    List<ProtoId<SpeciesPrototype>> roundStart = (from proto in speciesPrototypes where proto.RoundStart select proto.ID).Select(dummy => (ProtoId<SpeciesPrototype>)dummy).ToList();

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

            // We raise this event to allow other systems to handle spawning this player themselves. (e.g. late-join wizard,
            // etc.)
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

            HashSet<ProtoId<JobPrototype>>? jobBans = _banManager.GetJobBans(player.UserId);
            if (jobBans != null)
                restrictedRoles.UnionWith(jobBans);

            // Pick best job best on prefs.
            string? presetId = CurrentPreset?.ID ?? Preset?.ID;
            jobId ??= _stationJobs.PickBestAvailableJobWithPriority(station,
                character.GetJobPrioritiesForGamemode(presetId),
                true,
                restrictedRoles);

            // If no job available, stay in lobby, or if no lobby spawn as observer
            if (jobId is null)
            {
                if (!LobbyEnabled) JoinAsObserver(player);

                var evNoJobs = new NoJobsAvailableSpawningEvent(
                    player); // Used by gamerules to wipe their antag slot, if they got one
                RaiseLocalEvent(evNoJobs);

                _chatManager.DispatchServerMessage(player,
                    Loc.GetString("game-ticker-player-no-jobs-available-when-joining"));
                return;
            }

            PlayerJoinGame(player, silent);

            ContentPlayerData? data = player.ContentData();

            DebugTools.AssertNotNull(data);

            Entity<MindComponent> newMind = _mind.CreateMind(data.UserId, character.Name);
            _mind.SetUserId(newMind, data.UserId);

            var jobPrototype = _prototypeManager.Index<JobPrototype>(jobId);

            _playTimeTrackings.PlayerRolesChanged(player);

            EntityUid? mobMaybe = _stationSpawning.SpawnPlayerCharacterOnStation(station, jobId, character);
            DebugTools.AssertNotNull(mobMaybe);
            EntityUid mob = mobMaybe.Value;

            // Apply origin effects (components, accents, items)
            _originSystem.ApplyOrigin(mob, character);

            _mind.TransferTo(newMind, mob);

            _roles.MindAddJobRole(newMind, silent: silent, jobPrototype: jobId);
            string jobName = _jobs.MindTryGetJobName(newMind);
            _admin.UpdatePlayerList(player);

/*
            // Deadcode
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
                        false,
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
                        false);
                }
            }
*/

            // wtf?
            // if (player.UserId == new Guid("{e887eb93-f503-4b65-95b6-2f282c014192}")) AddComp<OwOAccentComponent>(mob);

            _stationJobs.TryAssignJob(station, jobPrototype, player.UserId);

            if (lateJoin)
            {
                _adminLogger.Add(LogType.LateJoin,
                    LogImpact.Medium,
                    $"Player {player.Name} late joined as {character.Name:characterName} on station {Name(station)
                        :stationName} with {ToPrettyString(mob):entity} as a {jobName:jobName}.");
            }
            else
            {
                _adminLogger.Add(LogType.RoundStartJoin,
                    LogImpact.Medium,
                    $"Player {player.Name} joined as {character.Name:characterName} on station {Name(station)
                        :stationName} with {ToPrettyString(mob):entity} as a {jobName:jobName}.");
            }

            // Make sure they're aware of extended access.
            if (Comp<StationJobsComponent>(station).ExtendedAccess
                && (jobPrototype.ExtendedAccess.Count > 0 || jobPrototype.ExtendedAccessGroups.Count > 0))
                _chatManager.DispatchServerMessage(player, Loc.GetString("job-greet-crew-shortages"));

            if (!silent && TryComp(station, out MetaDataComponent? metaData))
            {
                _chatManager.DispatchServerMessage(player,
                    Loc.GetString("job-greet-station-name", ("stationName", metaData.EntityName)));
            }

            if (_distressSignal.SelectedPlanetMapName != null)
            {
                _chatManager.DispatchServerMessage(player,
                    Loc.GetString("job-greet-planet-name", ("planetName", _distressSignal.SelectedPlanetMapName)));
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
        ///     Makes a player join into the game and spawn on a station.
        /// </summary>
        /// <param name="player" >The player joining</param>
        /// <param name="station" >The station they're spawning on</param>
        /// <param name="jobId" >An optional job for them to spawn as</param>
        /// <param name="silent" >Whether the player should be greeted upon joining</param>
        public void MakeJoinGame(ICommonSession player, EntityUid station, string? jobId = null, bool silent = false)
        {
            if (!_playerGameStatuses.ContainsKey(player.UserId))
                return;

            if (!_userDb.IsLoadComplete(player))
                return;

            SpawnPlayer(player, station, jobId, silent: silent);
        }

        /// <summary>
        ///     Causes the given player to join the current game as observer ghost. See also <see cref="SpawnObserver" />
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
        ///     Spawns an observer ghost and attaches the given player to it. If the player does not yet have a mind, the
        ///     player is given a new mind with the observer role. Otherwise, the current mind is transferred to the ghost.
        /// </summary>
        public void SpawnObserver(ICommonSession player)
        {
            if (DummyTicker)
                return;

            var makeObserver = false;
            Entity<MindComponent?>? mind = player.GetMind();
            if (mind == null)
            {
                string name = GetPlayerProfile(player).Name;
                (EntityUid mindId, MindComponent mindComp) = _mind.CreateMind(player.UserId, name);
                mind = (mindId, mindComp);
                _mind.SetUserId(mind.Value, player.UserId);
                makeObserver = true;
            }

            EntityUid? ghost = _ghost.SpawnGhost(mind.Value);
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
            EntityQueryEnumerator<SpawnPointComponent, TransformComponent> spawnPointQuery
                = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
            while (spawnPointQuery.MoveNext(out EntityUid uid, out SpawnPointComponent? point,
                out TransformComponent? transform))
            {
                if (point.SpawnType != SpawnPointType.Observer
                    || TerminatingOrDeleted(uid)
                    || transform.MapUid == null
                    || TerminatingOrDeleted(transform.MapUid.Value))
                    continue;

                _possiblePositions.Add(transform.Coordinates);
            }

            EntityQuery<MetaDataComponent> metaQuery = GetEntityQuery<MetaDataComponent>();

            // Fallback to a random grid.
            if (_possiblePositions.Count == 0)
            {
                AllEntityQueryEnumerator<MapGridComponent> query = AllEntityQuery<MapGridComponent>();
                while (query.MoveNext(out EntityUid uid, out MapGridComponent? _))
                {
                    if (!metaQuery.TryGetComponent(uid, out MetaDataComponent? meta) || meta.EntityPaused
                        || TerminatingOrDeleted(uid)) continue;

                    _possiblePositions.Add(new(uid, Vector2.Zero));
                }
            }

            if (_possiblePositions.Count != 0)
            {
                // TODO: This is just here for the eye lerping.
                // Ideally engine would just spawn them on grid directly I guess? Right now grid traversal is handling it
                // during
                // update which means we need to add a hack somewhere around it.
                EntityCoordinates spawn = _robustRandom.Pick(_possiblePositions);
                var toMap = _transform.ToMapCoordinates(spawn);

                if (!_map.TryFindGridAt(toMap, out EntityUid gridUid, out _))
                    return spawn;

                TransformComponent gridXform = Transform(gridUid);
                return new(gridUid, Vector2.Transform(toMap.Position, _transform.GetInvWorldMatrix(gridXform)));
            }

            if (_map.MapExists(DefaultMap))
            {
                EntityUid mapUid = _map.GetMapOrInvalid(DefaultMap);
                if (!TerminatingOrDeleted(mapUid))
                    return new(mapUid, Vector2.Zero);
            }

            // Just pick a point at this point I guess.
            foreach (MapId map in _map.GetAllMapIds())
            {
                EntityUid mapUid = _map.GetMapOrInvalid(map);

                if (!metaQuery.TryGetComponent(mapUid, out MetaDataComponent? meta)
                    || meta.EntityPaused
                    || TerminatingOrDeleted(mapUid))
                    continue;

                return new(mapUid, Vector2.Zero);
            }

            // AAAAAAAAAAAAA
            // This should be an error, if it didn't cause tests to start erroring when they delete a player.
            _sawmill.Warning("Found no observer spawn points!");
            return EntityCoordinates.Invalid;
        }
#endregion

        private readonly record struct AuAssignmentCounts(
            int NoJob,
            int ThreatLeaders,
            int ThreatMembers,
            int ThirdPartyLeaders,
            int ThirdPartyMembers
        );
    }
}
