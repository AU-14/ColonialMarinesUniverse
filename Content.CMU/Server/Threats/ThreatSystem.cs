using System.Linq;
using Content.Server.AU14.Round;
using Content.Server.AU14.Scenario;
using Content.Server.GameTicking;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Mind.Commands;
using Content.Shared._CMU14.Threats;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Synth;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Construction.Nest;
using Content.Shared.AU14.Scenario;
using Content.Shared.AU14.util;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.Players;
using Content.Shared.Roles;
using Content.Shared.SSDIndicator;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using AbominationComponent = Content.Shared._CMU14.Threats.Mobs.Abomination.AbominationComponent;
using AbominationMimicComponent = Content.Shared._CMU14.Threats.Mobs.Abomination.AbominationMimicComponent;
using ApeComponent = Content.Shared._CMU14.Threats.Mobs.Ape.ApeComponent;
using TribalComponent = Content.Shared._CMU14.Threats.Mobs.Tribal.TribalComponent;

namespace Content.Server._CMU14.Threats;

public sealed partial class ThreatSystem : EntitySystem
{
    [Dependency] private AuRoundSystem _auRound = default!;
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private GhostRoleSystem _ghostRole = default!;
    [Dependency] private SharedMindSystem _mindSystem = default!;
    [Dependency] private NpcFactionSystem _npcFaction = default!;
    [Dependency] private PlatoonSpawnRuleSystem _platoonSpawnRule = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedRoleSystem _roles = default!;
    [Dependency] private ScenarioPlanSystem _scenarioPlan = default!;
    [Dependency] private ThreatVoteSystem _threatVote = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    private static readonly ProtoId<JobPrototype> ThreatLeaderJobId = new("AU14JobThreatLeader");
    private static readonly ProtoId<JobPrototype> ThreatMemberJobId = new("AU14JobThreatMember");
    private static readonly EntProtoId ThreatMindRoleId = new("MindRoleThreat");
    private static readonly ProtoId<NpcFactionPrototype> ThreatNpcFaction = new("THREAT");

    private static readonly IReadOnlyDictionary<string, JobScaleEntry> EmptyScaling
        = new Dictionary<string, JobScaleEntry>();

    private static readonly ThreatMarkerType[] ThreatMarkerTypes = Enum.GetValues<ThreatMarkerType>();

    private readonly List<PendingThreatForceSpawn> _pendingSpawns = new();

    private readonly ISawmill _sawmill = Logger.GetSawmill("au14.threat");
    public readonly ProtoId<NpcFactionPrototype> threatNPCFaction = "THREAT";

