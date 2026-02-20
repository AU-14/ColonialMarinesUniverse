using System.Linq;
using Content.Server.AU14.Round;
using Content.Shared.AU14.Threats;
using Robust.Shared.Map;
using Content.Shared.Roles;
using Content.Shared.Mind;
using Content.Server.GameTicking;
using Content.Shared._RMC14.Dropship;
using Content.Shared.AU14.util;
using Content.Shared.Players;
using Robust.Shared.Random;
using Robust.Server.Player;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Content.Server.AU14.VendorMarker;
using Content.Shared.Ghost;
using Content.Shared.ParaDrop;
using Content.Shared._RMC14.CrashLand;
using Content.Server.Chat.Systems;
using Robust.Shared.Timing;
using Robust.Shared.EntitySerialization;

namespace Content.Server.AU14.ThirdParty;

public sealed class AuThirdPartySystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    private readonly ISawmill _sawmill = Logger.GetSawmill("thirdparty");
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly AuRoundSystem _auRoundSystem = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly SharedDropshipSystem _sharedDropshipSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    // --- State for round third party spawning ---
    private ThreatPrototype? _currentThreat;
    private List<AuThirdPartyPrototype>? _thirdPartyList;
    private int _nextThirdPartyIndex = 0;
    private float _spawnTimer = 0f;
    private TimeSpan _spawnInterval = TimeSpan.FromMinutes(5);
    private bool _spawningActive = false;

    public void SpawnThirdParty(AuThirdPartyPrototype party, PartySpawnPrototype spawnProto, bool roundStart, Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)>? assignedJobs = null, bool? overrideDropship = null)
    {
        const float SpawnTogetherRadius = 8f;

        const float PlayerAvoidRadius = 20f;
        _sawmill.Debug($"[AuThirdPartySystem] Spawning third party: {party.ID}");

        // Determine entry method. If overrideDropship is provided, it takes precedence (true => shuttle, false => ground).
        var entryMethod = overrideDropship.HasValue
            ? (overrideDropship.Value ? "shuttle" : "ground")
            : (party.EntryMethod?.ToLowerInvariant() ?? "ground");
        _sawmill.Debug($"[AuThirdPartySystem] Entry method: {entryMethod} (overrideDropship={overrideDropship})");

        List<EntityUid> markerEntities = new();
        EntityUid mainGridUid = EntityUid.Invalid;
        bool parachuteMode = false;

        // Maintain compatibility with existing code that uses these locals.
        var newpartySpawn = spawnProto;
        bool useDropship = entryMethod == "shuttle";

        if (entryMethod == "shuttle")
        {
            // Dropship step (existing behavior)
            var foundDestination = false;
            EntityUid? chosenDestination = null;
            var destQuery = _entityManager.EntityQueryEnumerator<DropshipDestinationComponent, TransformComponent>();
            while (destQuery.MoveNext(out var destUid, out var destComp, out var destXform))
            {
                if (destComp.Ship == null && string.IsNullOrEmpty(destComp.FactionController))
                {
                    foundDestination = true;
                    chosenDestination = destUid;
                    break;
                }
            }
            if (!foundDestination)
            {
                _sawmill.Error("[AuThirdPartySystem] No valid dropship destination found (not landed, not controlled). Aborting third party spawn.");
                return;
            }
            _sawmill.Debug($"[AuThirdPartySystem] Found valid dropship destination: {chosenDestination}");
            var deserializationOpts = DeserializationOptions.Default with { InitializeMaps = true };
            if (!_mapLoader.TryLoadMap(party.dropshippath, out var dropshipMap, out var grids, deserializationOpts))
            {
                _sawmill.Error($"[AuThirdPartySystem] Failed to load dropship map: {party.dropshippath}");
                return;
            }
            mainGridUid = grids.FirstOrDefault();
            if (mainGridUid == EntityUid.Invalid)
            {
                _sawmill.Error($"[AuThirdPartySystem] No grids found in dropship map: {party.dropshippath}");
                return;
            }
            _sawmill.Debug($"[AuThirdPartySystem] Dropship grid initialized: {mainGridUid}");


            var navQuery = _entityManager.EntityQueryEnumerator<DropshipNavigationComputerComponent, TransformComponent>();
            EntityUid? navUid = null;
            DropshipNavigationComputerComponent? navComp = null;
            while (navQuery.MoveNext(out var uid, out var comp, out var xform))
            {
                if (xform.ParentUid == mainGridUid)
                {
                    navUid = uid;
                    navComp = comp;
                    break;
                }
            }

            if (navUid != null && navComp != null && chosenDestination != null)
            {
                var navEntity = new Entity<DropshipNavigationComputerComponent>(navUid.Value, navComp);
                _sharedDropshipSystem.FlyTo(navEntity, chosenDestination.Value, null);
                _sawmill.Debug($"[AuThirdPartySystem] Commanded dropship nav computer {navUid} to fly to destination {chosenDestination}");
            }
            else
            {
                _sawmill.Warning($"[AuThirdPartySystem] Could not find navigation computer on dropship grid {mainGridUid}; the dropship may not be able to travel.");
            }



            // Collect markers on dropship grid
            var query = _entityManager.EntityQueryEnumerator<AuInsertMarkerComponent>();
            while (query.MoveNext(out var uid, out _))
            {
                var gridUid = _entityManager.GetComponent<TransformComponent>(uid).GridUid;
                if (gridUid != null && gridUid.Value == mainGridUid)
                    markerEntities.Add(uid);
            }
            _sawmill.Debug($"[AuThirdPartySystem] Dropship markers collected: {markerEntities.Count}");
            // Spawn consoles
            var vmarkerQuery = _entityManager.EntityQueryEnumerator<VendorMarkerComponent>();
            int consoleCount = 0;
            while (vmarkerQuery.MoveNext(out var vmarkerUid, out var vmarkerComp))
            {
                var markerXform = _entityManager.GetComponent<TransformComponent>(vmarkerUid);
                if (markerXform.GridUid != mainGridUid)
                    continue;
                switch (vmarkerComp.Class)
                {
                    case PlatoonMarkerClass.DSPilot:
                        _entityManager.SpawnEntity("CMComputerDropshipNavigationThirdParty", markerXform.Coordinates);
                        consoleCount++;
                        break;
                    case PlatoonMarkerClass.DSWeapons:
                        _entityManager.SpawnEntity("CMComputerDropshipWeapons", markerXform.Coordinates);
                        consoleCount++;
                        break;
                }
            }
            _sawmill.Debug($"[AuThirdPartySystem] Dropship consoles spawned: {consoleCount}");
        }
        else if (entryMethod == "parachute")
        {
            // Parachute mode: collect parachute markers on the main map
            parachuteMode = true;
            var pQuery = _entityManager.EntityQueryEnumerator<ParachuteMarkerComponent, TransformComponent>();
            while (pQuery.MoveNext(out var uid, out var pComp, out var pxform))
            {
                // Parachute markers are reusable and do not need to be marked as used; include all of them.
                markerEntities.Add(uid);
            }
            _sawmill.Debug($"[AuThirdPartySystem] Parachute markers collected: {markerEntities.Count}");
        }
        else
        {
            // Ground spawn: collect all markers on main map (existing behavior)
            var query = _entityManager.EntityQueryEnumerator<AuInsertMarkerComponent>();
            while (query.MoveNext(out var uid, out _))
            {
                markerEntities.Add(uid);
            }
            _sawmill.Debug($"[AuThirdPartySystem] Main map markers collected: {markerEntities.Count}");
        }

        MapId? mapId = null;
        if (markerEntities.Count > 0)
            mapId = _entityManager.GetComponent<TransformComponent>(markerEntities[0]).MapID;

        List<EntityUid> GetMarkers(ThreatMarkerType markerType)
        {
            var markerId = newpartySpawn != null && newpartySpawn.Markers.TryGetValue(markerType, out var id) ? id : "";
            var markers = new List<EntityUid>();
            var query = _entityManager.EntityQueryEnumerator<ThreatSpawnMarkerComponent>();
            while (query.MoveNext(out var uid, out var comp))
            {
                if (comp.ThreatMarkerType == markerType && (comp.ID == markerId || (comp.ID == "" && markerId == "")))
                {
                    if (mapId == null || _entityManager.GetComponent<TransformComponent>(uid).MapID == mapId)
                    {
                        // Only include markers that are not already used
                        if (!comp.Used)
                            markers.Add(uid);
                    }
                }
            }

            _sawmill.Debug($"[AuThirdPartySystem] GetMarkers({markerType}): Found {markers.Count} unused markers with markerId '{markerId}' on map {mapId}");
            return markers;
        }
        bool spawnTogether = newpartySpawn?.SpawnTogether == true;
        Dictionary<ThreatMarkerType, List<EntityUid>> markerCache = new();
        EntityUid? centerMarker = null;
        if (spawnTogether)
        {
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

        bool IsMarkerBlockedByPlayers(EntityUid marker)
        {
            // Only check main-map/groundside markers; dropship spawns handled elsewhere via useDropship

                var markerCoords = _entityManager.GetComponent<TransformComponent>(marker).Coordinates;
                _sawmill.Debug($"[AuThirdPartySystem] Checking marker {marker} at coords {markerCoords}");

                foreach (var session in _playerManager.Sessions)
                {
                    if (!session.AttachedEntity.HasValue)
                    {
                        _sawmill.Debug($"[AuThirdPartySystem] Session has no attached entity, skipping");
                        continue;
                    }

                    var attached = session.AttachedEntity.Value;
                    _sawmill.Debug($"[AuThirdPartySystem] Found attached entity {attached} for session");

                    // Skip ghosts
                    if (_entityManager.HasComponent<GhostComponent>(attached))
                    {
                        _sawmill.Debug($"[AuThirdPartySystem] Attached entity {attached} is a ghost, skipping");
                        continue;
                    }

                    if (!_entityManager.TryGetComponent<TransformComponent>(attached, out var playerXform))
                    {
                        _sawmill.Debug($"[AuThirdPartySystem] Could not get TransformComponent for attached entity {attached}, skipping");
                        continue;
                    }

                    // Log check steps for debugging
                    _sawmill.Debug($"[AuThirdPartySystem] Checking player {attached} for proximity to marker {marker} (player coords={playerXform.Coordinates}, marker coords={markerCoords})");

                    if (_transform.InRange(playerXform.Coordinates, markerCoords, PlayerAvoidRadius))
                    {
                        _sawmill.Debug($"[AuThirdPartySystem] Marker {marker} is blocked by player {attached} within radius {PlayerAvoidRadius}");
                        return true;
                    }
                    else
                    {
                        _sawmill.Debug($"[AuThirdPartySystem] Player {attached} not within avoid radius of marker {marker}");
                    }
                }

                return false;
        }



        EntityUid PickSafeMarker(List<EntityUid> candidates)
        {
            if (candidates.Count == 0)
                return EntityUid.Invalid;

            // Shuffle candidates for fairness
            var shuffled = candidates.OrderBy(_ => _random.Next()).ToList();
            foreach (var m in shuffled)
            {
                if (!IsMarkerBlockedByPlayers(m))
                    return m;
            }

            // Fallback: no safe marker found, return a random one
            return candidates[_random.Next(candidates.Count)];
        }

        var spawnedLeaders = new List<EntityUid>();
        var spawnedGrunts = new List<EntityUid>();
        var SpawnedEnts = new List<EntityUid>();
         // Track the last marker we used during this spawn operation
         EntityUid? lastUsedMarker = null;
        // Before spawning, verify we have enough unused markers for each required type. If not, abort the spawn.
        var leaderReq = spawnProto.LeadersToSpawn.Values.Sum();
        var gruntReq = spawnProto.GruntsToSpawn.Values.Sum();
        var entityReq = spawnProto.entitiestospawn.Values.Sum();

        var leaderMarkers = GetSpawnMarkers(Content.Shared.AU14.Threats.ThreatMarkerType.Leader);
        var gruntMarkers = GetSpawnMarkers(Content.Shared.AU14.Threats.ThreatMarkerType.Member);
        var entityMarkers = GetSpawnMarkers(Content.Shared.AU14.Threats.ThreatMarkerType.Entity);

        // If parachute mode, use the parachute marker pool for all types; make local mutable copies so we can pick without replacement during this spawn
        if (parachuteMode)
        {
            leaderMarkers = markerEntities.ToList();
            gruntMarkers = markerEntities.ToList();
            entityMarkers = markerEntities.ToList();
        }

        // If this is a groundside spawn, ensure there are enough *safe* markers (unused and not near alive players).
        if (!useDropship)
        {
            var safeLeaderMarkers = leaderMarkers.Where(m => !IsMarkerBlockedByPlayers(m)).ToList();
            var safeGruntMarkers = gruntMarkers.Where(m => !IsMarkerBlockedByPlayers(m)).ToList();
            var safeEntityMarkers = entityMarkers.Where(m => !IsMarkerBlockedByPlayers(m)).ToList();

            if (safeLeaderMarkers.Count < leaderReq || safeGruntMarkers.Count < gruntReq || safeEntityMarkers.Count < entityReq)
            {
                _sawmill.Warning($"[AuThirdPartySystem] Not enough safe markers to spawn third party {party.ID}: leaders needed {leaderReq}, safe available {safeLeaderMarkers.Count}; grunts needed {gruntReq}, safe available {safeGruntMarkers.Count}; entities needed {entityReq}, safe available {safeEntityMarkers.Count}. Aborting spawn.");
                return;
            }

            // Replace marker pools with safe lists so subsequent selection never picks an unsafe marker.
            leaderMarkers = safeLeaderMarkers;
            gruntMarkers = safeGruntMarkers;
            entityMarkers = safeEntityMarkers;
        }
        else
        {
            // For dropship spawns we still require unused markers, as before
            if (leaderMarkers.Count < leaderReq || gruntMarkers.Count < gruntReq || entityMarkers.Count < entityReq)
            {
                _sawmill.Warning($"[AuThirdPartySystem] Not enough unused dropship markers to spawn third party {party.ID}: leaders needed {leaderReq}, available {leaderMarkers.Count}; grunts needed {gruntReq}, available {gruntMarkers.Count}; entities needed {entityReq}, available {entityMarkers.Count}. Aborting spawn.");
                return;
            }
        }

        _sawmill.Debug($"[AuThirdPartySystem] Spawning leaders...");
        // Spawn leaders
        foreach (var (protoId, count) in spawnProto.LeadersToSpawn)
        {
            for (int i = 0; i < count; i++)
            {
                // Select a groundside marker that is not too close to alive players (exclude freshly spawned entities)
                EntityUid marker;
                if (parachuteMode && !useDropship)
                {
                    // pick a random safe marker from leaderMarkers and remove it so it's not reused this spawn
                    var safe = leaderMarkers.Where(m => !IsMarkerBlockedByPlayers(m)).ToList();
                    if (safe.Count == 0)
                        marker = PickSafeMarker(leaderMarkers);
                    else
                        marker = safe[_random.Next(safe.Count)];
                    leaderMarkers.Remove(marker);
                }
                else
                {
                    marker = useDropship ? leaderMarkers[_random.Next(leaderMarkers.Count)] : PickSafeMarker(leaderMarkers);
                }
                var coords = _entityManager.GetComponent<TransformComponent>(marker).Coordinates;
                var ent = _entityManager.SpawnEntity(protoId, coords);
                // If parachute mode, hand off to the shared paradrop system so the entity falls from the sky.
                if (parachuteMode)
                {
                    // Ensure the entity is paradroppable; SharedParaDropSystem will fall back to crash-land if missing.
                    var paraComp = EnsureComp<ParaDroppableComponent>(ent);
                    Dirty(ent, paraComp);

                    // Raise AttemptCrashLandEvent on the grid entity that the parachute marker resides on so the para-drop handler will run.
                    var markerXform = _entityManager.GetComponent<TransformComponent>(marker);
                    if (markerXform.GridUid.HasValue)
                    {
                        var gridEntity = markerXform.GridUid.Value;
                        var attemptEvent = new Content.Shared._RMC14.CrashLand.AttemptCrashLandEvent(ent);
                        RaiseLocalEvent(gridEntity, ref attemptEvent);
                    }
                }
                spawnedLeaders.Add(ent);
                // Mark this marker's component as used (do NOT mark neighbors yet)
                if (_entityManager.TryGetComponent<ThreatSpawnMarkerComponent>(marker, out var lmComp) && !lmComp.Used)
                {
                    lmComp.Used = true;
                    Dirty(marker, lmComp);
                }
                // Parachute markers are intentionally NOT marked as used so they may be reused.
                 lastUsedMarker = marker;
                 _sawmill.Debug($"[AuThirdPartySystem] Spawned leader {protoId} at {coords} (entity {ent})");
            }
        }
        _sawmill.Debug($"[AuThirdPartySystem] Spawning grunts...");
        foreach (var (protoId, count) in spawnProto.GruntsToSpawn)
        {
            for (int i = 0; i < count; i++)
            {
                EntityUid marker;
                if (parachuteMode && !useDropship)
                {
                    var safe = gruntMarkers.Where(m => !IsMarkerBlockedByPlayers(m)).ToList();
                    if (safe.Count == 0)
                        marker = PickSafeMarker(gruntMarkers);
                    else
                        marker = safe[_random.Next(safe.Count)];
                    gruntMarkers.Remove(marker);
                }
                else
                {
                    marker = useDropship ? gruntMarkers[_random.Next(gruntMarkers.Count)] : PickSafeMarker(gruntMarkers);
                }
                var coords = _entityManager.GetComponent<TransformComponent>(marker).Coordinates;
                var ent = _entityManager.SpawnEntity(protoId, coords);
                if (parachuteMode)
                {
                    var paraComp = EnsureComp<ParaDroppableComponent>(ent);
                    Dirty(ent, paraComp);

                    var markerXform = _entityManager.GetComponent<TransformComponent>(marker);
                    if (markerXform.GridUid.HasValue)
                    {
                        var gridEntity = markerXform.GridUid.Value;
                        var attemptEvent = new Content.Shared._RMC14.CrashLand.AttemptCrashLandEvent(ent);
                        RaiseLocalEvent(gridEntity, ref attemptEvent);
                    }
                }
                 spawnedGrunts.Add(ent);
                 // Mark this marker's component as used (do NOT mark neighbors yet)
                 if (_entityManager.TryGetComponent<ThreatSpawnMarkerComponent>(marker, out var gmComp) && !gmComp.Used)
                 {
                     gmComp.Used = true;
                     Dirty(marker, gmComp);
                 }
                 // Parachute markers are intentionally NOT marked as used so they may be reused.
                  lastUsedMarker = marker;
                  _sawmill.Debug($"[AuThirdPartySystem] Spawned grunt {protoId} at {coords} (entity {ent})");
            }
        }
        _sawmill.Debug($"[AuThirdPartySystem] Spawning ents...");
        foreach (var (protoId, count) in spawnProto.entitiestospawn)
        {
            for (int i = 0; i < count; i++)
            {
                EntityUid marker;
                if (parachuteMode && !useDropship)
                {
                    var safe = entityMarkers.Where(m => !IsMarkerBlockedByPlayers(m)).ToList();
                    if (safe.Count == 0)
                        marker = PickSafeMarker(entityMarkers);
                    else
                        marker = safe[_random.Next(safe.Count)];
                    entityMarkers.Remove(marker);
                }
                else
                {
                    marker = useDropship ? entityMarkers[_random.Next(entityMarkers.Count)] : PickSafeMarker(entityMarkers);
                }
                var coords = _entityManager.GetComponent<TransformComponent>(marker).Coordinates;
                var ent = _entityManager.SpawnEntity(protoId, coords);
                if (parachuteMode)
                {
                    var paraComp = EnsureComp<ParaDroppableComponent>(ent);
                    Dirty(ent, paraComp);

                    var markerXform = _entityManager.GetComponent<TransformComponent>(marker);
                    if (markerXform.GridUid.HasValue)
                    {
                        var gridEntity = markerXform.GridUid.Value;
                        var attemptEvent = new Content.Shared._RMC14.CrashLand.AttemptCrashLandEvent(ent);
                        RaiseLocalEvent(gridEntity, ref attemptEvent);
                    }
                }
                 SpawnedEnts.Add(ent);
                 // Mark this marker's component as used (do NOT mark neighbors yet)
                 if (_entityManager.TryGetComponent<ThreatSpawnMarkerComponent>(marker, out var emComp) && !emComp.Used)
                 {
                     emComp.Used = true;
                     Dirty(marker, emComp);
                 }
                 // Parachute markers are intentionally NOT marked as used so they may be reused.
                  lastUsedMarker = marker;
                  _sawmill.Debug($"[AuThirdPartySystem] Spawned ent {protoId} at {coords} (entity {ent})");
            }
        }

        // After all spawns: if spawnTogether is true, mark nearby unused markers around the last used marker.
        void MarkNeighborsIfNeeded()
        {
            if (!spawnTogether || lastUsedMarker == null)
                return;

            var centerMarkerUid = lastUsedMarker.Value;
            if (!_entityManager.TryGetComponent<ThreatSpawnMarkerComponent>(centerMarkerUid, out var centerComp))
                return;

            var centerXform = _entityManager.GetComponent<TransformComponent>(centerMarkerUid);
            var centerCoords = centerXform.Coordinates;
            var centerMap = centerXform.MapID;

            var query = _entityManager.EntityQueryEnumerator<ThreatSpawnMarkerComponent>();
            while (query.MoveNext(out var otherUid, out var _))
            {
                if (otherUid == centerMarkerUid)
                    continue;

                var otherXform = _entityManager.GetComponent<TransformComponent>(otherUid);
                if (otherXform.MapID != centerMap)
                    continue;

                if (_transform.InRange(otherXform.Coordinates, centerCoords, SpawnTogetherRadius))
                {
                    if (_entityManager.TryGetComponent<ThreatSpawnMarkerComponent>(otherUid, out var otherComp) && !otherComp.Used)
                    {
                        otherComp.Used = true;
                        Dirty(otherUid, otherComp);
                    }
                }
            }
        }

        // Run neighbor-marking now (only once per spawn operation, using the last used marker)
        MarkNeighborsIfNeeded();

        if (roundStart && assignedJobs != null)
        {
            _sawmill.Debug($"[AuThirdPartySystem] Assigning minds to third party entities (roundstart)");
            var leaderJobId = new ProtoId<JobPrototype>("AU14JobThirdPartyLeader");
            var memberJobId = new ProtoId<JobPrototype>("AU14JobThirdPartyMember");
            var leaderPlayers = assignedJobs.Where(x => x.Value.Item1 == leaderJobId).Select(x => x.Key).ToList();
            var memberPlayers = assignedJobs.Where(x => x.Value.Item1 == memberJobId).Select(x => x.Key).ToList();
            var mindSystem = _entityManager.System<SharedMindSystem>();
            var roleSystem = _entityManager.System<SharedRoleSystem>();
            for (int i = 0; i < leaderPlayers.Count && i < spawnedLeaders.Count; i++)
            {
                var playerNetId = leaderPlayers[i];
                var entity = spawnedLeaders[i];
                if (!_playerManager.TryGetSessionById(playerNetId, out var session))
                    continue;
                var ticker = _entityManager.System<GameTicker>();
                ticker.PlayerJoinGame(session, silent: true);
                var data = session.ContentData();
                var mind = mindSystem.GetMind(playerNetId) ?? mindSystem.CreateMind(playerNetId, data?.Name ?? "Third Party Player");
                mindSystem.SetUserId(mind, playerNetId);
                mindSystem.TransferTo(mind, entity);
                roleSystem.MindAddJobRole(mind, silent: true, jobPrototype: "AU14JobThirdPartyLeader");
            }
            for (int i = 0; i < memberPlayers.Count && i < spawnedGrunts.Count; i++)
            {
                var playerNetId = memberPlayers[i];
                var entity = spawnedGrunts[i];
                if (!_playerManager.TryGetSessionById(playerNetId, out var session))
                    continue;
                var ticker = _entityManager.System<GameTicker>();
                ticker.PlayerJoinGame(session, silent: true);
                var data = session.ContentData();
                var mind = mindSystem.GetMind(playerNetId) ?? mindSystem.CreateMind(playerNetId, data?.Name ?? "Third Party Player");
                mindSystem.SetUserId(mind, playerNetId);
                mindSystem.TransferTo(mind, entity);
                roleSystem.MindAddJobRole(mind, silent: true, jobPrototype: "AU14JobThirdPartyMember");
            }
        }
        if (!string.IsNullOrWhiteSpace(party.AnnounceArrival))
        {
            _chat.DispatchGlobalAnnouncement(party.AnnounceArrival, "", playSound: false, colorOverride: Color.DarkOrange);
            _sawmill.Info($"[AuThirdPartySystem] Announced arrival for third party {party.ID}: {party.AnnounceArrival}");
        }

    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (!_spawningActive || _thirdPartyList == null || _currentThreat == null)
            return;
        if (_nextThirdPartyIndex >= _thirdPartyList.Count)
        {
            _spawningActive = false;
            return;
        }
        _spawnTimer += frameTime;
        var party = _thirdPartyList[_nextThirdPartyIndex];
        if (party.RoundStart)
        {
            _nextThirdPartyIndex++;
            return;
        }
        int ghostCount = _playerManager.Sessions.Count(s => s.AttachedEntity == null || _entityManager.HasComponent<GhostComponent>(s.AttachedEntity));
        if (ghostCount < party.GhostsNeeded)
        {
            return;
        }
        var interval = TimeSpan.FromTicks((long)(_spawnInterval.Ticks / Math.Max(1, party.weight)));
        if (_spawnTimer < interval.TotalSeconds)
            return;
        _spawnTimer = 0f;
        int roll = _random.Next(1, 101);
        int chance = Math.Clamp(party.weight * 10, 5, 100); // Example: weight 1 = 10%, weight 10 = 100%
        if (roll <= chance)
        {
            if (_prototypeManager.TryIndex(party.PartySpawn, out var spawnProto))
            {
                SpawnThirdParty(party, spawnProto, false);
                _sawmill.Debug($"[AuThirdPartySystem] Spawned third party {party.ID} (roll {roll} <= {chance})");
            }
            else
            {
                _sawmill.Error($"[AuThirdPartySystem] No spawn proto for third party {party.ID} (PartySpawn={party.PartySpawn})");
            }
            _nextThirdPartyIndex++;
        }
        else
        {
            _sawmill.Debug($"[AuThirdPartySystem] Did not spawn {party.ID} (roll {roll} > {chance})");
        }
    }


    public void StartThirdPartySpawning(ThreatPrototype threat, Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)>? assignedJobs = null)
    {
        _currentThreat = threat;
        _thirdPartyList = _auRoundSystem.SelectedThirdParties.ToList();
        _nextThirdPartyIndex = 0;
        _spawnTimer = 0f;
        if (_currentThreat != null)
        {
            try
            {
                _spawnInterval = TimeSpan.FromSeconds(Math.Max(1, _currentThreat.ThirdPartyInterval));
            }
            catch
            {
                _sawmill.Warning("[AuThirdPartySystem] Invalid ThirdPartyInterval on threat; using default interval.");
            }
        }
        else
        {

                var selectedPlanetField = _auRoundSystem.GetType().GetField("_selectedPlanet", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var selectedPlanet = selectedPlanetField?.GetValue(_auRoundSystem) as Content.Shared._RMC14.Rules.RMCPlanetMapPrototypeComponent;
                if (selectedPlanet != null && selectedPlanet.ThirdPartyInterval.HasValue)
                {
                    var val = Math.Max(1, selectedPlanet.ThirdPartyInterval.Value);
                    _spawnInterval = TimeSpan.FromSeconds(val);
                    _sawmill.Debug($"[AuThirdPartySystem] Using planet spawn interval: {val} seconds (no active threat)");
                }


        }

        if (_thirdPartyList == null || _thirdPartyList.Count == 0)
        {
            _sawmill.Debug("[AuThirdPartySystem] No third parties selected for this planet; skipping third-party spawning.");
            _spawningActive = false;
            return;
        }

        _spawningActive = true;
        // Spawn all roundstart third parties immediately (called after jobs assigned)
        if (_thirdPartyList != null)
        {
            foreach (var party in _thirdPartyList)
            {
                if (!party.RoundStart)
                    break;

                if (_prototypeManager.TryIndex<PartySpawnPrototype>(party.PartySpawn, out var spawnProto))
                {
                    SpawnThirdParty(party, spawnProto, true, assignedJobs);
                    _sawmill.Debug($"[AuThirdPartySystem] Spawned roundstart third party {party.ID}");
                }
                else
                {
                    _sawmill.Error($"[AuThirdPartySystem] No spawn proto for roundstart third party {party.ID} (PartySpawn={party.PartySpawn})");
                }
                _nextThirdPartyIndex++;
            }
        }
    }


}

