using System.Collections.Generic;
using System.Linq;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Mind.Commands;
using Content.Shared.AU14.Threats;
using Content.Server.AU14.Round;
using Robust.Shared.Timing;
using Content.Shared.AU14.util;
using Robust.Shared.Prototypes;
using Robust.Shared.Map;
using Content.Shared.Roles;
using Content.Shared.Mind;
using Content.Server.GameTicking;
using Robust.Shared.Network;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.Ghost;
using Content.Shared.NPC.Components;
using Content.Shared.Players;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Enums;
using Robust.Shared.Log;
using Robust.Shared.Random;

namespace Content.Server.AU14.Threats;

public sealed partial class AuThreatSystem : EntitySystem
{
    private static readonly ProtoId<JobPrototype> ThreatLeaderJobId = new("AU14JobThreatLeader");
    private static readonly ProtoId<JobPrototype> ThreatMemberJobId = new("AU14JobThreatMember");

    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private SharedMindSystem _mindSystem = default!;
    [Dependency] private NpcFactionSystem _npcFaction = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IGameTiming _timing = default!;
    public readonly ProtoId<NpcFactionPrototype> threatnpcfaction = "THREAT";
    [Dependency] private SharedRoleSystem _roles = default!;
    [Dependency] private GhostRoleSystem _ghostRole = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private sealed class PendingThreatSpawn
    {
        public required ThreatPrototype Threat;
        public required MapId MapId;
        public required Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)> AssignedJobs;
        public required TimeSpan FireAt;
        public IReadOnlyList<NetUserId>? VoteHeldPlayers;
        public bool RequireObserverForVotePlayers;
    }

    private PendingThreatSpawn? _pendingSpawn;

    internal static bool IsThreatJob(ProtoId<JobPrototype>? job)
    {
        return job == ThreatLeaderJobId || job == ThreatMemberJobId;
    }

    internal static int RemoveThreatJobAssignments(
        Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)> assignedJobs,
        IReadOnlySet<NetUserId>? keepPlayers = null)
    {
        var removed = 0;
        foreach (var (player, (job, _)) in assignedJobs.ToArray())
        {
            if (!IsThreatJob(job))
                continue;

            if (keepPlayers != null && keepPlayers.Contains(player))
                continue;

            assignedJobs.Remove(player);
            removed++;
        }

        return removed;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_pendingSpawn == null || _timing.CurTime < _pendingSpawn.FireAt)
            return;

        var pending = _pendingSpawn;
        _pendingSpawn = null;

        try
        {
            ExecuteSpawn(
                pending.Threat,
                pending.MapId,
                pending.AssignedJobs,
                pending.VoteHeldPlayers,
                pending.RequireObserverForVotePlayers);
            var roundSystem = _entityManager.EntitySysManager.GetEntitySystem<AuRoundSystem>();
            roundSystem.StartThreatWinConditions(pending.Threat);
        }
        catch (Exception ex)
        {
            Logger.GetSawmill("au14.threat").Error($"[AuThreatSystem] Delayed threat spawn threw: {ex}");
        }
    }

    /// <summary>
    /// In Colony Fall: schedules threat entity spawning and win condition activation after a random
    /// delay via the game update loop. In all other presets: spawns and starts win conditions immediately.
    /// </summary>
    public void SpawnThreatAtRoundStart(ThreatPrototype threat,
        MapId mapId,
        Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)> assignedJobs)
    {
        if (threat == null)
        {
            Logger.GetSawmill("au14.threat").Debug("[AuThreatSystem] No threat selected for round start, skipping threat spawn.");
            return;
        }

        var roundSystem = _entityManager.EntitySysManager.GetEntitySystem<AuRoundSystem>();
        var isColonyFall = string.Equals(roundSystem.SelectedPreset?.ID, "ColonyFall", StringComparison.OrdinalIgnoreCase);

        if (isColonyFall)
        {
            var delaySeconds = _random.NextDouble() * (threat.SpawnDelayMax - threat.SpawnDelayMin) + threat.SpawnDelayMin;
            Logger.GetSawmill("au14.threat").Debug($"[AuThreatSystem] Colony Fall threat '{threat.ID}' will spawn in {delaySeconds:F1}s.");
            _pendingSpawn = new PendingThreatSpawn
            {
                Threat = threat,
                MapId = mapId,
                AssignedJobs = assignedJobs,
                FireAt = _timing.CurTime + TimeSpan.FromSeconds(delaySeconds),
            };
        }
        else
        {
            ExecuteSpawn(threat, mapId, assignedJobs);
            roundSystem.StartThreatWinConditions(threat);
        }
    }

    public void SpawnThreatFromVote(
        ThreatPrototype threat,
        MapId mapId,
        Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)> assignedJobs,
        IReadOnlyList<NetUserId> heldPlayers)
    {
        if (threat == null)
        {
            Logger.GetSawmill("au14.threat").Debug("[AuThreatSystem] No threat selected from vote, skipping threat spawn.");
            return;
        }

        var roundSystem = _entityManager.EntitySysManager.GetEntitySystem<AuRoundSystem>();
        var isColonyFall = string.Equals(roundSystem.SelectedPreset?.ID, "ColonyFall", StringComparison.OrdinalIgnoreCase);

        if (isColonyFall)
        {
            var delaySeconds = _random.NextDouble() * (threat.SpawnDelayMax - threat.SpawnDelayMin) + threat.SpawnDelayMin;
            Logger.GetSawmill("au14.threat").Debug($"[AuThreatSystem] Colony Fall voted threat '{threat.ID}' will spawn in {delaySeconds:F1}s.");
            _pendingSpawn = new PendingThreatSpawn
            {
                Threat = threat,
                MapId = mapId,
                AssignedJobs = assignedJobs,
                FireAt = _timing.CurTime + TimeSpan.FromSeconds(delaySeconds),
                VoteHeldPlayers = heldPlayers.ToList(),
                RequireObserverForVotePlayers = true,
            };
        }
        else
        {
            ExecuteSpawn(threat, mapId, assignedJobs, heldPlayers, false);
            roundSystem.StartThreatWinConditions(threat);
        }
    }

    private void ExecuteSpawn(ThreatPrototype threat,
        MapId mapId,
        Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)> assignedJobs,
        IReadOnlyList<NetUserId>? voteHeldPlayers = null,
        bool requireObserverForVotePlayers = false)
    {
        var partySpawn = threat.RoundStartSpawn;
        if (string.IsNullOrWhiteSpace(partySpawn))
        {
            Logger.GetSawmill("au14.threat").Debug( $"[DEBUG] Threat '{threat.ID}' has no RoundStartSpawn configured, skipping spawn.");
            var removed = RemoveThreatJobAssignments(assignedJobs);
            if (removed > 0)
                Logger.GetSawmill("au14.threat").Warning($"[AuThreatSystem] Removed {removed} threat assignment(s) for threat '{threat.ID}' with no roundstart spawn so normal overflow assignment can handle them.");
            return;
        }
        var newpartySpawn = _prototypeManager.TryIndex(partySpawn, out var spawn) ? spawn : null;
        if (newpartySpawn == null)
        {
            Logger.GetSawmill("au14.threat").Error( $"[ERROR] Could not find RoundStartSpawn prototype '{partySpawn}' for threat '{threat.ID}'. Skipping threat spawn.");
            var removed = RemoveThreatJobAssignments(assignedJobs);
            if (removed > 0)
                Logger.GetSawmill("au14.threat").Warning($"[AuThreatSystem] Removed {removed} threat assignment(s) for threat '{threat.ID}' with missing roundstart spawn '{partySpawn}' so normal overflow assignment can handle them.");
            return;
        }

        // Helper to get marker entity Uids by marker type
        List<EntityUid> GetMarkers(ThreatMarkerType markerType)
        {
            var markerId = newpartySpawn != null && newpartySpawn.Markers.TryGetValue(markerType, out var id) ? id : "";
            var markers = new List<EntityUid>();
            var query = _entityManager.EntityQueryEnumerator<Content.Shared.AU14.Threats.ThreatSpawnMarkerComponent>();
            while (query.MoveNext(out var uid, out var comp))
            {
                if (comp.ThreatMarkerType == markerType && !comp.ThirdParty && (comp.ID == markerId || (comp.ID == "" && markerId == "")))
                {
                    if (_entityManager.GetComponent<TransformComponent>(uid).MapID == mapId)
                        markers.Add(uid);
                }
            }
            Logger.GetSawmill("au14.threat").Debug(
                $"[DEBUG] GetMarkers({markerType}): Found {markers.Count} markers with markerId '{markerId}' on map {mapId}");
            return markers;
        }

        // --- Spawn entities and collect them for mind assignment ---
        var spawnedLeaders = new List<EntityUid>();
        var spawnedMembers = new List<EntityUid>();
        Logger.GetSawmill("au14.threat").Debug( $"[DEBUG] Begin spawning threat entities for threat: {threat?.ID ?? "null"}");

        // --- Spawn Together logic ---
        bool spawnTogether = newpartySpawn?.SpawnTogether == true;
        Dictionary<ThreatMarkerType, List<EntityUid>> markerCache = new();
        EntityUid? centerMarker = null;
        if (spawnTogether)
        {
            // Gather all markers of all types
            var allMarkers = new List<EntityUid>();
            foreach (ThreatMarkerType type in System.Enum.GetValues(typeof(ThreatMarkerType)))
            {
                allMarkers.AddRange(GetMarkers(type));
            }

            if (allMarkers.Count > 0)
            {
                centerMarker = allMarkers[_random.Next(allMarkers.Count)];
                var centerCoords = _entityManager.GetComponent<TransformComponent>(centerMarker.Value).Coordinates;
                foreach (ThreatMarkerType type in System.Enum.GetValues(typeof(ThreatMarkerType)))
                {
                    var markers = GetMarkers(type);
                    var filtered = markers.Where(m =>
                    {
                        var coords = _entityManager.GetComponent<TransformComponent>(m).Coordinates;
                        return _transform.InRange(coords, centerCoords, 50f);
                    }).ToList();
                    // Fallback to all markers if none are in range
                    markerCache[type] = filtered.Count > 0 ? filtered : markers;
                }
            }
        }

        List<EntityUid> GetSpawnMarkers(ThreatMarkerType type)
        {
            if (spawnTogether && markerCache.TryGetValue(type, out var cached))
                return cached;
            return GetMarkers(type);
        }

        // Spawn leaders
        if (newpartySpawn != null)
        {
            var playerCount = _playerManager.PlayerCount;

            // Helper: compute the spawn count for a single entity prototype ID
            // using the per-entity scaling dict on the PartySpawnPrototype.
            // If Benchmark is set it overrides the base; otherwise the static count is the base.
            int GetScaledCount(string protoId, int staticCount)
            {
                if (newpartySpawn.Scaling.TryGetValue(protoId, out var entry))
                {
                    return JobScaling.CalculateScaledSlots(playerCount, staticCount, entry);
                }
                return staticCount;
            }

            // Spawn leaders — each entity proto gets its own scaled count
            foreach (var (protoId, staticCount) in newpartySpawn.LeadersToSpawn)
            {
                var count = GetScaledCount(protoId, staticCount);
                var markers = GetSpawnMarkers(ThreatMarkerType.Leader);
                Logger.GetSawmill("au14.threat").Debug(
                    $"[DEBUG] Spawning {count} leaders of protoId {protoId} at {markers.Count} markers (static={staticCount})");
                for (int i = 0; i < count; i++)
                {
                    var marker = markers.Count > 0 ? markers[i % markers.Count] : EntityUid.Invalid;
                    if (marker != EntityUid.Invalid)
                    {
                        var ent = _entityManager.SpawnEntity(protoId,
                            _entityManager.GetComponent<TransformComponent>(marker).Coordinates);
                        spawnedLeaders.Add(ent);
                        Logger.GetSawmill("au14.threat").Debug( $"[DEBUG] Spawned leader entity {ent} at marker {marker}");
                    }
                }
            }

            // Spawn grunts/members — each entity proto gets its own scaled count
            foreach (var (protoId, staticCount) in newpartySpawn.GruntsToSpawn)
            {
                var count = GetScaledCount(protoId, staticCount);
                var markers = GetSpawnMarkers(ThreatMarkerType.Member);
                Logger.GetSawmill("au14.threat").Debug(
                    $"[DEBUG] Spawning {count} members of protoId {protoId} at {markers.Count} markers (static={staticCount})");
                for (int i = 0; i < count; i++)
                {
                    var marker = markers.Count > 0 ? markers[i % markers.Count] : EntityUid.Invalid;
                    if (marker != EntityUid.Invalid)
                    {
                        var ent = _entityManager.SpawnEntity(protoId,
                            _entityManager.GetComponent<TransformComponent>(marker).Coordinates);
                        spawnedMembers.Add(ent);
                        Logger.GetSawmill("au14.threat").Debug( $"[DEBUG] Spawned member entity {ent} at marker {marker}");
                    }
                }
            }

            Logger.GetSawmill("au14.threat").Debug( $"[DEBUG] Spawned {spawnedMembers.Count} threat members.");

            // Spawn other entities
            var spawnedEntities = 0;
            foreach (var (protoId, count) in newpartySpawn.entitiestospawn)
            {
                var markers = GetSpawnMarkers(ThreatMarkerType.Entity);
                Logger.GetSawmill("au14.threat").Debug(
                    $"[DEBUG] Spawning {count} other entities of protoId {protoId} at {markers.Count} markers");
                for (int i = 0; i < count; i++)
                {
                    var marker = markers.Count > 0 ? markers[i % markers.Count] : EntityUid.Invalid;
                    if (marker != EntityUid.Invalid)
                    {
                        _entityManager.SpawnEntity(protoId,
                            _entityManager.GetComponent<TransformComponent>(marker).Coordinates);
                        spawnedEntities++;
                        Logger.GetSawmill("au14.threat").Debug(
                            $"[DEBUG] Spawned other entity of protoId {protoId} at marker {marker}");
                    }
                }
            }

            Logger.GetSawmill("au14.threat").Debug( $"[DEBUG] Spawned {spawnedEntities} other threat entities.");

            var spawnedThreatPlayers = new HashSet<NetUserId>();

            if (voteHeldPlayers != null)
            {
                var eligibleHeldPlayers = GetEligibleVoteHeldPlayers(voteHeldPlayers, requireObserverForVotePlayers);
                _random.Shuffle(eligibleHeldPlayers);
                var heldAssignments = eligibleHeldPlayers
                    .Select(player =>
                    {
                        var job = assignedJobs.TryGetValue(player, out var assigned) &&
                                  assigned.Item1 == ThreatLeaderJobId
                            ? ThreatLeaderJobId
                            : ThreatMemberJobId;

                        return new ThreatVoteAssignment(player, job);
                    })
                    .ToList();

                var voteAssignments = ThreatVoteSelection.BuildSpawnAssignments(
                    heldAssignments,
                    spawnedLeaders.Count,
                    spawnedMembers.Count);

                var assignedLeaders = AssignThreatMinds(
                    voteAssignments.Where(assignment => assignment.Job == ThreatLeaderJobId),
                    spawnedLeaders,
                    spawnedThreatPlayers);
                var assignedMembers = AssignThreatMinds(
                    voteAssignments.Where(assignment => assignment.Job == ThreatMemberJobId),
                    spawnedMembers,
                    spawnedThreatPlayers);

                AddGhostRolesForUnassigned(spawnedLeaders, assignedLeaders, ThreatLeaderJobId);
                AddGhostRolesForUnassigned(spawnedMembers, assignedMembers, ThreatMemberJobId);

                Logger.GetSawmill("au14.threat").Debug(
                    $"[DEBUG] Voted threat assigned {assignedLeaders} leader(s), {assignedMembers} member(s), exposed {spawnedLeaders.Count - assignedLeaders + spawnedMembers.Count - assignedMembers} ghost role body/bodies.");

                var removedVoteAssignments = RemoveThreatJobAssignments(assignedJobs, spawnedThreatPlayers);
                if (removedVoteAssignments > 0)
                    Logger.GetSawmill("au14.threat").Warning($"[AuThreatSystem] Removed {removedVoteAssignments} unspawned voted threat assignment(s).");
                return;
            }

            var leaderPlayers = assignedJobs.Where(x => x.Value.Item1 == ThreatLeaderJobId).Select(x => x.Key).ToList();
            var memberPlayers = assignedJobs.Where(x => x.Value.Item1 == ThreatMemberJobId).Select(x => x.Key).ToList();
            var leaderAssignments = leaderPlayers
                .Select(player => new ThreatVoteAssignment(player, ThreatLeaderJobId))
                .ToList();
            var memberAssignments = memberPlayers
                .Select(player => new ThreatVoteAssignment(player, ThreatMemberJobId))
                .ToList();

            var assignedRoundstartLeaders = AssignThreatMinds(leaderAssignments, spawnedLeaders, spawnedThreatPlayers);
            Logger.GetSawmill("au14.threat").Debug(
                $"[DEBUG] Assigned {assignedRoundstartLeaders} leader minds");
            var assignedRoundstartMembers = AssignThreatMinds(memberAssignments, spawnedMembers, spawnedThreatPlayers);
            Logger.GetSawmill("au14.threat").Debug(
                $"[DEBUG] Assigned {assignedRoundstartMembers} member minds");
            var removed = RemoveThreatJobAssignments(assignedJobs, spawnedThreatPlayers);
            if (removed > 0)
                Logger.GetSawmill("au14.threat").Warning($"[AuThreatSystem] Removed {removed} unspawned threat assignment(s) so normal overflow assignment can handle them.");
        }
    }

    private List<NetUserId> GetEligibleVoteHeldPlayers(
        IReadOnlyList<NetUserId> heldPlayers,
        bool requireObserver)
    {
        var eligible = new List<NetUserId>(heldPlayers.Count);
        foreach (var playerId in heldPlayers)
        {
            if (!_playerManager.TryGetSessionById(playerId, out var session) ||
                session.Status == SessionStatus.Disconnected)
            {
                continue;
            }

            if (requireObserver &&
                !_entityManager.TryGetComponent(session.AttachedEntity, out GhostComponent? _))
            {
                continue;
            }

            eligible.Add(playerId);
        }

        return eligible;
    }

    private int AssignThreatMinds(
        IEnumerable<ThreatVoteAssignment> assignments,
        IReadOnlyList<EntityUid> entities,
        HashSet<NetUserId> spawnedThreatPlayers)
    {
        var assigned = 0;
        foreach (var assignment in assignments)
        {
            if (assigned >= entities.Count)
                break;

            if (!TryAssignThreatMind(assignment.Player, entities[assigned], assignment.Job))
                continue;

            spawnedThreatPlayers.Add(assignment.Player);
            assigned++;
        }

        return assigned;
    }

    private bool TryAssignThreatMind(
        NetUserId playerNetId,
        EntityUid entity,
        ProtoId<JobPrototype> jobId)
    {
        if (!_playerManager.TryGetSessionById(playerNetId, out var session))
        {
            Logger.GetSawmill("content").Error($"[THREAT SPAWN] Could not find session for player {playerNetId}");
            return false;
        }

        var ticker = _entityManager.EntitySysManager.GetEntitySystem<GameTicker>();
        ticker.PlayerJoinGame(session, silent: true);

        GhostRoleComponent? ghostRole = null;
        if (TryComp(entity, out ghostRole) && ghostRole.MakeSentient)
            MakeSentientCommand.MakeSentient(entity, EntityManager, ghostRole.AllowMovement, ghostRole.AllowSpeech);

        var data = session.ContentData();
        var mind = _mindSystem.GetMind(playerNetId);
        if (mind == null)
        {
            mind = _mindSystem.CreateMind(playerNetId, data?.Name ?? "Threat Player");
            _mindSystem.SetUserId(mind.Value, playerNetId);
            Logger.GetSawmill("au14.threat").Debug($"[DEBUG] Created mind for threat player {playerNetId}");
        }

        _mindSystem.TransferTo(mind.Value, entity);
        Logger.GetSawmill("au14.threat").Debug(
            $"[DEBUG] Assigned threat mind {mind.Value} to entity {entity} for player {playerNetId} as {jobId.Id}");

        var entityJob = ghostRole?.JobProto ?? jobId;
        _roles.MindAddJobRole(mind.Value, silent: true, jobPrototype: entityJob);
        AddStartingMindRole(entity, mind.Value);
        _roles.MindAddRole(mind.Value, "MindRoleThreat", silent: true);
        AddThreatFaction(entity);

        if (ghostRole != null)
        {
            _ghostRole.UnregisterGhostRole((entity, ghostRole));
        }

        return true;
    }

    private void AddStartingMindRole(EntityUid entity, EntityUid mind)
    {
        if (TryComp(entity, out StartingMindRoleComponent? starting))
            _roles.MindAddRole(mind, starting.MindRole, silent: starting.Silent);
    }

    private void AddGhostRolesForUnassigned(
        IReadOnlyList<EntityUid> entities,
        int assignedCount,
        ProtoId<JobPrototype> jobId)
    {
        for (var i = Math.Max(0, assignedCount); i < entities.Count; i++)
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
        ghostRole.MindRoles = new List<EntProtoId> { "MindRoleThreat" };

        EnsureComp<GhostTakeoverAvailableComponent>(entity);
    }

    private void AddThreatFaction(EntityUid entity)
    {
        EnsureComp<NpcFactionMemberComponent>(entity);
        _npcFaction.AddFaction((entity, CompOrNull<NpcFactionMemberComponent>(entity)), threatnpcfaction);
    }
}