    internal IReadOnlyList<PendingThreatSpawnDebugView> PendingThreatSpawnsForDebug =>
        _pendingSpawns
            .Select(pending => new PendingThreatSpawnDebugView(pending.Threat.ID,
                pending.FireAt,
                pending.PlannedForce))
            .ToList();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        while (_pendingSpawns.Count > 0 && _timing.CurTime >= _pendingSpawns[0].FireAt)
        {
            PendingThreatForceSpawn pending = _pendingSpawns[0];
            _pendingSpawns.RemoveAt(0);

            try
            {
                ThreatSpawnExecutionResult resolvedForce = ExecuteSpawn(pending.Threat,
                    pending.MapId,
                    pending.AssignedJobs,
                    pending.VoteHeldPlayers,
                    pending.RequireObserverForVotePlayers,
                    pending.PlannedForce);
                if (resolvedForce.Spawned)
                    StartThreatWinConditions(pending.Threat, resolvedForce.ResolvedForce);
            }
            catch (Exception ex)
            {
                _sawmill.Error($"[ThreatSystem] Delayed threat spawn threw: {ex}");
                ReleaseVoteHeldPlayers(pending.VoteHeldPlayers, pending.Threat.ID, "delayed threat spawn threw", true);
            }
        }
    }

    internal static bool IsThreatJob(ProtoId<JobPrototype>? job)
        => job == ThreatLeaderJobId || job == ThreatMemberJobId;

    private static ThreatAssignmentCounts CountThreatAssignments(
        Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)> assignedJobs)
    {
        var leaders = 0;
        var members = 0;
        foreach ((NetUserId _, (ProtoId<JobPrototype>? job, EntityUid _)) in assignedJobs)
        {
            if (job == ThreatLeaderJobId)
                leaders++;
            else if (job == ThreatMemberJobId)
                members++;
        }

        return new(leaders, members);
    }

    private static string FormatSpawnBodies(IReadOnlyDictionary<string, int> bodies)
    {
        return bodies.Count == 0
            ? "none"
            : string.Join(", ", bodies.Select(pair => $"{pair.Key}={pair.Value}"));
    }

    internal static int RemoveThreatJobAssignments(
        Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)> assignedJobs,
        IReadOnlySet<NetUserId>? keepPlayers = null)
    {
        var removed = 0;
        foreach ((NetUserId player, (ProtoId<JobPrototype>? job, EntityUid _)) in assignedJobs.ToArray())
        {
            if (!ThreatSystem.IsThreatJob(job))
                continue;

            if (keepPlayers != null && keepPlayers.Contains(player))
                continue;

            assignedJobs.Remove(player);
            removed++;
        }

        return removed;
    }

    private void ReleaseVoteHeldPlayers(IReadOnlyCollection<NetUserId>? voteHeldPlayers,
        string threatId,
        string reason,
        bool respawn)
    {
        if (voteHeldPlayers == null || voteHeldPlayers.Count == 0)
            return;

        _threatVote.UnblockRoundJoinsForPlayers(voteHeldPlayers);

        if (!respawn)
        {
            _sawmill.Debug($"[ThreatSystem] Released {voteHeldPlayers.Count} held threat vote player(s) after {reason
            } for '{
                threatId}'.");

            return;
        }

        foreach (NetUserId playerId in voteHeldPlayers)
        {
            if (!_playerManager.TryGetSessionById(playerId, out ICommonSession? session)
                || session.Status == SessionStatus.Disconnected)
                continue;

            _sawmill.Info($"[ThreatSystem] Releasing held threat vote player {session.Name} ({playerId}) for '{threatId
            }' because {reason}; returning them to lobby.");
            _ticker.Respawn(session);
        }
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent ev)
    {
        if (ev.New != GameRunLevel.InRound)
            _pendingSpawns.Clear();
    }

    /// <summary>
    ///     In Colony Fall: schedules threat entity spawning and win condition activation after a random
    ///     delay via the game update loop. In all other presets: spawns and starts win conditions immediately.
    /// </summary>
    public void SpawnThreatAtRoundStart(ThreatPrototype threat,
        MapId mapId,
        Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)> assignedJobs)
    {
        if (threat == null)
        {
            _sawmill.Debug("[ThreatSystem] No threat selected for round start, skipping threat spawn.");

            return;
        }

        if (_sawmill.Level <= LogLevel.Debug)
        {
            ThreatAssignmentCounts assignmentCounts = ThreatSystem.CountThreatAssignments(assignedJobs);
            _sawmill.Debug($"[ThreatSystem] Roundstart threat spawn requested: threat={threat.ID}, map={mapId
            }, assignedJobs={
                assignedJobs.Count}, threatLeaders={assignmentCounts.Leaders}, threatMembers={
                    assignmentCounts.Members}, roundStartSpawn={threat.RoundStartSpawn}.");
        }

        bool isColonyFall = string.Equals(_auRound.SelectedPreset?.ID, "ColonyFall",
            StringComparison.OrdinalIgnoreCase);

        if (isColonyFall)
        {
            double delaySeconds = _random.NextDouble() * (threat.SpawnDelayMax - threat.SpawnDelayMin)
                + threat.SpawnDelayMin;
            _sawmill.Debug($"[ThreatSystem] Colony Fall threat '{threat.ID}' will spawn in {delaySeconds:F1}s.");
            SchedulePendingThreatSpawn(threat,
                mapId,
                assignedJobs,
                TimeSpan.FromSeconds(delaySeconds));
        }
        else
        {
            ThreatSpawnExecutionResult resolvedForce = ExecuteSpawn(threat, mapId, assignedJobs);
            if (resolvedForce.Spawned)
                StartThreatWinConditions(threat, resolvedForce.ResolvedForce);
        }
    }

    public void SpawnThreatFromVote(ThreatPrototype threat,
        MapId mapId,
        Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)> assignedJobs,
        IReadOnlyList<NetUserId> heldPlayers)
    {
        if (threat == null)
        {
            _sawmill.Debug("[ThreatSystem] No threat selected from vote, skipping threat spawn.");
            ReleaseVoteHeldPlayers(heldPlayers, "null", "no threat was selected", true);

            return;
        }

        if (_sawmill.Level <= LogLevel.Debug)
        {
            ThreatAssignmentCounts assignmentCounts = ThreatSystem.CountThreatAssignments(assignedJobs);
            _sawmill.Debug($"[ThreatSystem] Voted threat spawn requested: threat={threat.ID}, map={mapId
            }, assignedJobs={
                assignedJobs.Count}, heldPlayers={heldPlayers.Count}, threatLeaders={assignmentCounts.Leaders
                }, threatMembers={assignmentCounts.Members}, roundStartSpawn={threat.RoundStartSpawn}.");
        }

        bool isColonyFall = string.Equals(_auRound.SelectedPreset?.ID, "ColonyFall",
            StringComparison.OrdinalIgnoreCase);

        if (isColonyFall)
        {
            double delaySeconds = _random.NextDouble() * (threat.SpawnDelayMax - threat.SpawnDelayMin)
                + threat.SpawnDelayMin;
            _sawmill.Debug($"[ThreatSystem] Colony Fall voted threat '{threat.ID}' will spawn in {delaySeconds:F1}s.");
            SchedulePendingThreatSpawn(threat,
                mapId,
                assignedJobs,
                TimeSpan.FromSeconds(delaySeconds),
                heldPlayers,
                true);
        }
        else
        {
            ThreatSpawnExecutionResult resolvedForce = ExecuteSpawn(threat, mapId, assignedJobs, heldPlayers);
            if (resolvedForce.Spawned)
                StartThreatWinConditions(threat, resolvedForce.ResolvedForce);
        }
    }

    internal void SchedulePendingThreatSpawn(ThreatPrototype threat,
        MapId mapId,
        Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)> assignedJobs,
        TimeSpan delay,
        IReadOnlyList<NetUserId>? voteHeldPlayers = null,
        bool requireObserverForVotePlayers = false)
    {
        var pending = new PendingThreatForceSpawn
        {
            Threat = threat,
            MapId = mapId,
            AssignedJobs = assignedJobs,
            FireAt = _timing.CurTime + delay,
            VoteHeldPlayers = voteHeldPlayers?.ToList(),
            RequireObserverForVotePlayers = requireObserverForVotePlayers
        };

        TryResolvePendingThreatForce(pending);
        EnqueuePendingThreatSpawn(pending);
    }

    private void TryResolvePendingThreatForce(PendingThreatForceSpawn pending)
    {
        var coveredScenarioForce = false;
        try
        {
            ScenarioPlanValidationRequest request = BuildThreatSpawnScenarioPlanRequest(pending.Threat,
                pending.AssignedJobs,
                pending.VoteHeldPlayers);
            coveredScenarioForce = _scenarioPlan.HasMappedHostileRoundGroup(request.PresetId, pending.Threat.ID);
            if (_scenarioPlan.TryResolveSelectedThreatForce(request, out ResolvedThreatForcePlan? force,
                    out string diagnostic)
                && force != null)
            {
                pending.PlannedForce = force;
                _sawmill.Debug($"[ThreatSystem] Planned delayed threat force '{force.ForceId}' for threat '{
                    pending.Threat.ID}'.");

                return;
            }

            string backupDiagnostic = coveredScenarioForce
                ? "covered Round Groups do not use legacy marker lookup"
                : "delayed spawn will use live resolution or legacy markers";
            _sawmill.Warning($"[ThreatSystem] Could not resolve delayed threat Force Plan for '{pending.Threat.ID}'; {
                backupDiagnostic}. {diagnostic}");
        }
        catch (Exception ex)
        {
            string backupDiagnostic = coveredScenarioForce
                ? "covered Round Groups do not use legacy marker lookup"
                : "delayed spawn will use live resolution or legacy markers";
            _sawmill.Error($"[ThreatSystem] Delayed threat Force Plan resolution threw for '{pending.Threat.ID}'; {
                backupDiagnostic
            }. {ex}");
        }
    }

    private void EnqueuePendingThreatSpawn(PendingThreatForceSpawn pending)
    {
        int index = _pendingSpawns.FindIndex(existing => existing.FireAt > pending.FireAt);
        if (index < 0)
        {
            _pendingSpawns.Add(pending);

            return;
        }

        _pendingSpawns.Insert(index, pending);
    }

    private ThreatSpawnExecutionResult ExecuteSpawn(ThreatPrototype threat,
        MapId mapId,
        Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)> assignedJobs,
        IReadOnlyList<NetUserId>? voteHeldPlayers = null,
        bool requireObserverForVotePlayers = false,
        ResolvedThreatForcePlan? plannedForce = null)
    {
        string threatId = threat.ID;
        ProtoId<PartySpawnPrototype> partySpawn = threat.RoundStartSpawn;
        if (string.IsNullOrWhiteSpace(partySpawn))
        {
            _sawmill.Debug($"[DEBUG] Threat '{threat.ID}' has no RoundStartSpawn configured, skipping spawn.");
            int removed = ThreatSystem.RemoveThreatJobAssignments(assignedJobs);
            if (removed > 0)
            {
                _sawmill.Warning($"[ThreatSystem] Removed {removed} threat assignment(s) for threat '{threat.ID
                }' with no roundstart spawn so normal overflow assignment can handle them.");
            }

            ReleaseVoteHeldPlayers(voteHeldPlayers, threat.ID, "no roundstart spawn was configured", true);

            return new(null, false);
        }

        PartySpawnPrototype? newPartySpawn = _prototypeManager.TryIndex(partySpawn, out PartySpawnPrototype? spawn)
            ? spawn
            : null;
        if (newPartySpawn == null)
        {
            _sawmill.Error($"[ERROR] Could not find RoundStartSpawn prototype '{partySpawn}' for threat '{threat.ID
            }'. Skipping threat spawn.");
            int removed = ThreatSystem.RemoveThreatJobAssignments(assignedJobs);
            if (removed > 0)
            {
                _sawmill.Warning($"[ThreatSystem] Removed {removed} threat assignment(s) for threat '{threat.ID
                }' with missing roundstart spawn '{partySpawn}' so normal overflow assignment can handle them.");
            }

            ReleaseVoteHeldPlayers(voteHeldPlayers, threat.ID, $"roundstart spawn '{partySpawn}' was missing", true);

            return new(null, false);
        }

        TryResolveScenarioPlanSpawnMarkers(threat,
            mapId,
            assignedJobs,
            voteHeldPlayers,
            plannedForce,
            out ResolvedThreatSpawnMarkerSet? scenarioMarkers,
            out ResolvedThreatForcePlan? resolvedForce);
        string presetId = _auRound.SelectedPreset?.ID ?? string.Empty;
        if (scenarioMarkers == null && _scenarioPlan.HasMappedHostileRoundGroup(presetId, threat.ID))
        {
            _sawmill.Error($"[ThreatSystem] Covered Round Group for threat '{threat.ID
            }' resolved without live Spawn Markers on map {mapId
            }; aborting authoritative Scenario Plan threat spawn instead of using legacy marker lookup.");
            int removed = ThreatSystem.RemoveThreatJobAssignments(assignedJobs);
            if (removed > 0)
            {
                _sawmill.Warning($"[ThreatSystem] Removed {removed
                } threat assignment(s) after authoritative Scenario Plan marker resolution failed.");
            }

            ReleaseVoteHeldPlayers(voteHeldPlayers, threat.ID, "Scenario Plan marker resolution failed", true);

            return new(resolvedForce, false);
        }

        var markerCache = new Dictionary<ThreatMarkerType, List<EntityUid>>();

        // Helper to get marker entity Uids by marker type
        List<EntityUid> GetMarkers(ThreatMarkerType markerType)
        {
            if (markerCache.TryGetValue(markerType, out List<EntityUid>? cachedMarkers))
                return cachedMarkers;

            List<EntityUid> markers = ResolveMarkers(markerType);
            markerCache[markerType] = markers;

            return markers;
        }

        List<EntityUid> ResolveMarkers(ThreatMarkerType markerType)
        {
            if (scenarioMarkers != null
                && scenarioMarkers.TryGetMarkers(markerType.ToString(), out IReadOnlyList<EntityUid> plannedMarkers))
            {
                _sawmill.Debug($"[DEBUG] GetMarkers({markerType}): Using {plannedMarkers.Count
                } Scenario Plan marker(s) on map {
                    mapId}");

                return plannedMarkers.ToList();
            }

            string markerId = newPartySpawn != null && newPartySpawn.Markers.TryGetValue(markerType, out string? id)
                ? id
                : string.Empty;
            var legacyMarkers = new List<EntityUid>();
            EntityQueryEnumerator<ThreatSpawnMarkerComponent> query = _entityManager
                .EntityQueryEnumerator<ThreatSpawnMarkerComponent>();
            while (query.MoveNext(out EntityUid uid, out ThreatSpawnMarkerComponent? comp))
            {
                if (comp.ThreatMarkerType == markerType
                    && !comp.ThirdParty
                    && (comp.ID == markerId || (comp.ID == string.Empty && markerId == string.Empty)))
                {
                    if (_entityManager.GetComponent<TransformComponent>(uid).MapID == mapId)
                        legacyMarkers.Add(uid);
                }
            }

            string bucketTag = ScenarioMarkerTags.Bucket(markerType.ToString());
            string markerIdTag = ScenarioMarkerTags.MarkerId(markerId);
            EntityQueryEnumerator<ScenarioSpawnMarkerComponent, TransformComponent> scenarioQuery = _entityManager
                .EntityQueryEnumerator<ScenarioSpawnMarkerComponent, TransformComponent>();
            while (scenarioQuery.MoveNext(out EntityUid uid, out ScenarioSpawnMarkerComponent? comp,
                       out TransformComponent? transform))
            {
                if (transform.MapID != mapId ||
                    _entityManager.HasComponent<ThreatSpawnMarkerComponent>(uid) ||
                    comp.Kind != SpawnMarkerKind.ThreatMarker ||
                    !comp.Tags.Contains(ScenarioMarkerTags.ForceHostile, StringComparer.OrdinalIgnoreCase) ||
                    !comp.Tags.Contains(bucketTag, StringComparer.OrdinalIgnoreCase) ||
                    !comp.Tags.Contains(markerIdTag, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                legacyMarkers.Add(uid);
            }

            _sawmill.Debug($"[DEBUG] GetMarkers({markerType}): Found {legacyMarkers.Count} legacy-compatible markers with markerId '{
                markerId
            }' on map {mapId}");

            return legacyMarkers;
        }

        // --- Spawn entities and collect them for mind assignment ---
        var spawnedLeaders = new List<EntityUid>();
        var spawnedMembers = new List<EntityUid>();
        _sawmill.Debug($"[DEBUG] Begin spawning threat entities for threat: {threat?.ID ?? "null"}");

        // --- Spawn Together logic ---
        bool spawnTogether = newPartySpawn?.SpawnTogether == true;
        Dictionary<ThreatMarkerType, List<EntityUid>> spawnTogetherMarkers = new();
        if (spawnTogether)
        {
            // Gather all markers of all types
            var allMarkers = new List<EntityUid>();
            foreach (ThreatMarkerType type in ThreatMarkerTypes)
            {
                allMarkers.AddRange(GetMarkers(type));
            }

            if (allMarkers.Count > 0)
            {
                EntityUid centerMarker = allMarkers[_random.Next(allMarkers.Count)];
                EntityCoordinates centerCoords = _entityManager.GetComponent<TransformComponent>(centerMarker)
                    .Coordinates;
                foreach (ThreatMarkerType type in ThreatMarkerTypes)
                {
                    List<EntityUid> markers = GetMarkers(type);
                    var filtered = new List<EntityUid>();
                    foreach (EntityUid marker in markers)
                    {
                        EntityCoordinates coords = _entityManager.GetComponent<TransformComponent>(marker).Coordinates;
                        if (_transform.InRange(coords, centerCoords, 50f))
                            filtered.Add(marker);
                    }

                    // Fallback to all markers if none are in range
                    spawnTogetherMarkers[type] = filtered.Count > 0 ? filtered : markers;
                }
            }
        }

        List<EntityUid> GetSpawnMarkers(ThreatMarkerType type)
        {
            if (spawnTogether && spawnTogetherMarkers.TryGetValue(type, out List<EntityUid>? cached))
                return cached;

            return GetMarkers(type);
        }

        // Spawn leaders
        if (newPartySpawn != null)
        {
            int playerCount = _playerManager.PlayerCount;

            IReadOnlyDictionary<string, int> leaderBodies = ThreatSystem.GetSpawnBodies(resolvedForce?.SpawnPlan,
                ThreatMarkerType.Leader,
                newPartySpawn.LeadersToSpawn,
                newPartySpawn.Scaling,
                playerCount);
            IReadOnlyDictionary<string, int> memberBodies = ThreatSystem.GetSpawnBodies(resolvedForce?.SpawnPlan,
                ThreatMarkerType.Member,
                newPartySpawn.GruntsToSpawn,
                newPartySpawn.Scaling,
                playerCount);
            IReadOnlyDictionary<string, int> entityBodies = ThreatSystem.GetSpawnBodies(resolvedForce?.SpawnPlan,
                ThreatMarkerType.Entity,
                newPartySpawn.EntitiesToSpawn,
                EmptyScaling,
                playerCount);
            int leaderReq = leaderBodies.Values.Sum();
            int memberReq = memberBodies.Values.Sum();
            int entityReq = entityBodies.Values.Sum();
            List<EntityUid> leaderMarkers = GetSpawnMarkers(ThreatMarkerType.Leader);
            List<EntityUid> memberMarkers = GetSpawnMarkers(ThreatMarkerType.Member);
            List<EntityUid> entityMarkers = GetSpawnMarkers(ThreatMarkerType.Entity);
            List<EntityUid> originalLeaderMarkers = leaderMarkers.ToList();
            List<EntityUid> originalMemberMarkers = memberMarkers.ToList();
            if (_sawmill.Level <= LogLevel.Debug)
            {
                ThreatAssignmentCounts assignmentCounts = ThreatSystem.CountThreatAssignments(assignedJobs);
                _sawmill.Debug($"[ThreatSystem] Threat spawn plan for '{threatId}': force={
                    resolvedForce?.ForceId ?? "legacy"
                }, partySpawn={newPartySpawn!.ID}, leaders[{ThreatSystem.FormatSpawnBodies(leaderBodies)
                }] requested={leaderReq} markers={leaderMarkers.Count}, members[{
                    ThreatSystem.FormatSpawnBodies(memberBodies)}] requested={memberReq} markers={
                        memberMarkers.Count}, entities[{ThreatSystem.FormatSpawnBodies(entityBodies)}] requested={
                            entityReq} markers={entityMarkers.Count}, assignedThreatLeaders={
                                assignmentCounts.Leaders}, assignedThreatMembers={assignmentCounts.Members}.");
            }

            if (leaderReq > 0 && leaderMarkers.Count == 0)
            {
                _sawmill.Warning($"[ThreatSystem] Threat '{threatId}' requested {leaderReq
                } leader body/bodies but found no leader markers on map {mapId}.");
            }

            if (memberReq > 0 && memberMarkers.Count == 0)
            {
                _sawmill.Warning($"[ThreatSystem] Threat '{threatId}' requested {memberReq
                } member body/bodies but found no member markers on map {mapId}.");
            }

            if (entityReq > 0 && entityMarkers.Count == 0)
            {
                _sawmill.Warning($"[ThreatSystem] Threat '{threatId}' requested {entityReq
                } entity spawn(s) but found no entity markers on map {mapId}.");
            }

            int SpawnBodies(string protoId, int count, List<EntityUid> markers, List<EntityUid>? spawnedList,
                string label)
            {
                _sawmill.Debug($"[DEBUG] Spawning {count} {label}(s) of protoId {protoId} at {markers.Count} markers");

                if (count > markers.Count)
                {
                    _sawmill.Warning($"[ThreatSystem] Threat '{threatId}' requested {count} {label} body/bodies for {
                        protoId
                    } but only {markers.Count
                    } marker(s) are available; remaining bodies will not spawn instead of stacking on reused markers.");
                }

                var spawned = 0;
                for (var i = 0; i < count && markers.Count > 0; i++)
                {
                    int markerIndex = _random.Next(markers.Count);
                    EntityUid marker = markers[markerIndex];
                    markers.RemoveAt(markerIndex);
                    EntityCoordinates coords = _entityManager.GetComponent<TransformComponent>(marker).Coordinates;

                    try
                    {
                        EntityUid ent = _entityManager.SpawnEntity(protoId, coords);
                        spawnedList?.Add(ent);
                        spawned++;
                        _sawmill.Debug($"[DEBUG] Spawned {label} entity {ent} at marker {marker}");
                    }
                    catch (Exception ex)
                    {
                        _sawmill.Error($"[ThreatSystem] Failed to spawn {label} ({protoId}) for threat '{threatId
                        }' at marker {marker}: {ex}");
                    }
                }

                return spawned;
            }

            string? GetExtraMemberPrototype()
            {
                foreach ((string protoId, int count) in memberBodies)
                {
                    if (count > 0)
                        return protoId;
                }

                return null;
            }

            int SpawnExtraVoteMemberBodies(int count)
            {
                if (count <= 0)
                    return 0;

                string? protoId = GetExtraMemberPrototype();
                if (protoId == null)
                {
                    _sawmill.Warning($"[ThreatSystem] Threat '{threatId}' needs {count} extra voted member body/bodies but has no member body prototype configured.");

                    return 0;
                }

                List<EntityUid> markers = originalMemberMarkers.Count > 0
                    ? originalMemberMarkers
                    : originalLeaderMarkers;
                if (markers.Count == 0)
                {
                    _sawmill.Warning($"[ThreatSystem] Threat '{threatId}' needs {count} extra voted member body/bodies but has no member or leader markers to reuse.");

                    return 0;
                }

                var spawned = 0;
                for (var i = 0; i < count; i++)
                {
                    EntityUid marker = markers[_random.Next(markers.Count)];
                    EntityCoordinates coords = _entityManager.GetComponent<TransformComponent>(marker).Coordinates;

                    try
                    {
                        EntityUid ent = _entityManager.SpawnEntity(protoId, coords);
                        spawnedMembers.Add(ent);
                        spawned++;
                    }
                    catch (Exception ex)
                    {
                        _sawmill.Error($"[ThreatSystem] Failed to spawn extra voted member ({protoId}) for threat '{threatId}' at marker {marker}: {ex}");
                    }
                }

                return spawned;
            }

            // Spawn leaders — each entity proto gets its own scaled count
            foreach ((string protoId, int count) in leaderBodies)
            {
                SpawnBodies(protoId, count, leaderMarkers, spawnedLeaders, "leader");
            }

            // Spawn grunts/members — each entity proto gets its own scaled count
            foreach ((string protoId, int count) in memberBodies)
            {
                SpawnBodies(protoId, count, memberMarkers, spawnedMembers, "member");
            }

            _sawmill.Debug($"[DEBUG] Spawned {spawnedMembers.Count} threat members.");

            // Spawn other entities
            var spawnedEntities = 0;
            foreach ((string protoId, int count) in entityBodies)
            {
                spawnedEntities += SpawnBodies(protoId, count, entityMarkers, null, "entity");
            }

            _sawmill.Debug($"[DEBUG] Spawned {spawnedEntities} other threat entities.");
            _sawmill.Info($"[ThreatSystem] Threat spawn result for '{threatId}': spawnedLeaders={spawnedLeaders.Count}/{
                leaderReq
            }, spawnedMembers={spawnedMembers.Count}/{memberReq}, spawnedEntities={spawnedEntities}/{entityReq}.");

            var spawnedThreatPlayers = new HashSet<NetUserId>();

            if (voteHeldPlayers != null)
            {
                List<NetUserId> eligibleHeldPlayers = GetEligibleVoteHeldPlayers(voteHeldPlayers,
                    requireObserverForVotePlayers);
                int missingVoteBodies = eligibleHeldPlayers.Count - spawnedLeaders.Count - spawnedMembers.Count;
                int extraMembers = SpawnExtraVoteMemberBodies(missingVoteBodies);
                if (extraMembers > 0)
                {
                    _sawmill.Info($"[ThreatSystem] Spawned {extraMembers} extra voted threat member body/bodies for '{threatId}' so held voters can enter the round.");
                }

                _random.Shuffle(eligibleHeldPlayers);
                var heldAssignments = new List<ThreatVoteAssignment>(eligibleHeldPlayers.Count);
                foreach (NetUserId player in eligibleHeldPlayers)
                {
                    ProtoId<JobPrototype> job = assignedJobs.TryGetValue(player,
                            out (ProtoId<JobPrototype>?, EntityUid) assigned)
                        && assigned.Item1 == ThreatLeaderJobId
                            ? ThreatLeaderJobId
                            : ThreatMemberJobId;

                    heldAssignments.Add(new(player, job));
                }

                List<ThreatVoteAssignment> voteAssignments = ThreatVoteSelection.BuildSpawnAssignments(heldAssignments,
                    spawnedLeaders.Count,
                    spawnedMembers.Count);
                var leaderAssignments = new List<ThreatVoteAssignment>(spawnedLeaders.Count);
                var memberAssignments = new List<ThreatVoteAssignment>(spawnedMembers.Count);
                foreach (ThreatVoteAssignment assignment in voteAssignments)
                {
                    if (assignment.Job == ThreatLeaderJobId)
                        leaderAssignments.Add(assignment);
                    else if (assignment.Job == ThreatMemberJobId)
                        memberAssignments.Add(assignment);
                }

                int assignedLeaders = AssignThreatMinds(leaderAssignments,
                    spawnedLeaders,
                    spawnedThreatPlayers);
                int assignedMembers = AssignThreatMinds(memberAssignments,
                    spawnedMembers,
                    spawnedThreatPlayers);
                _sawmill.Info($"[ThreatSystem] Voted threat assignment result for '{threatId}': eligibleHeldPlayers={
                    eligibleHeldPlayers.Count}, assignedLeaders={assignedLeaders}/{spawnedLeaders.Count
                    }, assignedMembers={assignedMembers}/{spawnedMembers.Count}.");

                foreach (NetUserId playerId in eligibleHeldPlayers)
                {
                    if (spawnedThreatPlayers.Contains(playerId))
                        continue;

                    if (!_playerManager.TryGetSessionById(playerId, out ICommonSession? session))
                        continue;

                    _sawmill.Info($"[ThreatSystem] Player {session.Name} ({playerId
                    }) returning to lobby as there was no threat mob available for them.");
                    _ticker.Respawn(session);
                }

                AddGhostRolesForUnassigned(spawnedLeaders, assignedLeaders, ThreatLeaderJobId);
                AddGhostRolesForUnassigned(spawnedMembers, assignedMembers, ThreatMemberJobId);

                _sawmill.Debug($"[DEBUG] Voted threat assigned {assignedLeaders} leader(s), {assignedMembers
                } member(s), exposed {
                    spawnedLeaders.Count - assignedLeaders + spawnedMembers.Count - assignedMembers
                } ghost role body/bodies.");

                int removedVoteAssignments
                    = ThreatSystem.RemoveThreatJobAssignments(assignedJobs, spawnedThreatPlayers);
                if (removedVoteAssignments > 0)
                {
                    _sawmill.Warning($"[ThreatSystem] Removed {removedVoteAssignments
                    } unspawned voted threat assignment(s).");
                }

                ReleaseVoteHeldPlayers(voteHeldPlayers, threatId, "voted threat spawn completed", false);

                return new(resolvedForce, true);
            }

            var roundstartLeaderAssignments = new List<ThreatVoteAssignment>();
            var roundstartMemberAssignments = new List<ThreatVoteAssignment>();
            foreach ((NetUserId player, (ProtoId<JobPrototype>? job, EntityUid _)) in assignedJobs)
            {
                if (job == ThreatLeaderJobId)
                    roundstartLeaderAssignments.Add(new(player, ThreatLeaderJobId));
                else if (job == ThreatMemberJobId)
                    roundstartMemberAssignments.Add(new(player, ThreatMemberJobId));
            }

            int assignedRoundstartLeaders = AssignThreatMinds(roundstartLeaderAssignments, spawnedLeaders,
                spawnedThreatPlayers);
            _sawmill.Debug($"[DEBUG] Assigned {assignedRoundstartLeaders} leader minds");
            int assignedRoundstartMembers = AssignThreatMinds(roundstartMemberAssignments, spawnedMembers,
                spawnedThreatPlayers);
            _sawmill.Debug($"[DEBUG] Assigned {assignedRoundstartMembers} member minds");
            _sawmill.Info($"[ThreatSystem] Roundstart threat assignment result for '{threatId}': leaderPlayers={
                roundstartLeaderAssignments.Count}, assignedLeaders={assignedRoundstartLeaders}/{
                    spawnedLeaders.Count}, memberPlayers={roundstartMemberAssignments.Count}, assignedMembers={
                        assignedRoundstartMembers}/{spawnedMembers.Count}.");
            if (roundstartLeaderAssignments.Count > spawnedLeaders.Count)
            {
                _sawmill.Warning($"[ThreatSystem] Threat '{threatId}' had {roundstartLeaderAssignments.Count
                } assigned leader player(s) but only {spawnedLeaders.Count} leader body/bodies spawned.");
            }

            if (roundstartMemberAssignments.Count > spawnedMembers.Count)
            {
                _sawmill.Warning($"[ThreatSystem] Threat '{threatId}' had {roundstartMemberAssignments.Count
                } assigned member player(s) but only {spawnedMembers.Count} member body/bodies spawned.");
            }

            int removed = ThreatSystem.RemoveThreatJobAssignments(assignedJobs, spawnedThreatPlayers);
            if (removed > 0)
            {
                _sawmill.Warning($"[ThreatSystem] Removed {removed
                } unspawned threat assignment(s) so normal overflow assignment can handle them.");
            }
        }

        return new(resolvedForce, true);
    }

    private static IReadOnlyDictionary<string, int> GetSpawnBodies(ResolvedSpawnPlan? spawnPlan,
        ThreatMarkerType markerType,
        IReadOnlyDictionary<string, int> legacyBodies,
        IReadOnlyDictionary<string, JobScaleEntry> legacyScaling,
        int playerCount)
    {
        SpawnBodyBucket? bucket = spawnPlan?.BodyBuckets.FirstOrDefault(bodyBucket =>
            bodyBucket.Bucket.Equals(markerType.ToString(), StringComparison.OrdinalIgnoreCase));

        if (bucket != null && (bucket.Bodies.Count > 0 || bucket.Count == 0))
            return bucket.Bodies;

        var bodies = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach ((string bodyId, int staticCount) in legacyBodies)
        {
            bodies[bodyId] = legacyScaling.TryGetValue(bodyId, out JobScaleEntry entry)
                ? JobScaling.CalculateScaledSlots(playerCount, staticCount, entry)
                : Math.Max(0, staticCount);
        }

        return bodies;
    }

    private void StartThreatWinConditions(ThreatPrototype threat, ResolvedThreatForcePlan? resolvedForce)
    {
        if (resolvedForce != null && resolvedForce.ThreatId.Equals(threat.ID, StringComparison.OrdinalIgnoreCase))
        {
            _auRound.StartThreatWinConditions(resolvedForce.WinConditionRuleIds,
                $"planned threat '{resolvedForce.ThreatId}'");

            return;
        }

        _auRound.StartThreatWinConditions(threat);
    }

    private bool TryResolveScenarioPlanSpawnMarkers(ThreatPrototype threat,
        MapId mapId,
        Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)> assignedJobs,
        IReadOnlyList<NetUserId>? voteHeldPlayers,
        ResolvedThreatForcePlan? plannedForce,
        out ResolvedThreatSpawnMarkerSet? markers,
        out ResolvedThreatForcePlan? resolvedForce)
    {
        markers = null;
        resolvedForce = plannedForce;
        var coveredScenarioForce = false;

        try
        {
            ScenarioPlanValidationRequest request = BuildThreatSpawnScenarioPlanRequest(threat, assignedJobs,
                voteHeldPlayers);
            coveredScenarioForce = _scenarioPlan.HasMappedHostileRoundGroup(request.PresetId, threat.ID);
            if (resolvedForce == null)
            {
                if (_scenarioPlan.TryResolveSelectedThreatForce(request, out resolvedForce, out string forceDiagnostic)
                    && resolvedForce != null)
                {
                    _sawmill.Debug($"[ThreatSystem] Resolved Scenario Plan Force Plan '{resolvedForce.ForceId
                    }' for threat '{
                        threat.ID}'.");
                }
                else
                {
                    string backupDiagnostic = coveredScenarioForce
                        ? "covered Round Groups do not use legacy marker lookup"
                        : "falling back to legacy marker lookup";
                    _sawmill.Warning($"[ThreatSystem] Could not resolve Scenario Plan Force Plan for threat '{threat.ID
                    }'; {
                        backupDiagnostic}. {forceDiagnostic}");
                    resolvedForce = null;

                    return false;
                }
            }

            if (resolvedForce != null)
            {
                if (_scenarioPlan.TryResolveThreatSpawnMarkers(resolvedForce, mapId, out markers,
                    out string plannedDiagnostic))
                {
                    _sawmill.Debug($"[ThreatSystem] Using Scenario Plan Spawn Markers for threat '{threat.ID}' on map {
                        mapId}.");

                    return true;
                }

                _sawmill.Warning(coveredScenarioForce
                    ? $"[ThreatSystem] Could not resolve Scenario Plan Spawn Markers for covered threat '{threat.ID
                    }' on map {mapId}. {plannedDiagnostic}"
                    : $"[ThreatSystem] Could not resolve Scenario Plan Spawn Markers for threat '{threat.ID
                    }' on map {mapId}; falling back to legacy marker lookup. {plannedDiagnostic}");
            }
        }
        catch (Exception ex)
        {
            string backupDiagnostic = coveredScenarioForce
                ? "covered Round Groups do not use legacy marker lookup"
                : "falling back to legacy marker lookup";
            _sawmill.Error($"[ThreatSystem] Scenario Plan Spawn Marker resolution threw for threat '{threat.ID
            }' on map {mapId}; {
                backupDiagnostic}. {ex}");
        }

        markers = null;

        return false;
    }

    private ScenarioPlanValidationRequest BuildThreatSpawnScenarioPlanRequest(ThreatPrototype threat,
        Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)> assignedJobs,
        IReadOnlyCollection<NetUserId>? voteHeldPlayers)
    {
        int playerCount = Math.Max(_playerManager.PlayerCount, assignedJobs.Count);
        if (voteHeldPlayers != null)
            playerCount = Math.Max(playerCount, voteHeldPlayers.Count);

        return new(_auRound.SelectedPreset?.ID ?? string.Empty,
            playerCount,
            _platoonSpawnRule.SelectedGovforPlatoon?.ID,
            _platoonSpawnRule.SelectedOpforPlatoon?.ID,
            _auRound.GetSelectedPlanetId(),
            _auRound.GetSelectedPlanet()?.MapId,
            threat.ID,
            _auRound.GetSelectedGovforShip(),
            _auRound.GetSelectedOpforShip());
    }

    private List<NetUserId> GetEligibleVoteHeldPlayers(IReadOnlyList<NetUserId> heldPlayers,
        bool requireObserver)
    {
        var eligible = new List<NetUserId>(heldPlayers.Count);
        foreach (NetUserId playerId in heldPlayers)
        {
            if (!_playerManager.TryGetSessionById(playerId, out ICommonSession? session)
                || session.Status == SessionStatus.Disconnected)
                continue;

            if (requireObserver && !_entityManager.TryGetComponent(session.AttachedEntity, out GhostComponent? _))
                continue;

            eligible.Add(playerId);
        }

        return eligible;
    }

    private int AssignThreatMinds(IReadOnlyList<ThreatVoteAssignment> assignments,
        IReadOnlyList<EntityUid> entities,
        HashSet<NetUserId> spawnedThreatPlayers)
    {
        var assigned = 0;
        for (var i = 0; i < assignments.Count; i++)
        {
            if (assigned >= entities.Count)
                break;

            ThreatVoteAssignment assignment = assignments[i];

            if (!TryAssignThreatMind(assignment.Player, entities[assigned], assignment.Job))
                continue;

            spawnedThreatPlayers.Add(assignment.Player);
            assigned++;
        }

        return assigned;
    }

    private bool TryAssignThreatMind(NetUserId playerNetId,
        EntityUid entity,
        ProtoId<JobPrototype> jobId)
    {
        if (!_playerManager.TryGetSessionById(playerNetId, out ICommonSession? session))
        {
            _sawmill.Error($"[THREAT SPAWN] Could not find session for player {playerNetId}");

            return false;
        }

        _ticker.PlayerJoinGame(session, true);

        GhostRoleComponent? ghostRole = null;
        if (TryComp(entity, out ghostRole) && ghostRole.MakeSentient)
            MakeSentientCommand.MakeSentient(entity, EntityManager, ghostRole.AllowMovement, ghostRole.AllowSpeech);

        ContentPlayerData? data = session.ContentData();
        EntityUid? mind = _mindSystem.GetMind(playerNetId);
        if (mind == null)
        {
            mind = _mindSystem.CreateMind(playerNetId, data?.Name ?? "Threat Player");
            _mindSystem.SetUserId(mind.Value, playerNetId);
            _sawmill.Debug($"[DEBUG] Created mind for threat player {playerNetId}");
        }

        _mindSystem.TransferTo(mind.Value, entity);
        _sawmill.Debug($"[DEBUG] Assigned threat mind {mind.Value} to entity {entity} for player {playerNetId} as {
            jobId.Id}");

        ProtoId<JobPrototype> entityJob = ghostRole?.JobProto ?? jobId;
        _roles.MindAddJobRole(mind.Value, silent: true, jobPrototype: entityJob);
        AddStartingMindRole(entity, mind.Value);
        _roles.MindAddRole(mind.Value, ThreatMindRoleId, silent: true);

        AddThreatFaction(entity);

        if (ghostRole != null) _ghostRole.UnregisterGhostRole((entity, ghostRole));

        return true;
    }

    private void AddStartingMindRole(EntityUid entity, EntityUid mind)
    {
        if (TryComp(entity, out StartingMindRoleComponent? starting))
            _roles.MindAddRole(mind, starting.MindRole, silent: starting.Silent);
    }

    private void AddGhostRolesForUnassigned(IReadOnlyList<EntityUid> entities,
        int assignedCount,
        ProtoId<JobPrototype> jobId)
    {
        for (int i = Math.Max(0, assignedCount); i < entities.Count; i++)
        {
            MakeThreatGhostRole(entities[i], jobId);
        }
    }

    private void MakeThreatGhostRole(EntityUid entity, ProtoId<JobPrototype> jobId)
    {
        AddThreatFaction(entity);

        var ghostRole = EnsureComp<GhostRoleComponent>(entity);
        ghostRole.RoleName = jobId == ThreatLeaderJobId
            ? "au14-threat-leader-ghost-role-name"
            : "au14-threat-ghost-role-name";
        ghostRole.RoleDescription = "au14-threat-ghost-role-description";
        ghostRole.RoleRules = "au14-threat-ghost-role-rules";
        ghostRole.JobProto = jobId;
        ghostRole.MindRoles = new List<EntProtoId> { ThreatMindRoleId };

        EnsureComp<GhostTakeoverAvailableComponent>(entity);
    }

    private void AddThreatFaction(EntityUid entity)
    {
        EnsureComp<NpcFactionMemberComponent>(entity);
        _npcFaction.AddFaction((entity, CompOrNull<NpcFactionMemberComponent>(entity)), threatNPCFaction);
    }

    internal bool HasCrashedDropship()
    {
        EntityQueryEnumerator<DropshipComponent> dropships = EntityQueryEnumerator<DropshipComponent>();
        while (dropships.MoveNext(out _, out DropshipComponent? dropship))
        {
            return dropship.Crashed;
        }

        return false;
    }

    internal bool IsExcludedFromVictory(EntityUid uid, MobStateComponent mobState)
    {
        // Exclude all threats, which use their own Victory Condition logic
        if (HasComp<XenoComponent>(uid)
            || HasComp<YautjaComponent>(uid)
            || HasComp<ApeComponent>(uid)
            || HasComp<TribalComponent>(uid)
            || HasComp<AbominationComponent>(uid)
            || HasComp<AbominationMimicComponent>(uid))
            return true;

        if (HasComp<SynthComponent>(uid))
            return true;

        // Don't exclude the dead (ghosts), we tally them as eliminated instead
        if (mobState.CurrentState == MobState.Dead)
            return false;

        // Alive and nested or SSD
        return HasComp<XenoNestedComponent>(uid)
            || (TryComp(uid, out SSDIndicatorComponent? ssd) && ssd.IsSSD);
    }

    internal sealed record PendingThreatSpawnDebugView(
        string ThreatId,
        TimeSpan FireAt,
        ResolvedThreatForcePlan? PlannedForce
    );

    private readonly record struct ThreatAssignmentCounts(int Leaders, int Members);

    private readonly record struct ThreatSpawnExecutionResult(
        ResolvedThreatForcePlan? ResolvedForce,
        bool Spawned
    );

    private sealed class PendingThreatForceSpawn
    {
        public required Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)> AssignedJobs;
        public required TimeSpan FireAt;
        public required MapId MapId;
        public ResolvedThreatForcePlan? PlannedForce;
        public bool RequireObserverForVotePlayers;
        public required ThreatPrototype Threat;
        public IReadOnlyList<NetUserId>? VoteHeldPlayers;
    }
}
