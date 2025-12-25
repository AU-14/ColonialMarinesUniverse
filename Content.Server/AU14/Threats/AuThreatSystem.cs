using System.Collections.Generic;
using System.Linq;
using Content.Shared.AU14.Threats;
using Content.Server.AU14.Round;
using Robust.Shared.Prototypes;
using Robust.Shared.Map;
using Content.Shared.Roles;
using Content.Shared.Mind;
using Content.Server.GameTicking;
using Robust.Shared.Network;
using Content.Shared.AU14.Threats;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.Players;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Log;
using Robust.Shared.Random;

namespace Content.Server.AU14.Threats;

public sealed class AuThreatSystem : EntitySystem
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly SharedMindSystem _mindSystem = default!;
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    public readonly ProtoId<NpcFactionPrototype> threatnpcfaction = "THREAT";
    [Dependency] private readonly SharedRoleSystem _roles = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    /// <summary>
    /// Spawns the chosen threat's leaders, members, and entities at their correct markers at round start.
    /// Also assigns player minds to spawned threat entities for threat jobs.
    /// </summary>
    public void SpawnThreatAtRoundStart(ThreatPrototype threat,
        MapId mapId,
        Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)> assignedJobs)
    {
        if (threat == null)
        {
            Logger.Error("[AuThreatSystem] SpawnThreatAtRoundStart called with null threat prototype. Aborting spawn.");
            return;
        }

        var partySpawn = threat.RoundStartSpawn;
        if (string.IsNullOrWhiteSpace(partySpawn))
        {
            Logger.DebugS("au14.threat", $"[DEBUG] Threat '{threat.ID}' has no RoundStartSpawn configured, skipping spawn.");
            return;
        }
        var newpartySpawn = _prototypeManager.TryIndex(partySpawn, out var spawn) ? spawn : null;
        if (newpartySpawn == null)
        {
            Logger.ErrorS("au14.threat", $"[ERROR] Could not find RoundStartSpawn prototype '{partySpawn}' for threat '{threat.ID}'. Skipping threat spawn.");
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
                if (comp.ThreatMarkerType == markerType && (comp.ID == markerId || (comp.ID == "" && markerId == "")))
                {
                    if (_entityManager.GetComponent<TransformComponent>(uid).MapID == mapId)
                        markers.Add(uid);
                }
            }
            Logger.DebugS("au14.threat",
                $"[DEBUG] GetMarkers({markerType}): Found {markers.Count} markers with markerId '{markerId}' on map {mapId}");
            return markers;
        }

        // --- Spawn entities and collect them for mind assignment ---
        var spawnedLeaders = new List<EntityUid>();
        var spawnedMembers = new List<EntityUid>();
        Logger.DebugS("au14.threat", $"[DEBUG] Begin spawning threat entities for threat: {threat?.ID ?? "null"}");

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
                        return coords.InRange(_entityManager, centerCoords, 50f);
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
            foreach (var (protoId, count) in newpartySpawn.LeadersToSpawn)
            {
                var markers = GetSpawnMarkers(ThreatMarkerType.Leader);
                Logger.DebugS("au14.threat",
                    $"[DEBUG] Spawning {count} leaders of protoId {protoId} at {markers.Count} markers");
                for (int i = 0; i < count; i++)
                {
                    var marker = markers.Count > 0 ? markers[i % markers.Count] : EntityUid.Invalid;
                    if (marker != EntityUid.Invalid)
                    {
                        var ent = _entityManager.SpawnEntity(protoId,
                            _entityManager.GetComponent<TransformComponent>(marker).Coordinates);
                        spawnedLeaders.Add(ent);
                        Logger.DebugS("au14.threat", $"[DEBUG] Spawned leader entity {ent} at marker {marker}");
                    }
                }
            }

            // Spawn grunts/members
            foreach (var (protoId, count) in newpartySpawn.GruntsToSpawn)
            {
                var markers = GetSpawnMarkers(ThreatMarkerType.Member);
                Logger.DebugS("au14.threat",
                    $"[DEBUG] Spawning {count} members of protoId {protoId} at {markers.Count} markers");
                for (int i = 0; i < count; i++)
                {
                    var marker = markers.Count > 0 ? markers[i % markers.Count] : EntityUid.Invalid;
                    if (marker != EntityUid.Invalid)
                    {
                        var ent = _entityManager.SpawnEntity(protoId,
                            _entityManager.GetComponent<TransformComponent>(marker).Coordinates);
                        spawnedMembers.Add(ent);
                        Logger.DebugS("au14.threat", $"[DEBUG] Spawned member entity {ent} at marker {marker}");
                    }
                }
            }

            Logger.DebugS("au14.threat", $"[DEBUG] Spawned {spawnedMembers.Count} threat members.");

            // Spawn other entities
            var spawnedEntities = 0;
            foreach (var (protoId, count) in newpartySpawn.entitiestospawn)
            {
                var markers = GetSpawnMarkers(ThreatMarkerType.Entity);
                Logger.DebugS("au14.threat",
                    $"[DEBUG] Spawning {count} other entities of protoId {protoId} at {markers.Count} markers");
                for (int i = 0; i < count; i++)
                {
                    var marker = markers.Count > 0 ? markers[i % markers.Count] : EntityUid.Invalid;
                    if (marker != EntityUid.Invalid)
                    {
                        _entityManager.SpawnEntity(protoId,
                            _entityManager.GetComponent<TransformComponent>(marker).Coordinates);
                        spawnedEntities++;
                        Logger.DebugS("au14.threat",
                            $"[DEBUG] Spawned other entity of protoId {protoId} at marker {marker}");
                    }
                }
            }

            Logger.DebugS("au14.threat", $"[DEBUG] Spawned {spawnedEntities} other threat entities.");

            // Assign jobs and minds
            var threatLeaderJobId = new ProtoId<JobPrototype>("AU14JobThreatLeader");
            var threatMemberJobId = new ProtoId<JobPrototype>("AU14JobThreatMember");
            var leaderPlayers = assignedJobs.Where(x => x.Value.Item1 == threatLeaderJobId).Select(x => x.Key).ToList();
            var memberPlayers = assignedJobs.Where(x => x.Value.Item1 == threatMemberJobId).Select(x => x.Key).ToList();

            // Assign leader minds
            for (int i = 0; i < leaderPlayers.Count && i < spawnedLeaders.Count; i++)
            {
                var playerNetId = leaderPlayers[i];
                var entity = spawnedLeaders[i];
                // Get session
                if (!_playerManager.TryGetSessionById(playerNetId, out var session))
                {
                    Logger.Error($"[THREAT SPAWN] Could not find session for leader player {playerNetId}");
                    continue;
                }

                // Ensure player is joined to the round
                var ticker = _entityManager.EntitySysManager.GetEntitySystem<GameTicker>();
                ticker.PlayerJoinGame(session, silent: true);
                // Ensure mind exists
                var data = session.ContentData();
                var mind = _mindSystem.GetMind(playerNetId);
                if (mind == null)
                {
                    mind = _mindSystem.CreateMind(playerNetId, data?.Name ?? "Threat Player");
                    _mindSystem.SetUserId(mind.Value, playerNetId);
                    Logger.DebugS("au14.threat", $"[DEBUG] Created mind for leader player {playerNetId}");
                }

                // Transfer mind to threat entity
                _mindSystem.TransferTo(mind.Value, entity);
                Logger.DebugS("au14.threat",
                    $"[DEBUG] Assigned leader mind {mind.Value} to entity {entity} for player {playerNetId}");
                // Assign job role
                _roles.MindAddJobRole(mind.Value, silent: true, jobPrototype: "AU14JobThreatLeader");
                // Add to threat NPC faction
                EnsureComp<Content.Shared.NPC.Components.NpcFactionMemberComponent>(entity);
                _npcFaction.AddFaction((entity,
                        CompOrNull<Content.Shared.NPC.Components.NpcFactionMemberComponent>(entity)),
                    threatnpcfaction);
            }

            Logger.DebugS("au14.threat",
                $"[DEBUG] Assigned {Math.Min(leaderPlayers.Count, spawnedLeaders.Count)} leader minds");
            // Assign member minds
            for (int i = 0; i < memberPlayers.Count && i < spawnedMembers.Count; i++)
            {
                var playerNetId = memberPlayers[i];
                var entity = spawnedMembers[i];
                if (!_playerManager.TryGetSessionById(playerNetId, out var session))
                {
                    Logger.Error($"[THREAT SPAWN] Could not find session for member player {playerNetId}");
                    continue;
                }
                // Ensure player is joined to the round
                var ticker = _entityManager.EntitySysManager.GetEntitySystem<GameTicker>();
                ticker.PlayerJoinGame(session, silent: true);
                // Ensure mind exists
                var data = session.ContentData();
                var mind = _mindSystem.GetMind(playerNetId);
                if (mind == null)
                {
                    mind = _mindSystem.CreateMind(playerNetId, data?.Name ?? "Threat Player");
                    _mindSystem.SetUserId(mind.Value, playerNetId);
                    Logger.DebugS("au14.threat", $"[DEBUG] Created mind for member player {playerNetId}");
                }

                // Transfer mind to threat entity
                _mindSystem.TransferTo(mind.Value, entity);
                Logger.DebugS("au14.threat",
                    $"[DEBUG] Assigned member mind {mind.Value} to entity {entity} for player {playerNetId}");
                // Assign job role
                _roles.MindAddJobRole(mind.Value, silent: true, jobPrototype: "AU14JobThreatMember");
                // Add to threat NPC faction
                EnsureComp<Content.Shared.NPC.Components.NpcFactionMemberComponent>(entity);
                _npcFaction.AddFaction((entity,
                        CompOrNull<Content.Shared.NPC.Components.NpcFactionMemberComponent>(entity)),
                    threatnpcfaction);
            }

            Logger.DebugS("au14.threat",
                $"[DEBUG] Assigned {Math.Min(memberPlayers.Count, spawnedMembers.Count)} member minds");
        }
    }
}
